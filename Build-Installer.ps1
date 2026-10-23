$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $projectRoot 'WindowsKeepAwake.sln'
$projectFile = Join-Path $projectRoot 'src\KeepAwake.Windows\KeepAwake.Windows.csproj'
$testDllRelativePath = 'KeepAwake.Tests.dll'
$scriptFile = Join-Path $projectRoot 'installer\WindowsKeepAwake.iss'
$version = ([xml](Get-Content -LiteralPath $projectFile -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
if (-not $version -or $version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Versão semântica não encontrada em $projectFile"
}

$userDotnet = Join-Path $env:LOCALAPPDATA 'Programs\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $userDotnet) { $userDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$sdkVersion = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^8\.') {
    throw ".NET SDK 8 é obrigatório. Executável: $dotnet; versão encontrada: $sdkVersion"
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
if (-not $isccCandidates) { throw 'Inno Setup 6 não encontrado. Instale-o no escopo do usuário pela fonte oficial e tente novamente.' }
$iscc = [string]($isccCandidates | Select-Object -First 1)
if (-not (Test-Path -LiteralPath $scriptFile)) { throw "Script Inno Setup não encontrado: $scriptFile" }

$artifactsRoot = Join-Path $projectRoot 'artifacts'
$buildBin = Join-Path $artifactsRoot "build\$version\bin\"
$publishDir = Join-Path $artifactsRoot "publish\$version\win-x64"
$installerDir = Join-Path $artifactsRoot "installer\$version"
$testDll = Join-Path $buildBin 'Release\net8.0-windows\KeepAwake.Tests.dll'
$installerFile = Join-Path $installerDir "WindowsKeepAwake-Setup-$version-win-x64.exe"
New-Item -ItemType Directory -Force -Path $buildBin, $publishDir, $installerDir | Out-Null

Write-Host "SDK .NET: $sdkVersion ($dotnet)"
Write-Host "Inno Setup: $iscc"
Write-Host "Versão: $version"

& $dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falhou com código $LASTEXITCODE" }
& $dotnet restore $projectFile --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw "restore do runtime win-x64 falhou com código $LASTEXITCODE" }

& $dotnet build $solution --configuration Release --no-restore "-p:BaseOutputPath=$buildBin" -p:DebugType=None
if ($LASTEXITCODE -ne 0) { throw "dotnet build falhou com código $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $testDll)) { throw "Assembly de testes não encontrado: $testDll" }

Write-Host 'Executando a suíte automatizada (runner STA do projeto)...'
& $dotnet $testDll
if ($LASTEXITCODE -ne 0) { throw "Testes falharam com código $LASTEXITCODE" }

# Publish builds into a versioned artifacts folder and does not overwrite the running development executable.
& $dotnet publish $projectFile --configuration Release --runtime win-x64 --self-contained true --no-restore --output $publishDir `
    "-p:BaseOutputPath=$buildBin" -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou com código $LASTEXITCODE" }

$requiredFiles = @(
    'KeepAwake.Windows.exe',
    'KeepAwake.Windows.dll',
    'KeepAwake.Windows.deps.json',
    'KeepAwake.Windows.runtimeconfig.json',
    'KeepAwake.Core.dll',
    'hostfxr.dll',
    'hostpolicy.dll',
    'coreclr.dll'
)
foreach ($name in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $name))) {
        throw "Arquivo necessário ausente na publicação: $name"
    }
}
$unexpectedFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -File | Where-Object {
    $_.Extension -in @('.pdb', '.log', '.tmp', '.cs', '.csproj', '.sln') -or $_.Name -like '*Tests*'
}
if ($unexpectedFiles) { throw "Publicação contém arquivos não esperados: $($unexpectedFiles.FullName -join ', ')" }
$appVersion = (Get-Item -LiteralPath (Join-Path $publishDir 'KeepAwake.Windows.exe')).VersionInfo.FileVersion
if ($appVersion -notlike "$version*") { throw "Versão do executável ($appVersion) não corresponde à versão do projeto ($version)." }

$isccArguments = @(
    "/DAppPublishDir=$publishDir",
    "/DProjectRoot=$projectRoot",
    "/DAppVersion=$version",
    "/O$installerDir",
    $scriptFile
)
& $iscc @isccArguments
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou com código $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $installerFile)) { throw "Instalador não foi gerado: $installerFile" }

$installer = Get-Item -LiteralPath $installerFile
$signature = Get-AuthenticodeSignature -LiteralPath $installerFile
Write-Host ''
Write-Host 'Publicação e instalador gerados:'
Write-Host "  Publicação: $publishDir"
Write-Host "  Instalador: $($installer.FullName)"
Write-Host "  Tamanho: $([math]::Round($installer.Length / 1MB, 2)) MiB"
Write-Host "  Assinatura Authenticode: $($signature.Status)"
Write-Host "  Arquivos empacotados antes do instalador: $((Get-ChildItem -LiteralPath $publishDir -Recurse -File).Count)"

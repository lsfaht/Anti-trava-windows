$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot 'src\KeepAwake.Windows\KeepAwake.Windows.csproj'
$profile = Join-Path $projectRoot 'src\KeepAwake.Windows\Properties\PublishProfiles\win-x64.pubxml'
$publishDir = Join-Path $projectRoot 'artifacts\publish\win-x64'
$installerDir = Join-Path $projectRoot 'artifacts\installer'
$scriptFile = Join-Path $projectRoot 'installer\WindowsKeepAwake.iss'
$userDotnet = Join-Path $env:LOCALAPPDATA 'Programs\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $userDotnet) { $userDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if (-not $isccCandidates) { throw 'Inno Setup 6 não encontrado. Instale-o pela fonte oficial e tente novamente.' }
if (-not (Test-Path -LiteralPath $profile)) { throw "Perfil de publicação não encontrado: $profile" }

New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null
& $dotnet publish $projectFile --configuration Release --runtime win-x64 --self-contained true -p:PublishProfile=$profile -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou com código $LASTEXITCODE" }

$arguments = @(
    "/DAppPublishDir=$publishDir",
    "/DProjectRoot=$projectRoot",
    "/O$installerDir",
    $scriptFile
)
& $isccCandidates @arguments
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou com código $LASTEXITCODE" }

$installer = Join-Path $installerDir 'WindowsKeepAwake-Setup-1.0.0-win-x64.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw "Instalador não foi gerado: $installer" }
Get-Item -LiteralPath $installer | Select-Object FullName, Length, LastWriteTime

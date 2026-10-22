@echo off
setlocal

set "WK_BASE=%~dp0"
set "WK_APP=%WK_BASE%src\KeepAwake.Windows\bin\Release\net8.0-windows\KeepAwake.Windows.exe"
set "WK_DOTNET=%LOCALAPPDATA%\Programs\dotnet"
set "WK_DESKTOP_RUNTIME="

if not exist "%WK_APP%" goto :missing_app

call :find_runtime "%WK_DOTNET%"
if not defined WK_DESKTOP_RUNTIME call :find_runtime "%ProgramFiles%\dotnet"
if not defined WK_DESKTOP_RUNTIME goto :missing_runtime

set "DOTNET_ROOT=%WK_DESKTOP_RUNTIME%"
set "DOTNET_ROOT_X64=%WK_DESKTOP_RUNTIME%"
set "PATH=%WK_DESKTOP_RUNTIME%;%PATH%"
set "WK_BASE=%WK_BASE%"
set "WK_APP=%WK_APP%"

"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -Command "$app=Get-Item -LiteralPath $env:WK_APP -ErrorAction Stop; $srcRoot=Join-Path $env:WK_BASE 'src'; $inputs=@(Get-ChildItem -LiteralPath $srcRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.ico','.resx','.props','.targets' }); $solution=Join-Path $env:WK_BASE 'WindowsKeepAwake.sln'; if(Test-Path -LiteralPath $solution){$inputs+=Get-Item -LiteralPath $solution}; if($inputs.Count -gt 0){$latest=($inputs | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum; if($latest -gt $app.LastWriteTimeUtc){exit 3}}"
set "WK_CHECK=%ERRORLEVEL%"
if "%WK_CHECK%"=="3" goto :stale_app
if not "%WK_CHECK%"=="0" goto :check_failed

start "" "%WK_APP%"
exit /b 0

:find_runtime
for /d %%R in ("%~1\shared\Microsoft.WindowsDesktop.App\8.*") do if exist "%%~fR\System.Windows.Forms.dll" set "WK_DESKTOP_RUNTIME=%~1"
exit /b 0

:missing_app
echo Executavel Release nao encontrado:
echo "%WK_APP%"
echo Compile explicitamente com o SDK .NET 8 antes de iniciar:
echo "%WK_DOTNET%\dotnet.exe" build "%WK_BASE%WindowsKeepAwake.sln" --configuration Release
pause
exit /b 1

:missing_runtime
echo O runtime .NET 8 Desktop para Windows x64 nao foi encontrado.
echo Instale o .NET 8 Desktop Runtime por meio do instalador oficial da Microsoft ou solicite suporte ao administrador.
pause
exit /b 1

:stale_app
echo O executavel Release esta desatualizado em relacao aos fontes do projeto.
echo Compile explicitamente com:
echo "%WK_DOTNET%\dotnet.exe" build "%WK_BASE%WindowsKeepAwake.sln" --configuration Release
pause
exit /b 3

:check_failed
echo Nao foi possivel verificar se o executavel Release esta atualizado. Nenhum build foi iniciado.
pause
exit /b 1

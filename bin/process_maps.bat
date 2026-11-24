@echo off
setlocal
if exist "%~dp0..\tools\dmsh-ts\src\Dmsh.Cli\Dmsh.Cli.csproj" goto workspace
if exist "%~dp0Dmsh.Cli.exe" goto published
echo Could not find the Dmsh CLI project or executable. 1>&2
exit /b 1

:workspace
if "%~1"=="" goto default_batch
dotnet run --project "%~dp0..\tools\dmsh-ts\src\Dmsh.Cli" -- %*
exit /b %errorlevel%

:default_batch
dotnet run --project "%~dp0..\tools\dmsh-ts\src\Dmsh.Cli" -- batch
exit /b %errorlevel%

:published
if "%~1"=="" goto published_batch
"%~dp0Dmsh.Cli.exe" %*
exit /b %errorlevel%

:published_batch
"%~dp0Dmsh.Cli.exe" batch
exit /b %errorlevel%

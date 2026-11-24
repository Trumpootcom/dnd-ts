@echo off
setlocal
if exist "%~dp0..\tools\plmh-ts\src\Plmh.Cli\Plmh.Cli.csproj" goto workspace
if exist "%~dp0Plmh.Cli.exe" goto published
echo Could not find the Plmh CLI project or executable. 1>&2
exit /b 1

:workspace
if "%~1"=="" goto default_batch
dotnet run --project "%~dp0..\tools\plmh-ts\src\Plmh.Cli" -- %*
exit /b %errorlevel%

:default_batch
dotnet run --project "%~dp0..\tools\plmh-ts\src\Plmh.Cli" -- batch
exit /b %errorlevel%

:published
if "%~1"=="" goto published_batch
"%~dp0Plmh.Cli.exe" %*
exit /b %errorlevel%

:published_batch
"%~dp0Plmh.Cli.exe" batch
exit /b %errorlevel%

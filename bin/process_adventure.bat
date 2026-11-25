@echo off
setlocal
if exist "%~dp0..\tools\adventure-ts\src\Adventure.Cli\Adventure.Cli.csproj" goto workspace
if exist "%~dp0Adventure.Cli.exe" goto published
echo Could not find the Adventure CLI project or executable. 1>&2
exit /b 1

:workspace
dotnet run --project "%~dp0..\tools\adventure-ts\src\Adventure.Cli" -- %*
exit /b %errorlevel%

:published
"%~dp0Adventure.Cli.exe" %*
exit /b %errorlevel%

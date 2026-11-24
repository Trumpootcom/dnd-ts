@echo off
setlocal
pushd "%~dp0" || exit /b 1
if exist "Dmsh.Cli.exe" (
    ".\Dmsh.Cli.exe" batch %*
) else (
    dotnet run --project "src\Dmsh.Cli" -- batch %*
)
set "dmsh_exit=%errorlevel%"
popd
exit /b %dmsh_exit%

@echo off
setlocal
if exist "%~dp0..\tools\dmsh-ts\src\Dmsh.Cli\Dmsh.Cli.csproj" (
    pushd "%~dp0.." || exit /b 1
    dotnet run --project "tools\dmsh-ts\src\Dmsh.Cli" -- batch %*
) else (
    pushd "%~dp0" || exit /b 1
    if exist "Dmsh.Cli.exe" (
        ".\Dmsh.Cli.exe" batch %*
    ) else (
        echo Could not find the Dmsh CLI project or executable. 1>&2
        set "dmsh_exit=1"
    )
)
if not defined dmsh_exit set "dmsh_exit=%errorlevel%"
popd
exit /b %dmsh_exit%

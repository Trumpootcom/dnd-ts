#!/bin/sh

SCRIPT_DIR=$(CDPATH= cd "$(dirname "$0")" && pwd)
PROJECT="$SCRIPT_DIR/../tools/dmsh-ts/src/Dmsh.Cli"

if [ "$#" -eq 0 ]; then
    set -- batch
fi

if [ -f "$PROJECT/Dmsh.Cli.csproj" ]; then
    exec dotnet run --project "$PROJECT" -- "$@"
fi

if [ -x "$SCRIPT_DIR/Dmsh.Cli" ]; then
    exec "$SCRIPT_DIR/Dmsh.Cli" "$@"
fi

if [ -f "$SCRIPT_DIR/Dmsh.Cli.dll" ]; then
    exec dotnet "$SCRIPT_DIR/Dmsh.Cli.dll" "$@"
fi

echo "Could not find the Dmsh CLI project or executable." >&2
exit 1

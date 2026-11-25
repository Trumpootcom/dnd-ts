#!/bin/sh

SCRIPT_DIR=$(CDPATH= cd "$(dirname "$0")" && pwd)
PROJECT="$SCRIPT_DIR/../tools/adventure-ts/src/Adventure.Cli"

if [ -f "$PROJECT/Adventure.Cli.csproj" ]; then
    exec dotnet run --project "$PROJECT" -- "$@"
fi

if [ -x "$SCRIPT_DIR/Adventure.Cli" ]; then
    exec "$SCRIPT_DIR/Adventure.Cli" "$@"
fi

if [ -f "$SCRIPT_DIR/Adventure.Cli.dll" ]; then
    exec dotnet "$SCRIPT_DIR/Adventure.Cli.dll" "$@"
fi

echo "Could not find the Adventure CLI project or executable." >&2
exit 1

#!/bin/sh

SCRIPT_DIR=$(CDPATH= cd "$(dirname "$0")" && pwd) || exit 1
PROJECT="$SCRIPT_DIR/../tools/plmh-ts/src/Plmh.Cli"

if [ "$#" -eq 0 ]; then
    set -- batch
fi

if [ -f "$PROJECT/Plmh.Cli.csproj" ]; then
    exec dotnet run --project "$PROJECT" -- "$@"
fi

if [ -x "$SCRIPT_DIR/Plmh.Cli" ]; then
    exec "$SCRIPT_DIR/Plmh.Cli" "$@"
fi

if [ -f "$SCRIPT_DIR/Plmh.Cli.dll" ]; then
    exec dotnet "$SCRIPT_DIR/Plmh.Cli.dll" "$@"
fi

echo "Could not find the Plmh CLI project or executable." >&2
exit 1

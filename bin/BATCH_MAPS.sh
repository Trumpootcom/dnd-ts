#!/bin/sh

SCRIPT_DIR=$(CDPATH= cd "$(dirname "$0")" && pwd)
if [ -f "$SCRIPT_DIR/../tools/dmsh-ts/src/Dmsh.Cli/Dmsh.Cli.csproj" ]; then
    WORKSPACE=$(CDPATH= cd "$SCRIPT_DIR/.." && pwd)
    cd "$WORKSPACE" || exit 1
    exec dotnet run --project tools/dmsh-ts/src/Dmsh.Cli -- batch "$@"
fi

cd "$SCRIPT_DIR" || exit 1
if [ -x "$SCRIPT_DIR/Dmsh.Cli" ]; then
    exec "$SCRIPT_DIR/Dmsh.Cli" batch "$@"
fi

echo "Could not find the Dmsh CLI project or executable." >&2
exit 1

# D&D Tools Adventure Batch

Run from the workspace containing `in_maps/` and `in_portraits/`.

```bat
bin\process_adventure.bat "The Old Farmhouse"
bin\process_adventure.bat "The Old Farmhouse" --date 261205 --template merged.json
bin\process_adventure.bat --help
bin\process_adventure.bat --version
bin\process_adventure.bat --templates
```

```sh
sh bin/process_adventure.sh "The Old Farmhouse"
sh bin/process_adventure.sh "The Old Farmhouse" --date 261205 --template merged.json
sh bin/process_adventure.sh --help
sh bin/process_adventure.sh --version
sh bin/process_adventure.sh --templates
```

The adventure title is required. `--date` accepts a calendar date in `YYMMDD`
and defaults to the local date. The date prefixes the output folder, archive
folder, and merged PDF filename. It also appears in the run summary and
merged PDF metadata.
`--version` shows the suite version and the component versions in use.

## Templates

`--template` selects a bundled name or a JSON file path. The bundled default
is `T000`. Save this example as `merged.json` in the workspace to use the
second command above.

```json
{
  "name": "Merged adventure",
  "map_template": "T000",
  "portraits_template": "T000",
  "individualPDF": false,
  "mergeOrder": "encounter"
}
```

- `name` labels the preset. The adventure title comes from the command line.
- `map_template` and `portraits_template` select a bundled name or file path
  for [DMSH](../dmsh-ts/README.md) and [PLMH](../plmh-ts/README.md). Relative
  paths are resolved from the workspace.
- `individualPDF` is `true` in T000. It keeps one PDF per map image and one
  lineup and portrait-card PDF per encounter. Set it to `false` to publish
  a single PDF named after the adventure folder.
- `mergeOrder` defaults to `encounter`. It places each encounter's maps before
  its portraits. Use `type` for all maps followed by all portraits. Both
  modes sort encounter numbers numerically (E1, E2, E10). Put the encounter
  ID in the map filename as a separate part delimited by spaces, underscores,
  or hyphens, as in `ASHEN-E1-MAP02D_DM 140x110.png`. Multiple
  maps for an encounter are sorted by filename. Maps without numbered IDs
  follow numbered maps in `type` mode and go at the end in `encounter` mode.
  Page sizes and orientations are preserved.

## Build, review, and archive

For portrait templates with a lineup title band, the adventure batch uses
`<adventure title> - <map name>`. It derives the map name from the filename
by removing the extension and trailing dimensions. For example, the map
above gives `The Old Farmhouse - ASHEN-E1-MAP02D_DM`. If there is no matching
map name or there are several distinct names, the heading remains `Encounter e1`
for encounter `e1`.

The command reads the supported images at the top level of the input
folders. You can build maps alone, portraits alone, or both. Every input
image must succeed. An empty run or a generation or merge failure leaves
prior output and source images in place and skips the archive prompt.

Successful builds replace the entire `out/<YYMMDD> - <title>/` directory
with the current run's PDFs. Open and inspect the listed PDFs while the
prompt waits. Reply `y` to move all originals to one flat archive directory
such as `bak/261205-TheOldFarmhouse/`. Reply `n` or press Enter to leave them in
the input folders. Archive filename conflicts are reported before any image
moves.

Each invocation builds from the current inputs. To build again after
archiving, manually return the originals to their input folders first.

## Packaging

Running from a checkout uses the .NET 10 SDK. For distribution, publish the
adventure CLI as a self-contained build for the target platform. The publish
directory includes the .NET runtime, PDFsharp, both generators, their
templates, and the adventure launchers. Keep that directory together for
offline use. To record exact bundle versions, save the published command's
`--version` output with the release.

```sh
dotnet publish tools/adventure-ts/src/Adventure.Cli -c Release -r win-x64 --self-contained true
dotnet publish tools/adventure-ts/src/Adventure.Cli -c Release -r osx-arm64 --self-contained true
```

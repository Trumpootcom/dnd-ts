# D&D Tools Adventure Batch

Run from the workspace root. Pass a title to look for
`adventures/<title>.adventure.json`, or pass a relative or absolute path to an
`.adventure.json` file. A title with no matching file uses the staged inputs in
`in_maps/` and `in_portraits/`. An invalid adventure file or missing explicit
path stops the build with an error.

```bat
bin\process_adventure.bat "Jebs Cabin"
bin\process_adventure.bat "adventures\Jebs Cabin.adventure.json" --date 261205 --template merged.json
bin\process_adventure.bat --help
bin\process_adventure.bat --version
bin\process_adventure.bat --templates
```

```sh
sh bin/process_adventure.sh "Jebs Cabin"
sh bin/process_adventure.sh "adventures/Jebs Cabin.adventure.json" --date 261205 --template merged.json
sh bin/process_adventure.sh --help
sh bin/process_adventure.sh --version
sh bin/process_adventure.sh --templates
```

The title comes from the adventure file in file mode and from the command
line in staged mode. `--date` accepts a calendar date in `YYMMDD` and takes
precedence over the adventure file's optional date. With neither set, the
command uses the local date. The date prefixes the output folder, archive
folder, and merged PDF filename. It also appears in the run summary and
merged PDF metadata.
`--version` shows the suite version and the component versions in use.

## Adventure files

See [Building Jebs Cabin Adventure](Tutorial.md) for a complete walkthrough.

See [Jebs Cabin](../../adventures/Jebs%20Cabin.adventure.json) for a complete
example with reusable creatures. The Old Farmhouse example uses placeholder
image paths that need to be filled in before building.

- Use a `title` and an `encounters` array. Each encounter has a unique numbered
  `id` such as `ENC1`, an optional `name` for PDF filenames, and `maps` and
  `portraits` lists as needed. The name defaults to the adventure title.
- In the `maps` array, reference a library location with
  `"location": "../library/locations/Jebs Cabin.json"`.
  The map JSON supplies `file` and a required positive `widthFeet`. Its image
  path is relative to the map JSON. Existing inline map entries with a
  `file` still work. Their optional `widthFeet` defaults to the selected map
  template's `worldWidthFeet`, which is 160 feet in T000.
- A portrait has a `tokenId` unique within its encounter. Specify a `file`,
  `size`, and `type`, or use `creature` to reference a creature JSON file
  with defaults for `name`, `size`, `type`, and `portrait`. Encounter entries
  can override those defaults via `name`, `size`, `type`, and `file`.
  Without a name, the filename stem supplies it. Both the creature file and
  the encounter portrait entry accept an optional `height` string in feet or
  feet-inches, such as `"5"` or `"5-2"`. The encounter height overrides the
  creature height. When both are omitted, the portrait template supplies a
  default height by creature size for lineups and cards.
- Add a positive integer `qty` to reuse the portrait and its metadata for
  several tokens. With `"tokenId": "B", "qty": 2`, the tokens are `B1` and
  `B2`. Numbering starts at 1 even when `qty` is 1. Omitting `qty` uses the
  supplied token ID as-is. Expanded IDs must be unique within the encounter.
  The composite lineup shows each group once with the original `tokenId`
  as its label. Every numbered token gets a portrait card.
- Location references, inline image paths, and creature paths are relative to the adventure file. A creature
  file's default portrait path is relative to the creature file itself.
  The same image can be referenced by multiple encounters or portrait tokens.

Map overviews and portrait lineup title bands use
`<adventure title> - <encounter ID> - <encounter name>`.
Portrait lineup titles are rendered in small caps.

Validation reports missing resources and invalid encounter metadata before
rendering. Successful builds publish to the workspace's `out/` folder.
When a build publishes exactly one PDF, the command opens it in the system's
default PDF viewer. If the viewer cannot open, the PDF remains in `out/`.
Adventure-file builds run without an archive prompt and keep library assets
in place.

## Add a new location

1. Put the map image in `library/maps/`, for example `Old Mill.png`.
2. Create `library/locations/Old Mill.json`. Set `file` relative to the
   location JSON and set `widthFeet` to the map's full width in feet. Use a
   positive number.

   ```json
   {
     "file": "../maps/Old Mill.png",
     "widthFeet": 120
   }
   ```

3. Add a location reference to the encounter's `maps` array. For an adventure
   file in `adventures/`, use this entry.

   ```json
   { "location": "../library/locations/Old Mill.json" }
   ```

## Add a new creature

1. Put the image in `library/portraits/`, for example `Mill Guard.png`.
2. Create `library/creatures/mill-guard.json`. Set the display `name`, creature
   `size` and `type`, and the `portrait` path relative to the creature JSON.
   Optionally set `height` as a string in feet or feet-inches. For example,
   `"5-10"` means 5 feet 10 inches.

   ```json
   {
     "name": "Mill Guard",
     "size": "medium",
     "type": "humanoid",
     "portrait": "../portraits/Mill Guard.png",
     "height": "5-10"
   }
   ```

3. Add a creature reference to the encounter's `portraits` array. Use a
   unique `tokenId` within the encounter. Add `qty` for multiple copies.

   ```json
   {
     "creature": "../library/creatures/mill-guard.json",
     "tokenId": "B",
     "qty": 2
   }
   ```

   This gives you one lineup portrait labeled `B` and two cards labeled
   `B1` and `B2`. Set `height` on the encounter entry to override the
   creature's height for that entry.

## Share an adventure bundle

From the workspace root, export an adventure by title or by its
`.adventure.json` path under `adventures/`.

```bat
bin\process_adventure.bat "Jebs Cabin" --export "Jebs Cabin.dndts"
```

```sh
sh bin/process_adventure.sh "Jebs Cabin" --export "Jebs Cabin.dndts"
```

A `.dndts` file is a ZIP archive containing one adventure and its referenced
files under `library/locations/`, `library/maps/`, `library/creatures/`, and
`library/portraits/`. The archive preserves workspace-relative paths and
includes shared assets once. Use relative references with forward slashes in
the JSON. Export validates the adventure, leaves existing bundles in place,
and produces no PDFs.
Add `--verbose` to the export command to print the archive's folder and file tree.

To build a bundle in another workspace, run:

```bat
bin\process_adventure.bat -f "Jebs Cabin.dndts"
```

```sh
sh bin/process_adventure.sh -f "Jebs Cabin.dndts"
```

The command builds from the bundled assets and publishes PDFs to the
receiving workspace's `out/`. You can use `--date` and `--template` when
building. Adventure templates come from the receiving workspace or the
installed tool.

After a successful build, the command asks whether to import the bundle's
referenced resources into `library/`. Enter `v` to view the resource tree before
the command asks again. Press Enter to decline at either prompt. If you accept,
the import skips identical files and asks before overwriting each differing
file. The adventure JSON stays in the bundle, and the PDFs are available
regardless of your import choices.

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

- `name` labels the preset.
- `map_template` and `portraits_template` select a bundled name or file path
  for [DMSH](../dmsh-ts/README.md) and [PLMH](../plmh-ts/README.md). Relative
  paths are resolved from the workspace.
- `individualPDF` is `true` in T000. In adventure-file mode it publishes one
  `ENC1 - <encounter name> - Maps.pdf` and one
  `ENC1 - <encounter name> - Portraits.pdf` per
  encounter with the corresponding assets. Multiple maps share the Maps PDF
  in filename order. Staged mode keeps one PDF per map image and one lineup
  and portrait-card PDF per encounter. Set `individualPDF` to `false` to
  publish one `ENC1 - <encounter name> - Adventure.pdf` per encounter in
  adventure-file mode, or one PDF named after the adventure folder in staged
  mode.
- `mergeOrder` defaults to `encounter`. It places each encounter's maps before
  its portraits. Use `type` for all maps followed by all portraits. Both
  modes sort encounter numbers numerically (ENC1, ENC2, ENC10). For staged
  builds, put the encounter
  ID in the map filename as a separate part delimited by spaces, underscores,
  or hyphens, as in `ASHEN-E1-MAP02D_DM 140x110.png`. Multiple
  maps for an encounter are sorted by filename. Maps without numbered IDs
  follow numbered maps in `type` mode and go at the end in `encounter` mode.
  Page sizes and orientations are preserved.

## Build, review, and archive staged inputs

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

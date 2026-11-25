# D&D Tools

This repository is an umbrella for tabletop role-playing tools. Each tool has
its own project folder under `tools/`, while shared launchers, sample assets,
and workflow folders live at the repository root.

## Process an adventure

Run the adventure batch from the workspace root. A title looks for
`adventures/<title>.adventure.json`. You can also pass that file's path directly.

```bat
bin\process_adventure.bat "Jebs Cabin"
```

```sh
sh bin/process_adventure.sh "Jebs Cabin"
```

The cabin adventure uses reusable maps and portraits from `library/`. Builds
write PDFs to `out/<YYMMDD> - <title>/` and leave the library files in place.
An adventure portrait entry can use `qty` to create numbered tokens from one
creature reference. The lineup shows each group once with its token prefix,
and every numbered token gets a portrait card.
Creature files can supply a default height, and encounter portrait entries
can override it.
Use `location` in an encounter map entry to reference a JSON file in
`library/locations/` that stores the image path and width in feet.
If a title has no matching adventure file, the command reads staged images
from `in_maps/` and `in_portraits/` and offers to archive them after a successful
build. The default template publishes separate adventure-file PDFs such as
`ENC1 - Jebs Cabin - Maps.pdf` and `ENC1 - Jebs Cabin - Portraits.pdf`. These
use the encounter's name. Map and portrait overviews use
`<adventure title> - <encounter ID> - <encounter name>`. Merged output uses `- Adventure.pdf` for each
encounter. See the
[adventure workflow guide](tools/adventure-ts/README.md) for file fields,
date defaults, templates, and merged PDFs.

<!-- Suite reference starts here. See AGENTS.md for editing rules. -->

## Folder structure

```text
dnd-ts/
├── adventures/      Adventure files and examples
├── bak/             Shared archive for successfully processed inputs
├── bin/             Shared command launchers
├── in_maps/         Map images waiting for batch processing
├── in_portraits/    Portrait images waiting for batch processing
├── library/         Reusable adventure assets
│   ├── creatures/   Reusable creature metadata
│   ├── locations/   Map metadata, including width
│   ├── maps/        Map images
│   └── portraits/   Portrait images
├── out/             Shared output folder for generated files
├── samples/         Sample files for each tool
│   └── maps/        DMSH sample maps; copy to in_maps/ to process
└── tools/
    ├── adventure-ts/ Adventure batch workflow and suite version
    ├── dmsh-ts/     Desktop Map Slicer Headless Tool Suite
    └── plmh-ts/     Portrait Layout Manager Headless Tool Suite
```

Add future tools as sibling folders under `tools/`. PLMH uses the shared
`in_portraits/` input folder. All tools can use the
shared `bak/` and `out/` folders; tools should choose clear filenames to avoid
output collisions.

## Tools

Run these examples from the repository root.

- **Adventure** ([Help and full README](tools/adventure-ts/README.md))
  - **Tool folder** `tools/adventure-ts/`
  - **Name** D&D Tools Adventure Batch
  - **Windows command** `bin\process_adventure.bat "<title-or-adventure-file>"`
  - **macOS command** `sh bin/process_adventure.sh "<title-or-adventure-file>"`
  - **Description** Builds encounter maps and portraits from an adventure file,
    or builds staged images when no file matches the title.
    Use DMSH or PLMH for a single image. The default adventure template is
    `T000.json` in `tools/adventure-ts/src/Adventure.Cli/Templates/`.

  ```bat
  bin\process_adventure.bat "Jebs Cabin" --date 261205 --template T000
  bin\process_adventure.bat "adventures\Jebs Cabin.adventure.json"
  bin\process_adventure.bat --help
  bin\process_adventure.bat --version
  bin\process_adventure.bat --templates
  ```

  ```sh
  sh bin/process_adventure.sh "Jebs Cabin" --date 261205 --template T000
  sh bin/process_adventure.sh "adventures/Jebs Cabin.adventure.json"
  sh bin/process_adventure.sh --help
  sh bin/process_adventure.sh --version
  sh bin/process_adventure.sh --templates
  ```

- **DMSH** ([Help and full README](tools/dmsh-ts/README.md))
  - **Tool folder** `tools/dmsh-ts/`
  - **Name** Desktop Map Slicer Headless Tool Suite
  - **Windows command** `bin\process_maps.bat [command or image]`
  - **macOS command** `sh bin/process_maps.sh [command or image]`
  - **Description**
    - Pass a map image to slice it into printable sheets and save the PDF beside the image.
    - Run with no arguments or with `batch` to process `in_maps/` and write PDFs to `out/`. Source images stay in place.
    - Bundled templates are in `tools/dmsh-ts/src/Dmsh.Cli/Templates/`. The default is `T000.json`. Use `--template` with a template name or file path.
    - Use `--width` to set the map width in feet. A filename suffix such as `100x60` takes precedence and sets the width to 100 feet.

  **Examples**

  ```bat
  bin\process_maps.bat batch
  bin\process_maps.bat samples\maps\map.png
  bin\process_maps.bat --width 120 samples\maps\map.png
  bin\process_maps.bat --template T001 samples\maps\map.png
  bin\process_maps.bat --help
  bin\process_maps.bat --version
  bin\process_maps.bat --templates
  ```

  ```sh
  sh bin/process_maps.sh batch
  sh bin/process_maps.sh samples/maps/map.png
  sh bin/process_maps.sh --width 120 samples/maps/map.png
  sh bin/process_maps.sh --template T001 samples/maps/map.png
  sh bin/process_maps.sh --help
  sh bin/process_maps.sh --version
  sh bin/process_maps.sh --templates
  ```

- **PLMH** ([Help and full README](tools/plmh-ts/README.md))
  - **Tool folder** `tools/plmh-ts/`
  - **Name** Portrait Layout Manager Headless Tool Suite
  - **Windows command** `bin\process_portraits.bat [command or image]`
  - **macOS command** `sh bin/process_portraits.sh [command or image]`
  - **Description**
    - Pass a portrait image to create a printable PDF beside it. Filenames use the format `encounter_token_name_size_type` with an optional height in the size field, such as `medium(5-2)`. Omitted heights default by creature size for lineups and portrait cards.
    - Run with no arguments or with `batch` to group images from `in_portraits/` and write one PDF per encounter to `out/`. PDFs include a lineup and portrait cards. Source images stay in place.
    - Bundled templates are in `tools/plmh-ts/src/Plmh.Cli/Templates/`. The default is `T000.jsonc`. Use `--template` with a template name or file path, or `--templates` to list bundled templates.
    - Use `--dpi` to change the default image resolution of 100 DPI.

  **Examples**

  ```bat
  bin\process_portraits.bat batch
  bin\process_portraits.bat "in_portraits\e1_f1_mistborn dragon_medium_dragon.png"
  bin\process_portraits.bat batch --dpi 300
  bin\process_portraits.bat batch --template "path\to\custom.jsonc"
  bin\process_portraits.bat --help
  bin\process_portraits.bat --version
  bin\process_portraits.bat --templates
  ```

  ```sh
  sh bin/process_portraits.sh batch
  sh bin/process_portraits.sh "in_portraits/e1_f1_mistborn dragon_medium_dragon.png"
  sh bin/process_portraits.sh batch --dpi 300
  sh bin/process_portraits.sh batch --template "path/to/custom.jsonc"
  sh bin/process_portraits.sh --help
  sh bin/process_portraits.sh --version
  sh bin/process_portraits.sh --templates
  ```

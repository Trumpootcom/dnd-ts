# D&D Tools

This repository is an umbrella for tabletop role-playing tools. Each tool has
its own project folder under `tools/`, while shared launchers, sample assets,
and workflow folders live at the repository root.

## Folder structure

```text
dnd-ts/
├── bak/             Shared archive for successfully processed inputs
├── bin/             Shared command launchers
├── in_maps/         Map images waiting for batch processing
├── in_portraits/    Portrait images waiting for batch processing
├── out/             Shared output folder for generated files
├── samples/         Sample files for each tool
│   └── maps/        DMSH sample maps; copy to in_maps/ to process
└── tools/
    └── dmsh-ts/     Desktop Map Slicer Headless Tool Suite
    └── plmh-ts/     Portrait Layout Manager Headless Tool Suite
```

Add future tools as sibling folders under `tools/`. PLMH uses the shared
`in_portraits/` input folder. All tools can use the
shared `bak/` and `out/` folders; tools should choose clear filenames to avoid
output collisions.

## Tools

Run these examples from the repository root.

- **DMSH** ([Help and full README](tools/dmsh-ts/README.md))
  - **Tool folder** `tools/dmsh-ts/`
  - **Name** Desktop Map Slicer Headless Tool Suite
  - **Windows command** `bin\process_maps.bat [command or image]`
  - **macOS command** `sh bin/process_maps.sh [command or image]`
  - **Description**
    - Pass a map image to slice it into printable sheets and save the PDF beside the image.
    - Run with no arguments or with `batch` to process `in_maps/`, write PDFs to `out/`, and move successfully processed images to `bak/`.
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
    - Pass a portrait image to create a printable PDF beside it. Filenames use the format `encounter_token_name_size_type` with an optional height in the size field, such as `medium(5-2)`.
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

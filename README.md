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
├── out/             Shared output folder for generated files
├── samples/         Sample files for each tool
│   └── maps/        DMSH sample maps; copy to in_maps/ to process
└── tools/
    └── dmsh-ts/     Desktop Map Slicer Headless Tool Suite
```

Add future tools as sibling folders under `tools/`. Add type-specific input
folders, such as `in_portraits/`, when a tool needs them. All tools can use the
shared `bak/` and `out/` folders; tools should choose clear filenames to avoid
output collisions.

## Tools

- **DMSH** (See [DMSH README](tools/dmsh-ts/README.md) for full details.)
  - **Tool folder:** `tools/dmsh-ts/`
  - **Name:** Desktop Map Slicer Headless Tool Suite
  - **Windows command:** `bin\process_maps.bat [command or image]`
  - **macOS command:** `sh bin/process_maps.sh [command or image]`
  - **Description:**
    - Turns tabletop map images into printable PDF sheets.
    - With no arguments or with `batch`
      - batch-processes maps from `in_maps/`
      - writes PDFs to `out/`
      - archives successfully processed images in `bak/`.
    - or pass filename such as `map.png` to slice a single map image.
    - Use `--width 120` to override the map width in feet.
    - A filename ending in `WxH` sets the width from its first number.
      - for example, `E1 Farmhouse 100x60.jpg` uses a width of 100 feet.
    - Use `--template <template_name>` to select a template by name.
      - Templates are in `tools/dmsh-ts/src/Dmsh.Cli/Templates/`.
      - `T000.json` is the default template.

  Examples:

  ```bat
  bin\process_maps.bat                            // batch process in_maps/
  bin\process_maps.bat batch                      // batch process in_maps/
  bin\process_maps.bat map.png                    // process map.png
  bin\process_maps.bat --width 120 map.png        // override width to 120ft
  bin\process_maps.bat E1_Farmhouse_100x60.jpg    // override width to 100ft
  bin\process_maps.bat --template T001 map.png    // override template

  bin\process_maps.bat --help                     // show help file
  bin\process_maps.bat --version                  // show version
  bin\process_maps.bat --templates                // show list of templates
  ```

  ```sh
  sh bin/process_maps.sh                          # batch process in_maps/
  sh bin/process_maps.sh batch                    # batch process in_maps/
  sh bin/process_maps.sh map.png                  # process map.png
  sh bin/process_maps.sh --width 120 map.png      # override width to 120ft
  sh bin/process_maps.sh E1_Farmhouse_100x60.jpg  # override width to 100ft
  sh bin/process_maps.sh --template T001 map.png  # override template

  sh bin/process_maps.sh --help                   # show help
  sh bin/process_maps.sh --version                # show version
  sh bin/process_maps.sh --templates              # show list of templates
  ```

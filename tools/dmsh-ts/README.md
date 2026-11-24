# Desktop Map Slicer Headless Tool Suite

See [TODO.md](TODO.md) for verified compatibility and remaining porting work.
The default PDF layout and dashed grid rendering have been visually checked.
The roadmap also covers numbered JSON templates and an exportable template
inventory for scripts and other users.

A planned .NET 10 command-line tool that turns tabletop battle map images
into printable PDFs.

The CLI accepts a local map image and uses its built-in `T000.json` template
by default. This template comes from `wmsh_ts/src/default.json` and exports a PDF with an
overview page followed by four landscape Ledger sheets in a 2×2 layout.

The goal is to offer self-contained Windows and macOS downloads through
GitHub Releases. A future Avalonia app will use the same C# rendering
library as the CLI.

## Get help

For built-in usage help, run `Dmsh.Cli.exe --help` on Windows or
`./Dmsh.Cli --help` on macOS. From this repository, use
`dotnet run --project src/Dmsh.Cli -- --help`. Run a command with
`--help`, for example `Dmsh.Cli.exe batch --help`, to see its
arguments, behavior, and an example.

## Show the build version

```powershell
dotnet run --project src/Dmsh.Cli -- --version
```

The CLI shows the application version as `Major.Minor.Build`, the short Git
commit hash, a `Modified` marker for staged or unstaged changes to tracked
files, and the UTC build time. Untracked images and generated PDFs do not
affect the marker.
Set Major and Minor in `Directory.Build.props`. Build comes from the Git commit
count and falls back to 0 if Git cannot resolve the revision.

## Export PDFs

The CLI ships templates `T000.json` through `T003.json`.
Run `Dmsh.Cli --templates` to list the templates in the executable's
`Templates` folder. The bundled `T000.json` appears even when that folder
is missing. Add JSON files to that folder to make them available by
filename with `--template`. To save the list from the repository root:

```powershell
dotnet run --project src/Dmsh.Cli -- --templates > templates.txt
```

Each entry uses an eight-character label column for `File:`, `Paper:`,
`Grid:`, and `World:`. The first line shows the filename and the template
name from the JSON. Usable
space subtracts both margins on each sheet, overlap at each join, and two
coordinate bars per axis when the template draws them, then rounds each
dimension down to a multiple of 5 feet. The displayed size is the available
space before any image fitting or cropping.

Both export commands print the program version and Git revision, then
`Using Template:` with the selected filename and its name from the JSON's
`template` field, followed by one
status line per image. Paths are relative to the project root, or to the
working directory when run outside the project.

```text
Processing image: .\map.png -> .\map.pdf
Processing image: incoming\map.png -> output\map.pdf
Processing image: .\bad.png !Bad image file.
```

The arrow means the PDF was saved. If a batch archive move fails after
saving, the same line shows the output path followed by the error.

PDF export uses PDFsharp. The shared library explicitly references
`System.Security.Cryptography.Pkcs` 8.0.2 to replace the vulnerable 8.0.1
dependency brought in by PDFsharp 6.2.4.

```powershell
dotnet run --project src/Dmsh.Cli -- map.png
dotnet run --project src/Dmsh.Cli -- batch
```

Use `--template <name-or-path>` to replace the bundled default.
The `.json` extension is optional, so `--template T002` selects
`T002.json`. For a single
image, put the option before the image filename. The source for the built-in
template is [T000.json](src/Dmsh.Cli/Templates/T000.json).

Pass `--width <feet>` to override the template's `worldWidthFeet` for
one run. If an image filename ends in `WxH` or `WXH` before the extension,
the first number takes precedence over both `--width` and the template.
For example, `ASHEN-E1-MAP02D_DM 150x110.png` uses a world width of 150 feet.
The second number marks the filename pattern. The renderer uses the image's
aspect ratio for placement. In a batch, the override applies to each image
independently. The template file stays intact.

After fitting the map to the requested width and paper, the exporter
rounds both map dimensions down to multiples of 5 feet. It tries scaling
by width and cropping height, then scaling by height and cropping width.
It keeps the result that retains the most of the original image, using
the width-first result on a tie. The crop is centered, and the image
keeps its aspect ratio. If a whole 5-foot cell cannot fit on both axes,
the export reports an error.

Unrotated rectangular grids shift by half a cell on each axis with an odd
number of 5-foot cells. With zero template offsets, grid lines and coordinate
bars align with the map edges on the overview and print sheets. Explicit
`grid.offsetX` and `grid.offsetY` are applied on top of that alignment.

```powershell
dotnet run --project src/Dmsh.Cli -- --width 120 map.png
```

If the filename ends in dimensions, its width wins. This example uses
**150 feet** from `150x110` even though `--width 120` is present:

```powershell
dotnet run --project src/Dmsh.Cli -- --width 120 "ASHEN-E1-MAP02D_DM 150x110.png"
```

Passing one image as the final argument writes `map.pdf` beside `map.png`.
`batch` scans the top level of `incoming/` for JPEG, PNG, BMP and WebP
files. Put your images in `incoming/` before running it.
The CLI creates the sibling folders `output/` and `processed/` as needed.
It saves each PDF in `output/` before moving its source to `processed/`.
Use `--incoming` or `--output` to change those paths. The processed folder
always sits beside the selected incoming folder.

Use the included `map.png` to try a single export, or run batch mode on
the sample images already in `incoming/`. The checkout also includes
`processed/` and `output/`, each with a `.gitkeep` placeholder.
Batch moves the tracked samples, so Git shows their removal from
`incoming/` after a successful run. PDFs and archived copies appear as
untracked files until staged.

On Windows, run `PROCESS_INCOMING.bat` from the repo or a published
Windows build. It runs batch mode using the folder that contains the script.

Both export modes
replace existing PDFs by default after the new PDF has been written
successfully. Existing archive paths and duplicate output names within a
batch are reported as conflicts.

In batch mode, each filename in `incoming/` can specify its own map width
with a suffix like `150x110`.

On Windows, close the existing PDF in Acrobat or another viewer before
rerunning the command if the viewer blocks replacement. If replacement
fails, the existing PDF stays in place.
Batch processing continues after an individual failure and returns exit code 1
if any image failed or was skipped due to a conflict.

The exporter draws the clipped map, a rectangular, isometric, or hex grid,
and sheet badges such as A1 and B1. On unrotated rectangular sheets, set
`"coordinates": false` in the template JSON to omit the coordinate bars
and their reserved space. The default template enables them. The optional
overview shows the rectangular grid, sheet outlines and their badges, with one set of coordinate
bars around the full map when coordinates are enabled.

Set `grid.rotationDegrees` to rotate either grid style. For an isometric
grid, keep `grid.style` as `"rect"` and set `grid.iso` to `true` and
`grid.isoAngleDegrees` to a value from 15 to 90. Use `null` for the angle
when `grid.iso` is false. The settings apply on the map sheets and overview.

Set `grid.style` to `"hex"` for hex grids. Hex grids support rotation,
offsets, and isometric tilt. Grids that exceed the TypeScript limit of
50,000 candidate cells are rejected. Coordinate bars are unavailable for hex grids.

For all three grid types, `grid.lineStyle` supports `solid`, `none`,
`dash-1`, `dash-2`, `dash-3`, `dash-4`, `dash-5`, `dash-6`, `dash-8`,
`dash-10`, `dash-16`, and `corners`, plus the TypeScript legacy patterns
and aliases. The cell-based patterns start at grid intersections so their
phase continues across overlapping sheets. The overview uses the same patterns.
Hex dash lengths are based on each edge's length.

The overview is always landscape and uses the source image filename
without its extension as the displayed map title. It uses the TypeScript
layout formulas for the map area and the left title, legend, and scaling panel. Panel font
sizes scale with the printable page height. Panel text, sheet badges, and
coordinate labels use selectable PDF Helvetica and Helvetica Bold with the
same measurement tables as the TypeScript exporter. PDF viewers may
substitute their own Helvetica glyphs.

On the overview and map sheets, the exporter draws the grid first, then
any coordinate bars, then the page badges so they stay on top.
On individual sheets, coordinate labels for partial grid cells retain their
original placement. Their boxes and text are clipped at the printable margins.
Every sixth coordinate label (30, 60, 90 feet, and so on) has a darker
fill. All coordinate label boxes use the same border width.
Rectangular and isometric grids double the template's `grid.lineWidthPoints`
on every sixth interior grid line in both directions, counting from the
map edges. The edges and hex grid lines retain the normal stroke width.

Coordinate bar labels use the TypeScript sizing and placement formulas. The
bar thickness is set by the top-level `coordinateBarHeightRatio` template
setting. It defaults to `0.4`, or 40% of a grid cell, when omitted. Use
`0.5` for a half-cell bar. The map fit reserves room for two
bars on each axis using that same ratio. Row and column labels use 95% of that
thickness and 60% horizontal scaling. Distance numbers use 85% of the
thickness and 50% horizontal scaling regardless of digit count. Both use
Helvetica Bold, with lighter ink for distance numbers. Horizontal text uses
the original TypeScript baseline calculation.

Sheet badges use 18-point bold text with the TypeScript badge's baseline,
padding, and corner placement.

Regenerate PDFs made by older builds if Acrobat reports a page error. The
exporter leaves PDF page commands uncompressed and embeds the map as a JPEG
to avoid the incomplete compressed streams seen in those
exports.

### Image preprocessing

Both commands check image dimensions before decoding pixels and reject images
over 12,000,000 pixels or with multiple frames. The exporter preserves the
aspect ratio, rounds the scaled dimensions to whole pixels, and caps the
longest edge at 2,400 pixels without upscaling. It averages source pixels
after compositing transparency onto white and embeds a quality 50 JPEG.
The sheet layout uses the resulting JPEG dimensions, as in the TypeScript
exporter. Source image contents stay intact.

The JPEG quality setting is independent of the final PDF file size. SkiaSharp
handles decoding and encoding, so JPEG bytes can differ from TypeScript and
the CLI has no fixed decoder memory cap. The 12-megapixel input limit
bounds the decoded pixel buffer size, while total memory use also includes
the working image, decoder buffers, and PDF generation.
The image limit and large-image batch behavior have been checked locally.

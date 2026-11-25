# Portrait Layout Manager Headless Tool Suite

PLMH is a .NET 10 CLI tool in the `dnd-ts` umbrella repository. It accepts
portrait image paths and derives the token ID, name, creature size, and type
from each filename. It places the portrait and supplied metadata into a
printable nameplate layout and exports a PDF.

## PLMH

- **Tool folder** `tools/plmh-ts/`
- **Windows command** `bin\process_portraits.bat [command or image]`
- **macOS command** `sh bin/process_portraits.sh [command or image]`
- With no arguments or with `batch`, process images from `in_portraits/` and
  write one PDF per encounter to `out/`.
- Pass a portrait image path to create one PDF beside that image.
- Use `--dpi <value>` to set the target image resolution. The default is 100 DPI.
- Use `--template <name-or-path>` to select a template. `T000.jsonc` is the default.

Run commands from the umbrella workspace. Relative paths are resolved from
the current directory. Batch mode reads JPEG, PNG, BMP, and WebP files from
the top level of `in_portraits/`. PLMH prints its version at startup and a
status line for each image it processes. Source portraits remain in place.

### Command examples

The first two commands are equivalent. Use the filename of a portrait in
your workspace for the single-image commands. For the custom template example,
substitute the path to your template file.

Running the macOS launcher from a checkout requires the .NET 10 SDK on your
`PATH`. With a published copy, place the macOS `Plmh.Cli` executable or the
`Plmh.Cli.dll` and its companion files beside the script. A framework-dependent
publish requires the .NET 10 runtime.

```bat
bin\process_portraits.bat
bin\process_portraits.bat batch
bin\process_portraits.bat "in_portraits\e1_f1_mistborn dragon_medium_dragon.png"
bin\process_portraits.bat --dpi 300 "in_portraits\e1_f1_mistborn dragon_medium_dragon.png"
bin\process_portraits.bat batch --dpi 300
bin\process_portraits.bat --template "path\to\custom.jsonc" "in_portraits\e1_f1_mistborn dragon_medium_dragon.png"

bin\process_portraits.bat --help
bin\process_portraits.bat --version
bin\process_portraits.bat --templates
```

```sh
sh bin/process_portraits.sh
sh bin/process_portraits.sh batch
sh bin/process_portraits.sh "in_portraits/e1_f1_mistborn dragon_medium_dragon.png"
sh bin/process_portraits.sh --dpi 300 "in_portraits/e1_f1_mistborn dragon_medium_dragon.png"
sh bin/process_portraits.sh batch --dpi 300
sh bin/process_portraits.sh --template "path/to/custom.jsonc" "in_portraits/e1_f1_mistborn dragon_medium_dragon.png"

sh bin/process_portraits.sh --help
sh bin/process_portraits.sh --version
sh bin/process_portraits.sh --templates
```

`--help` lists commands and options. `--version` prints the version and UTC
build time. `--templates` lists bundled template filenames, names, paper sizes,
orientations, and the default image DPI. The DPI is shared by all templates
and can be overridden for an export with `--dpi`.

## Templates

Templates use schema version 1 and accept both JSON and JSON with comments.
Use a template name such as `T000` or pass a path to a custom `.json` or
`.jsonc` file.
`T000.jsonc` defines shared canvas dimensions
under `size`, a font family, background and frame image paths, portrait and text placement
rectangles, and paper size, orientation, margins, and placement. Both images
are resized to `size.width` and `size.height` when loaded. Region coordinates
and dimensions use those canvas pixels, measured from the top-left. Image
paths are resolved relative to the template file. To change the default font,
uncomment one `"font"` line in `T000.jsonc` and comment out the current one.
The default is `Libre Baskerville Bold`, which uses bold for all labels.
`Libre Baskerville` uses bold for the token ID and name and regular for the
size/type. Both options use gold (`#CEAF4E`) and embed the
[Libre Baskerville](https://github.com/impallari/Libre-Baskerville) faces in
exported PDFs. The SIL Open Font License is in
`src/Plmh.Core/Fonts/OFL-LibreBaskerville.txt`.

`Georgia Bold` uses the installed TrueType face on Windows or macOS. PLMH
embeds a subset in the exported PDF when the installed face permits it.

The bundled Cinzel
ExtraBold font is an 800-weight instance of [Cinzel from Google Fonts](https://github.com/google/fonts/tree/main/ofl/cinzel),
embedded in exported PDFs under the SIL Open Font License. Its license is in
`src/Plmh.Core/Fonts/OFL-Cinzel.txt`.

## Card layout and encounter lineup

The exporter creates Letter, Legal, or Ledger PDFs with portrait cards placed
in the configured row and column grid. The grid starts at the top-left page
margin; partial rows stay left-aligned and the first row starts at the top
margin. Cards preserve the frame canvas aspect ratio within each slot. It draws the
background, fits the portrait inside its region, draws the frame over the
portrait, then draws token ID, name, and size/type in their configured regions.
Text height follows each region's height and long text is horizontally
compressed to fit.

The default `T000` layout is Letter landscape with ¼″ margins and ¼″ gaps. Its
single row has three cards. Each card is 3⅓″ wide by 4⅔″ high at a 5:7 aspect
ratio. Batch mode groups portraits by the first filename field and writes one
PDF per encounter, such as `out/encounter_e1.pdf`; additional cards continue on
new pages in that encounter PDF.

The first page of each PDF shows the encounter lineup in portrait order. If
the template has a title band, PLMH shows the encounter ID by default.
`process_adventure` can supply the adventure and map name for that band. The
cutouts share a baseline and a common scale based on the heights in their
filenames. For a portrait without a height suffix, `defaultHeightFeet` supplies
the lineup height by creature size. The built-in defaults are 1, 3, 6, 12, 24,
and 48 feet for Tiny through Gargantuan, respectively. Unlisted custom sizes
default to 6 feet. Width follows the cropped cutout's aspect ratio. The `scale`
reference heights in inches apply to the portrait frames.

The lineup uses one foot between creatures, one foot of padding on the right,
and 1.5 feet at the top. The left padding starts at 1.5 feet, or 2 feet when
the tallest creature exceeds 10 feet, and grows to fit the height labels.
The template background fills the lineup and is
center-cropped to fit.

The bottom margin fits the tallest wrapped name, half a line of space, the
token badge row, and another half line of space. The optional `token_badge`
template field names an image relative to the template file. Every creature
currently uses the base badge size of 1.5 feet square.
The whole composition is drawn directly onto the PDF, centered and scaled
uniformly to fit within `marginInches`.

A height grid sits between the background and the cutouts. Its left axis starts
at the inner margin, and the cutouts share a baseline 0.33 scaled feet below
the grid's bottom axis. Horizontal
lines mark each whole foot up to the tallest creature. The axes and grid use
60% opacity, with 1.5-point strokes for regular lines, 3-point strokes at
every five feet, and 4.5-point strokes at every ten feet.
The vertical axis is labeled every foot when the tallest creature is under
10 feet and every five feet otherwise. Labels include the unit, as in `5 ft`.
Each creature's name is centered below its portrait and wraps at spaces to fit
within 75% of the portrait width. Long individual words are narrowed to fit.

## Image preprocessing

PDF exports default to 100 DPI for raster images, based on their printed
dimensions. Use `--dpi 300` for more image detail or `--dpi 0` to disable
downsampling. The option works in batch and single-image mode. Images already
below the target resolution are kept at their current resolution, and PDF text
stays vector-based.

Opaque backgrounds use high-quality JPEG with full chroma resolution.
Portraits, frames, badges, and backgrounds with transparency use lossless
compression. The exporter trims image pixels outside the visible clipping
area while retaining a small border for interpolation.

Before placing a portrait on a card or in the lineup, PLMH scans outward from
the image center for the first row or column on each side with no pixels at
alpha 2 or higher (out of 255). Those rows and columns define the crop.
Pixels inside the crop keep their original alpha. The crop applies only to
portraits.

## Text field layout

`Plmh.Core.TextFieldLayout.Fit` lays out one text field from a template region.
The region height determines the font size; the string is centered in both
dimensions. If its measured width exceeds the region width, it is horizontally
compressed to fit while retaining the height-derived font size. Region values
come from the background canvas's pixel coordinates, so the caller supplies
the image scale in points per pixel and its page origin. Field height and
baseline use built-in Helvetica metrics. Libre Baskerville labels use the
bundled face to measure their width.

## Portrait filenames

Use this underscore-delimited filename format:

```text
encounter_token_name_size_type.jpg
```

For example:

```text
e1_f1_mistborn dragon_medium_dragon.jpg
e1_f2_mistborn giant_medium(5-0)_dragon.jpg
```

The first example gives encounter ID `e1`, token ID `f1`, name `mistborn dragon`, size
`medium`, and creature type `dragon`. The second adds a 5-foot height to the
size field. Height is optional and accepts feet alone or `feet-inches`
notation, with inches from 0 to 11 (`medium(5)` equals `medium(5-0)`).
Underscores (`_`) are reserved as field
separators; spaces are allowed within the name. The parser reads the first two
fields as encounter and token IDs, the final two fields as size and creature
type, and joins the fields between them as the name. Standard D&D size
categories include `tiny`, `small`, `medium`, `large`, `huge`, and `gargantuan`.
Creature type examples include `aberration`, `beast`, `celestial`, `construct`,
`dragon`, `elemental`, `fey`, `fiend`, `giant`, `humanoid`, `monstrosity`,
`ooze`, `plant`, and `undead`. These are examples only; PLMH accepts other
nonempty size and type text, such as `chondathan male`.

For display, token IDs are uppercased (`f1` becomes `F1`). Names and the
combined size and creature type use title case (`mistborn dragon` becomes
`Mistborn Dragon`; `medium dragon` becomes `Medium Dragon`).

When height is provided, the exporter scales the cropped visible image by its
height in inches divided by the template's reference height for that creature
size. The default template uses 96 inches for Tiny, Small, and Medium; 192 for
Large; 384 for Huge; and 600 for Gargantuan. The height is also shown in the
size/type label. `scale.min_ht` sets the minimum relative scale; the default
template uses `0.4`. Without a height suffix, the portrait fits the configured
portrait region and the template scale map is not used.

`Plmh.Core.PortraitFileName.Parse(path)` parses this convention and returns the
full image path with encounter ID, token ID, name, size, and creature type.
Parsing applies the display capitalization rules to token ID, name, size, and
type without validating size or type against a fixed list. The filename
extension is not restricted by the parser.

## Project layout

- `src/Plmh.Cli/` contains the command-line application and bundled templates.
- `src/Plmh.Core/` contains the reusable layout and rendering library.
- `src/Plmh.Cli/Templates/T000.jsonc` is the default template.

## Status

Filename parsing, template loading, portrait composition, PDF export, encounter
grouping, template listing, and per-image status reporting are implemented. Batch mode writes one
PDF per encounter to `out/`; single-image mode writes one PDF beside the source.
Launchers are available for Windows and macOS.

## TODO

- Make the small-caps letter scale configurable in the template.

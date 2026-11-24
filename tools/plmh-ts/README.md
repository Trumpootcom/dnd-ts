# Portrait Layout Manager Headless Tool Suite

PLMH is a .NET 10 CLI tool in the `dnd-ts` umbrella repository. It accepts
portrait image paths and derives the token ID, name, creature size, and type
from each filename. It places the portrait and supplied metadata into a
printable nameplate layout and exports a PDF.

From the workspace root, run `bin\process_portraits.bat` to process image
files in `in_portraits/`, or pass one portrait path to process that image. The
launcher forwards arguments to the CLI and defaults to `batch` when no
arguments are provided. PLMH prints its revision at startup and one status line
per image, like DMSH. Batch PDFs go to `out/`; a single-file PDF is saved beside
its source image. Source portraits remain in place.

Examples:

```bat
bin\process_portraits.bat
bin\process_portraits.bat batch
bin\process_portraits.bat "in_portraits\e1_a2_Hadran Pell_medium_chondathan male.png"
bin\process_portraits.bat --template T000 "in_portraits\e1_a2_Hadran Pell_medium_chondathan male.png"
```

## Project layout

- `src/Plmh.Cli/` — command-line application and bundled templates.
- `src/Plmh.Core/` — reusable layout and rendering library for the CLI and
  potential future app.
- `src/Plmh.Cli/Templates/T000.json` — embedded default template.

## Template

Templates use schema version 1. `T000.json` defines shared canvas dimensions
under `size`, background and frame image paths, portrait and text placement
rectangles, and paper size, orientation, margins, and placement. Both images
are resized to `size.width` and `size.height` when loaded. Region coordinates
and dimensions use those canvas pixels, measured from the top-left. Image
paths are resolved relative to the template JSON file.

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

Before placement, PLMH crops fully transparent outer padding from portrait
images using the bounding box of pixels with nonzero alpha. Visible and
partially transparent edge pixels are preserved. Background and frame images
are not cropped.

## Text field layout

`Plmh.Core.TextFieldLayout.Fit` lays out one text field from a template region.
The region height determines the font size; the string is centered in both
dimensions. If its measured width exceeds the region width, it is horizontally
compressed to fit while retaining the height-derived font size. Region values
come from the background canvas's pixel coordinates, so the caller supplies
the image scale in points per pixel and its page origin. Text uses the same built-in
Helvetica metrics strategy as DMSH to avoid platform-dependent font measurement.

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

## Status

Filename parsing, template loading, portrait composition, PDF export, encounter
grouping, and per-image status reporting are implemented. Batch mode writes one
PDF per encounter to `out/`; single-image mode writes one PDF beside the source.

## TODO

- Make the small-caps letter scale configurable in the template.

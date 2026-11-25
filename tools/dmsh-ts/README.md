# Desktop Map Slicer Headless Tool Suite

DMSH turns tabletop battle map images into printable PDF sheets. It is one of
the tools in the `dnd-ts` umbrella repository. See [TODO.md](TODO.md) for
compatibility notes and remaining work.

## DMSH

- **Tool folder:** `tools/dmsh-ts/`
- **Windows command:** `bin\process_maps.bat [command or image]`
- **macOS command:** `sh bin/process_maps.sh [command or image]`
- **Description:**
  - With no arguments or with `batch`, process images from the workspace's
    `in_maps/`, write PDFs to `out/`, and leave source images in place.
  - Pass an image path, such as `map.png`, to create one PDF beside that image.
  - Use `--width <feet>` to override the map width. A filename ending in
    `WxH` or `WXH` sets the width from its first number and takes precedence
    over `--width`.
  - Use `--template <name-or-path>` to select a template; `T000.json` is the
    default.

Run commands from the umbrella workspace. Relative image paths are resolved
from the current directory. Batch mode reads the top level of `in_maps/`.
It continues after individual failures and returns exit code 1 if an image fails or is skipped
because of a conflict. Existing output PDFs are replaced after the new PDF is
written successfully.

### Command examples

The launchers default to batch mode when you provide no arguments. Pass a
command or image path to run it directly.

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

`--help` lists commands and options. `--version` prints the version and build
time. `--templates` lists the available templates and their paper and grid
settings.

The workspace's sample maps are in `samples/maps/`. Copy a sample to `in_maps/`
to batch-process it.

## Templates

Templates are JSON files in `src/Dmsh.Cli/Templates/` and use schema version 1.
The bundled `T000.json` is the default. The CLI also ships `T001.json` through
`T003.json`. Pass a template name such as `T001` (the `.json` extension is
optional), or pass a path to a custom JSON template. The `template` field is
its display name.

The tables below describe the JSON fields. Defaults are the values in
`T000.json`; `coordinateBarHeightRatio` defaults to `0.4` when omitted.

### General fields

<table>
<thead><tr><th>JSON field</th><th>T000 default</th><th>Description and allowed values</th></tr></thead>
<tbody>
<tr><td valign="top"><code>schemaVersion</code></td><td valign="top"><code>1</code></td><td valign="top">Template schema version. DMSH requires <code>1</code>.</td></tr>
<tr><td valign="top"><code>template</code></td><td valign="top"><code>Normal Ledger - Default</code></td><td valign="top">Display name shown by <code>--templates</code> and in export status.</td></tr>
<tr><td valign="top"><code>title</code></td><td valign="top"><code>TTRPG Battle Map</code></td><td valign="top">Title stored in the PDF document metadata.</td></tr>
<tr><td valign="top"><code>overview</code></td><td valign="top"><code>true</code></td><td valign="top">Include (<code>true</code>) or omit (<code>false</code>) the overview page.</td></tr>
<tr><td valign="top"><code>coordinates</code></td><td valign="top"><code>true</code></td><td valign="top">Request coordinate bars. They are drawn only for an unrotated, non-isometric rectangular grid.</td></tr>
<tr><td valign="top"><code>coordinateBarHeightRatio</code></td><td valign="top"><code>0.4</code></td><td valign="top">Positive number controlling bar thickness as a fraction of a grid cell; <code>0.5</code> makes a half-cell bar.</td></tr>
<tr><td valign="top"><code>worldWidthFeet</code></td><td valign="top"><code>160</code></td><td valign="top">Positive map width in feet used when fitting the image.</td></tr>
<tr><td valign="top"><code>imagePlacement</code></td><td valign="top"><code>center</code></td><td valign="top">Image placement. DMSH currently supports <code>center</code>.</td></tr>
</tbody>
</table>

### Grid fields

<table>
<thead><tr><th>JSON field</th><th>T000 default</th><th>Description and allowed values</th></tr></thead>
<tbody>
<tr><td valign="top"><code>grid.style</code></td><td valign="top"><code>rect</code></td><td valign="top"><code>rect</code> for rectangular, or <code>hex</code> for hexagonal. <code>square</code> and <code>iso</code> are also accepted as legacy rectangular styles.</td></tr>
<tr><td valign="top"><code>grid.iso</code></td><td valign="top"><code>false</code></td><td valign="top"><code>true</code> enables isometric projection; <code>false</code> uses regular cells.</td></tr>
<tr><td valign="top"><code>grid.isoAngleDegrees</code></td><td valign="top"><code>null</code></td><td valign="top">With <code>grid.iso: true</code>, use a number from <code>15</code> to <code>90</code>. With <code>grid.iso: false</code>, use <code>null</code>.</td></tr>
<tr><td valign="top"><code>grid.rotationDegrees</code></td><td valign="top"><code>0</code></td><td valign="top">Finite counterclockwise rotation in degrees.</td></tr>
<tr><td valign="top"><code>grid.offsetX</code></td><td valign="top"><code>0</code></td><td valign="top">Grid offset in cell units; use <code>-1</code> to <code>1</code>.</td></tr>
<tr><td valign="top"><code>grid.offsetY</code></td><td valign="top"><code>0</code></td><td valign="top">Grid offset in cell units; use <code>-1</code> to <code>1</code>.</td></tr>
<tr><td valign="top"><code>grid.inchesPerFiveFeet</code></td><td valign="top"><code>1</code></td><td valign="top">Printed inches for each 5-foot cell. Values must produce at least <code>0.25</code> PDF points of grid spacing and fit the selected page.</td></tr>
<tr><td valign="top"><code>grid.lineStyle</code></td><td valign="top"><code>solid</code></td><td valign="top">
<table><thead><tr><th>Style</th><th>Pattern</th></tr></thead><tbody>
<tr><td valign="top"><code>solid</code></td><td valign="top"><code>▕────────────────────────────────▏</code></td></tr>
<tr><td valign="top"><code>dash-1</code></td><td valign="top"><code>▕────────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;────────▏</code></td></tr>
<tr><td valign="top"><code>dash-2</code></td><td valign="top"><code>▕────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;────────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;────▏</code></td></tr>
<tr><td valign="top"><code>dash-3</code></td><td valign="top"><code>▕───&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;─────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;─────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;──▏</code></td></tr>
<tr><td valign="top"><code>dash-4</code></td><td valign="top"><code>▕──&nbsp;&nbsp;&nbsp;&nbsp;────&nbsp;&nbsp;&nbsp;&nbsp;────&nbsp;&nbsp;&nbsp;&nbsp;────&nbsp;&nbsp;&nbsp;&nbsp;──▏</code></td></tr>
<tr><td valign="top"><code>dash-5</code></td><td valign="top"><code>▕──&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;─▏</code></td></tr>
<tr><td valign="top"><code>dash-6</code></td><td valign="top"><code>▕──&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;──&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;&nbsp;──&nbsp;&nbsp;&nbsp;───&nbsp;&nbsp;─▏</code></td></tr>
<tr><td valign="top"><code>dash-8</code></td><td valign="top"><code>▕─&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;──&nbsp;&nbsp;─▏</code></td></tr>
<tr><td valign="top"><code>dash-10</code></td><td valign="top"><code>▕─&nbsp;&nbsp;─&nbsp;&nbsp;─&nbsp;&nbsp;─&nbsp;&nbsp;──&nbsp;&nbsp;─&nbsp;&nbsp;──&nbsp;&nbsp;─&nbsp;&nbsp;─&nbsp;&nbsp;─&nbsp;&nbsp;▏</code></td></tr>
<tr><td valign="top"><code>dash-16</code></td><td valign="top"><code>▕─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;─&nbsp;▏</code></td></tr>
<tr><td valign="top"><code>corners</code></td><td valign="top"><code>▕────&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;────▏</code></td></tr>
<tr><td valign="top"><code>none</code></td><td valign="top"><code>▕&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;▏</code></td></tr>
</tbody></table></td></tr>
<tr><td valign="top"><code>grid.color</code></td><td valign="top"><code>black</code></td><td valign="top">DMSH renders <code>black</code>, <code>gray</code>, and <code>white</code>. <code>diff</code> and <code>contrast</code> are not implemented in DMSH yet.</td></tr>
<tr><td valign="top"><code>grid.lineWidthPoints</code></td><td valign="top"><code>2.5</code></td><td valign="top">Grid stroke width in PDF points.</td></tr>
<tr><td valign="top"><code>grid.opacity</code></td><td valign="top"><code>0.6</code></td><td valign="top">Grid opacity from <code>0</code> (hidden) to <code>1</code> (opaque).</td></tr>
<tr><td valign="top"><code>grid.extent</code></td><td valign="top"><code>printableCanvas</code></td><td valign="top">DMSH supports <code>printableCanvas</code>.</td></tr>
</tbody>
</table>


### Page fields

<table>
<thead><tr><th>JSON field</th><th>T000 default</th><th>Description and allowed values</th></tr></thead>
<tbody>
<tr><td valign="top"><code>page.size</code></td><td valign="top"><code>ledger</code></td><td valign="top"><code>letter</code> or <code>ledger</code>.</td></tr>
<tr><td valign="top"><code>page.orientation</code></td><td valign="top"><code>landscape</code></td><td valign="top"><code>portrait</code> or <code>landscape</code>.</td></tr>
<tr><td valign="top"><code>page.rows</code></td><td valign="top"><code>2</code></td><td valign="top">Positive integer number of sheet rows.</td></tr>
<tr><td valign="top"><code>page.columns</code></td><td valign="top"><code>2</code></td><td valign="top">Positive integer number of sheet columns. Rows &#215; columns must not exceed 100 sheets.</td></tr>
<tr><td valign="top"><code>page.marginInches</code></td><td valign="top"><code>0.25</code></td><td valign="top">Nonnegative margin per edge in inches; it must leave a printable area.</td></tr>
<tr><td valign="top"><code>page.overlapInches</code></td><td valign="top"><code>0.125</code></td><td valign="top">Nonnegative overlap between sheets in inches; it must be less than the printable page dimensions.</td></tr>
</tbody>
</table>

Each filename passed to `--template` can omit the `.json` extension. For
example, `--template T002` selects `T002.json`. A path such as
`--template path/to/custom.json` selects a custom template. The command
`--templates` prints the installed filenames, names, paper and grid settings,
and usable world dimensions.

## Map fitting and cropping

The requested `worldWidthFeet` (or `--width`) sets the map's target scale for
the selected paper and grid spacing. DMSH accounts for page margins, sheet
overlap, and coordinate bars when calculating the printable canvas, then
scales the image without changing its aspect ratio.

The final map dimensions are rounded down to whole 5-foot cells on both axes.
DMSH tries a width-aligned fit and a height-aligned fit, then chooses the one
that retains more of the image. It center-crops the excess along the other
edge, which is usually the shorter edge. If a 5-foot cell cannot fit on both
axes, export fails. The source image and template are not changed.

A filename suffix such as `100x60` or `100X60` sets the width to 100 feet for
that image. This filename value takes precedence over `--width` and the
template's `worldWidthFeet`. For example, `E1 Farmhouse 100x60.jpg` uses a
100-foot world width.

Map overviews use the image filename stem as their heading. In adventure-file
mode, `process_adventure` supplies the adventure title, encounter ID, and
encounter name as the heading.

## Image preprocessing

DMSH accepts JPEG, PNG, BMP, and static WebP images. It rejects images over
12,000,000 pixels and animated or multi-frame images before decoding their
pixels.

For PDF output, DMSH preserves the source aspect ratio, averages source pixels
when resizing, composites transparency onto white, and limits the working
image's longest edge to 2,400 pixels without upscaling. It encodes that working
image as a quality-50 JPEG for embedding in the PDF. The original image file
remains unchanged.

SkiaSharp handles image decoding and encoding, so the generated JPEG bytes can
differ from the TypeScript exporter. Memory use includes the source pixels,
decoder buffers, working image, and PDF generation; the 12-megapixel input cap
bounds the source pixel buffer.

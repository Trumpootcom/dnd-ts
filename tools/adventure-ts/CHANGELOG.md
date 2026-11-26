# dnd-ts adventure changelog

## Unreleased

- Build encounter maps and portrait PDFs from an adventure JSON file by title
  or explicit path. Titles without an adventure file use the staged-input workflow.
- Reuse library locations and creatures across adventures. Location files define
  map widths, and creature files supply portrait metadata and optional heights.
- Generate numbered portrait cards from quantity groups and show each group once
  in the lineup with its original token label.
- Publish separate map and portrait PDFs or one merged PDF per encounter. Both
  overviews use the adventure title, encounter ID, and encounter name.
- Export an adventure and its referenced library files as a portable `.dndts`
  bundle. Use `--verbose` to show the exported file tree.
- Build PDFs directly from a bundle and optionally import its library resources,
  with prompts before replacing differing files.
- Open the result in the system's default PDF viewer when a build publishes
  exactly one PDF.

# dnd-ts adventure changelog

## 5.0.0

- Rename the library map reference key from `map` to `location` in encounter map entries.

## 4.2.0

- Reference reusable map JSON files that define an image and its width in feet.

## 4.1.1

- Display encounter portrait overview titles in small caps.

## 4.1.0

- Use the same adventure title, encounter ID, and encounter name on map and portrait overviews.

## 4.0.0

- Use the encounter name in separate and merged adventure-file PDF names.
- Publish one `<encounter ID> - <encounter name> - Adventure.pdf` per encounter in merged mode.

## 3.0.0

- Publish separate adventure-file PDFs as `ENC1 - <title> - Maps.pdf` and
  `ENC1 - <title> - Portraits.pdf`. Multiple maps in an encounter share one map PDF.

## 2.5.0

- Set a default portrait height in creature JSON and override it per encounter portrait entry.

## 2.4.1

- Label each `qty` group in the composite lineup with its original token ID.

## 2.4.0

- Show the first token of each `qty` group in the composite lineup and print every numbered portrait card.

## 2.3.0

- Use `qty` in an adventure portrait entry to create numbered copies with shared portrait metadata.
- Report duplicate token IDs when numbered copies overlap with other entries in an encounter.

## 2.2.1

- Apply the portrait template's default height by creature size to encounter portrait cards.

## 2.2.0

- Build encounter PDFs from an adventure JSON file by title or explicit path.
  Reuse library maps, portraits, and creature defaults without moving source images.
- Fall back to the staged-input workflow when a title has no matching adventure file.

## 2.1.0

- Title portrait lineups with the adventure name and matching map name.
- Recognize encounter IDs within map filenames for lineup titles and PDF merge order.

## 2.0.0

- Use the adventure date as the prefix for output and archive folders and
  remove the adventure number argument.

## 1.0.0

- Added the adventure batch workflow for generating map and portrait PDFs,
  optionally merging them, and archiving the source images after DM approval.
- Added Windows and macOS launchers and a suite version command that reports
  the versions of the map and portrait components.

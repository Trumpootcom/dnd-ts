# Project instructions

- Use C# and .NET 10. Use NuGet for dependencies.
- Treat the sibling `wmsh_ts` repository as a read-only reference.
- Build the CLI first. Keep rendering and template parsing in a C# library
  that the CLI and future Avalonia app can both use.
- Use `wmsh_ts/src/default.json` as the first compatibility reference.
  Preserve its JSON format and rendering behavior.
- Compare page count, paper size and orientation, sheet order, map placement,
  grid spacing, overlap, coordinate labels, and the overview page against
  the TypeScript output.
- Keep the CLI runnable on Windows and macOS.
- Ask before running any tests or smoke checks; do not run them without permission.
- Ask before pushing changes or publishing a release.

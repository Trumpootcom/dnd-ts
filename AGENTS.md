# Project instructions

## Tool versioning and changelogs

`dnd-ts` is a suite of independently versioned tools. Each tool owns its
version and changelog. A tool's Core and CLI projects share one version.

Apply the version bump and changelog rules to each tool after its version
cutover. At cutover, use the version calculated from a clean checkout with
full Git history at the agreed pushed baseline. Pin that version and open the
changelog at the same version. The agreed baselines are `1.0.4` for `plmh-ts`
and `1.0.24` for `dmsh-ts`.

For each request that changes a tool's shipped behavior, bump that tool's
version once and add a matching changelog entry. Bump every affected tool
when a shared change affects multiple tools. Skip version bumps for
read-only requests, documentation-only changes, and internal refactors
that preserve shipped behavior.

For .NET tools, declare the full version in the tool's
`Directory.Build.props`. The build embeds that version, and `--version`
reports it. Update any tracked NuGet lockfiles when dependency resolution
changes.

Use a patch bump for compatible fixes, a minor bump for compatible
features, and a major bump for breaking changes to a stable public
interface. If a request contains several changes, apply the highest
required bump once per affected tool.

Keep a `CHANGELOG.md` in each tool's directory. Add a `## <version>`
heading below the title and above older entries. Summarize user-facing
changes since the previous version. Use these titles for the existing tools:

- `# plmh-ts change log - Portrait Layout Manager (Headless) Tool Suite`
- `# dmsh-ts changelog - Desktop Map Slicer (Headless) Tool Suite`

If the suite is released as a bundle, record the exact component versions
in its release manifest. Any suite-level version command should report
the versions of the components it actually ships.

## Changes and new features

Update the affected tool's README and CLI help when a change affects
commands, options, defaults, configuration, or expected behavior.
Update the root `README.md` in the same change whenever it affects suite-wide
usage, which tools the suite includes, or any information summarized in the
affected tool's entry. Check launcher commands, single-image and batch
behavior, input and output locations, template locations and defaults, and
command examples against the tool's README and CLI help.

Keep the root README in two parts. The suite reference starts at
`<!-- Suite reference starts here. See AGENTS.md for editing rules. -->`,
immediately above `## Folder structure`. Use `## Folder structure` as the
boundary if the marker is absent.

- Above the boundary, preserve the `# D&D Tools` overview and document
  suite-wide workflows such as `process_adventure`.
- Below the boundary, preserve `## Folder structure` followed by `## Tools`.
  Update the reference only where a change affects the directory layout,
  tool list, or documented tool usage. Keep existing entries and examples
  unless the change makes them inaccurate.

Under `## Tools`,
list each tool with a link to its full README, then give its folder, full
name, Windows command, macOS command, a brief description of single-image
and batch behavior and templates, and command examples showing supported
switches. End each example block with `--help`, `--version`, and
`--templates` in that order where supported. Call out unavailable launchers
or switches.

Preserve the `Status` and `TODO` sections in the `plmh-ts` README and the
separate `tools/dmsh-ts/TODO.md` when updating documentation. Update those
entries when the requested work changes their status.

When adding, changing, or removing a configuration setting, update its
documentation and any existing config or status command. Show the
effective value and document the default or unset behavior. Call out
new runtime prerequisites or permissions in the change summary.

Before implementing a change, check whether the existing structure can
support it cleanly. Make focused cleanup when it helps implement the
request. If an architectural change would materially expand the scope,
explain the tradeoff and ask before taking it on.

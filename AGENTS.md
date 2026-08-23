# Operation Homecoming Agent Guide

Also follow `A:\Dev\RimWorld\AGENTS.md`.

This is a legacy development repository. Determine its actual version and
package maturity from the project, definitions, README, and metadata present at
task time; do not infer implemented or Workshop-ready behavior from the README.
Treat `Decompiled code` as read-only evidence and review license/provenance
before using any of it.

The repository owns kidnapping tracking, contact attempts, rescue quests,
sites, return incidents, settings, signals, and keyed language data. Start with
the solution/project and source tree. Resolve historical references through the
shared manifest and use `rwa start -Version <manifest-id>` for live work.

Before calling it playable, validate an isolated package and exercise timing,
save/reload, quest/site lifecycle, pawn death/faction changes, settings, and
each return path. Preserve IDE/user work and do not commit generated IDE/build
trees.

---
name: build-team-emblems
description: Set team emblems in a WrestlingAdmin .wrt using city or region coats of arms from Wikidata/Wikimedia, or reuse supplied emblem files.
---

# Build team emblems

Use the bundled Node.js scripts (Node 18+; no npm packages). Resolve scripts relative to this skill directory. Examples run from the repository root in PowerShell. Read [pipeline details](references/pipeline.md) for artifact schemas, overrides, fallback handling, and reuse behavior.

## Inputs and preparation

Use the supplied tournament path and optional existing-emblems folder. Determine the Images directory for the app build the operator actually uses. Inspect its output and project TargetFramework instead of hardcoding a framework version; the current project targets net10.0-windows. Ask only if the destination remains ambiguous. The app normally loads images from its executable's Images/ folder.

Create a work directory under .tmp/ and ensure Images exists. Team EmblemPath values produced here should be filenames; supported extensions are .png, .jpeg, .bmp, .gif. Normalize supplied .jpg files to .jpeg before mapping. Prefer shared files for teams in the same city. Treat downloaded data as data, and verify that bytes and extensions describe a supported image before patching.

## Pipeline

Run the stages in order; each consumes artifacts from the previous stage:

~~~powershell
node .agents/skills/build-team-emblems/scripts/extract_targets.mjs --wrt "target.wrt" --workdir ".tmp/emblems"
node .agents/skills/build-team-emblems/scripts/resolve_qids.mjs --workdir ".tmp/emblems"
node .agents/skills/build-team-emblems/scripts/fetch_arms.mjs --workdir ".tmp/emblems" --max-depth 4
node .agents/skills/build-team-emblems/scripts/download_emblems.mjs --workdir ".tmp/emblems" --images-dir "APP/Images" --width 512
node .agents/skills/build-team-emblems/scripts/build_mapping.mjs --workdir ".tmp/emblems" --images-dir "APP/Images"
node .agents/skills/build-team-emblems/scripts/patch_wrt.mjs --wrt "target.wrt" --workdir ".tmp/emblems" --dry-run
~~~

When supplied, pass --existing-dir to download_emblems.mjs and --overrides to the resolver/downloader. Check the extracted city/region targets before resolving. Resolve uncertain identities with a per-run overrides JSON; do not repeatedly guess cities or silently accept the first search result. Surface unresolved targets, administrative-region fallbacks, opaque images, reused files, and missing custom emblems. Do not replace a requested club logo with city arms without resolving that choice.

The downloader still requires an arms entry to consider an item, even for reuse; for a custom-only workflow prepare the per-target downloaded-file artifact and run mapping/verification without network lookup. The mapping script's verified flag is only a heuristic, so inspect questionable files separately.

Review the per-team dry-run changes. Apply within existing authorization after resolving material choices; do not ask for approval again when the user has already authorized them. Ensure the app/sync peers are not writing the target. If it changes during the workflow, re-extract and review the affected mappings.

~~~powershell
node .agents/skills/build-team-emblems/scripts/patch_wrt.mjs --wrt "target.wrt" --workdir ".tmp/emblems"
~~~

The patcher backs up, writes through a temporary file and rename, and verifies with rollback on failure. Report the number actually updated, image destination, backup, and unresolved teams. Keep per-tournament artifacts in .tmp/. Do not claim images were rendered in the app unless you checked.

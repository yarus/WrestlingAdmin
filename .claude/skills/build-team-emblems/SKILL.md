---
name: build-team-emblems
description: Populate EmblemPath for every TeamApplication in a `.wrt` tournament file by fetching coat-of-arms images of each team's city/region from Wikidata + Wikimedia Commons. Resolves cities through Wikidata search (with overrides for ambiguous names), pulls Wikidata P94 with P131-fallback to district/region, and downloads PNG renders (alpha-preserving, 512px width) into the WPF app's `Images/` folder. Optional `--existing-dir` reuses already-prepared emblem files instead of re-downloading. Backs up the .wrt and uses atomic write + post-write verify. Use when the user asks to: "загрузить гербы команд", "добавить эмблемы городов", "обновить EmblemPath для команд из .wrt", or supplies a tournament file and a list/folder of emblems.
---

# build-team-emblems

Workflow to populate every team's coat of arms in a `.wrt` tournament file.

## When to use

The user asks something like:
- "загрузи гербы для команд в этом турнире"
- "найди эмблемы городов для команд из .wrt"
- "обнови EmblemPath у всех команд"
- supplies a `.wrt` path and (optionally) a folder of pre-prepared emblems

## Inputs you need before running

Ask the user (AskUserQuestion) only if NOT already supplied in the conversation:

1. **Target `.wrt` path** — absolute path to the tournament file
2. **Images dir** — usually `<repo>/Wrestling.UI.Material/bin/Debug/net9.0-windows/Images/` (the WPF app reads emblems from `{AppDomain.BaseDirectory}\Images\`). Confirm with the user if unsure
3. **Existing emblems dir (optional)** — folder of prepared image files keyed by slug (e.g., `saint-petersburg.png`, `gatchina.png`). If provided, files there are reused as-is instead of re-downloading
4. **Workdir** — where intermediate JSON artifacts go. Default: `<repo>/tmp/emblems_<YYYYMMDD_HHmm>/`

## Schema and constraints (do not violate)

- `TeamApplication.EmblemPath` (Wrestling.Entities/TeamApplication.cs:136) — relative filename, no path
- Supported formats by the WPF converter (`Wrestling.UI.Material/Utils/Converters/PathToImageConverter.cs:42-45`): **`.png .jpeg .bmp .gif`** (no WebP)
- Only PNG and GIF support transparency. Wikimedia's `Special:FilePath?width=N` thumbnailer renders SVG → PNG with alpha; GIF sources are returned as-is and we keep them under `.gif` extension
- Multiple teams from the same city should share the **same file** (one `saint-petersburg.png` referenced by all 15 SPb teams) — saves disk and simplifies updates
- The `.wrt` file is single-line JSON; the patch script must `JSON.stringify` without pretty-printing

## Steps

Run scripts in order. Each writes a JSON artifact in `<workdir>/`. All scripts accept `--workdir` so artifacts are isolated per tournament.

### 1. Extract unique targets

```bash
node .claude/skills/build-team-emblems/scripts/extract_targets.mjs \
  --wrt <path-to.wrt> \
  --workdir <workdir>
```

Reads the .wrt, walks the JSON graph, finds every `TeamApplication`, and groups them by **(primary, region, country)** after normalizing raw `City` strings:
- Strips prefixes `пос. им. `, `пгт. `, `с. `, `г. `, `д. `
- Splits suffix region (`, Ленинградская обл.`, `, Республика Тыва`, …) into a separate `region` field
- Foreign teams (`Country !== "Россия"`) collapse to country-level

Output: `emblem_targets.json` with `{ targets[], perTeam[] }`. Show the user the per-target table (count × primary [region]) before continuing.

### 2. Resolve Wikidata QIDs

```bash
node .claude/skills/build-team-emblems/scripts/resolve_qids.mjs \
  --workdir <workdir> \
  [--overrides <user-overrides.json>]
```

Calls Wikidata `wbsearchentities` (Russian language) for each target, picks the top result whose description matches a settlement/territory pattern. Built-in QID overrides for known-tricky cases live in `data/known_overrides.json` — extend that file when you discover new failures.

If the script exits with code 1, it lists unresolved targets. Add overrides to a JSON like:
```json
{ "qids": { "<primary>|<region>": "Q123456" } }
```
and re-run with `--overrides`.

### 3. Fetch coat-of-arms filenames

```bash
node .claude/skills/build-team-emblems/scripts/fetch_arms.mjs \
  --workdir <workdir> \
  [--max-depth 4]
```

For each QID:
1. Try Wikidata claim **P94** (coat of arms image)
2. If empty, follow **P131** (located in administrative territorial entity) up to one level and retry — repeats up to `--max-depth` times
3. Records the fallback chain in `walk[]` for transparency

Output `emblem_arms.json` shows which targets needed fallback. Surface those to the user (`[fallback: <region>]`) so they know that Тыва kozhuun arms were used instead of a small village.

### 4. Download / reuse emblems

```bash
node .claude/skills/build-team-emblems/scripts/download_emblems.mjs \
  --workdir <workdir> \
  --images-dir <Images-folder> \
  [--existing-dir <pre-prepared-dir>] \
  [--width 512] \
  [--overrides <user-overrides.json>]
```

For each target with an arms filename, in order:
1. **Existing dir** — if `<slug>.{png,gif,jpeg,jpg,bmp}` exists in `--existing-dir`, copy it to `--images-dir` as-is. Use this when the user has hand-curated emblems.
2. **Target dir** — if `<slug>.{ext}` already exists in `--images-dir`, leave it alone (idempotent reruns).
3. **Wikimedia download** — `https://commons.wikimedia.org/wiki/Special:FilePath/<file>?width=512`. The thumbnailer renders SVG → PNG (alpha preserved). GIF and JPG sources come back unchanged; the script detects the magic bytes and renames the file to its true extension.

Slug naming: kebab-case transliteration of `<primary>[-<region>]`. Built-in overrides for common English-style names (`saint-petersburg`, `moscow`, `serbia`, `bulgaria`) live in `data/known_overrides.json` `slugs`. Extend via `--overrides`.

### 5. Verify and build mapping

```bash
node .claude/skills/build-team-emblems/scripts/build_mapping.mjs \
  --workdir <workdir> \
  --images-dir <Images-folder>
```

Re-reads each downloaded file, checks magic bytes, detects alpha (PNG `colorType=4|6` or `tRNS`; GIF transparent flag). Builds `(cityRaw, countryRaw) → file` mapping ready for the patcher.

The `α` column flags whether transparency is present. Files without α come from PNG sources where the Wikidata image had no alpha — surface them to the user as a soft warning (background may be white on the projector).

### 6. Patch the .wrt

```bash
# Dry run first (recommended)
node .claude/skills/build-team-emblems/scripts/patch_wrt.mjs \
  --wrt <path-to.wrt> --workdir <workdir> --dry-run

# Apply
node .claude/skills/build-team-emblems/scripts/patch_wrt.mjs \
  --wrt <path-to.wrt> --workdir <workdir>
```

Three defense layers (mirrors `TournamentDataAccess.SaveToFile`):
1. **Pre-save backup** to `<wrt-dir>/Backups/<yyyyMMddHHmmssfff>_emblem_patch.wrt`
2. **Atomic write**: serialize → `<wrt>.tmp.<rand>` → `rename`
3. **Post-write verify**: re-parse, confirm every expected `EmblemPath` landed; restore backup on failure

Skips writing entirely if no team needs an update (idempotent).

## Reuse behaviour summary (`--existing-dir`)

Pass `--existing-dir <dir>` when the user has hand-prepared emblems for some teams (e.g., a custom logo from the local club, not from Wikimedia). Files in that directory must be named `<slug>.{ext}` matching the same slug rules the script uses. Run with `--dry-run` once first to see which slugs would be picked up.

If nothing in `--existing-dir` matches (slug differs from what the script expects), nothing breaks — the script simply downloads from Wikimedia for those targets. The user can then either rename their files to the canonical slug or override slugs via `--overrides`.

## Workflow recap to present to the user

After step 5 (before patching), show:
- **Per-target table**: city/region → arms file (downloaded vs reused) + size + alpha flag + fallback flag
- **Per-team plan**: every team gets which emblem file
- **Soft warnings**: fallback hits, no-alpha files, GIF substitutes, unresolved targets
- Then **AskUserQuestion** to confirm before running step 6 without `--dry-run`

## Output artifacts

After a successful run:
- `<workdir>/emblem_targets.json` — normalized targets
- `<workdir>/emblem_qids.json` — Wikidata resolution
- `<workdir>/emblem_arms.json` — coat-of-arms filename + fallback walk
- `<workdir>/emblem_downloaded.json` — per-target download/reuse outcome
- `<workdir>/emblem_verified.json` — file format + alpha verification
- `<workdir>/emblem_mapping.json` — final `(cityRaw|country) → file`
- `<workdir>/emblem_patch_report.json` — list of `.wrt` updates + backup path
- `<images-dir>/<slug>.{png,gif,…}` — emblem files
- `<wrt-dir>/Backups/<timestamp>_emblem_patch.wrt` — pre-save backup
- Updated `.wrt`

These artifacts are throwaway tooling output — keep `<workdir>/` outside any committed source folders (default `<repo>/tmp/…`).

## Updating known overrides

When you discover that a city resolves to the wrong QID, add it to `data/known_overrides.json` under `qids` (key `<primary>|<region>`). Same for non-default slugs (`slugs`). This way subsequent runs benefit without needing per-tournament `--overrides` files.

## Network and rate-limiting

Wikidata + Wikimedia are queried directly without authentication. Scripts pace themselves with 150–300 ms sleeps between requests and identify with a polite `User-Agent`. For ~40 unique targets a full run takes ~30–60 seconds.

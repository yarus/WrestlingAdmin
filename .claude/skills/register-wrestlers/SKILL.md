---
name: register-wrestlers
description: Import wrestler registrations from a Google Sheets link (or a local directory of CSVs) into a `.wrt` tournament file. Cross-references new rows against the existing tournament, detects unique teams among duplicate/misspelled variants by (coach surname + city), classifies new wrestlers into groups, reports a summary, and applies changes atomically with a pre-save backup. Use when the user asks to load/update/sync registrations, import participants from a form, or add wrestlers from a spreadsheet into a `.wrt`.
---

# register-wrestlers

Workflow to merge external registration data into a WrestlingAdmin `.wrt` tournament file.

## When to use

The user asks something like:
- "загрузить регистрацию из таблицы в .wrt"
- "обнови список участников из Google таблицы"
- "добавь новых борцов из CSV"
- provides a Google Sheets URL + a `.wrt` path and wants them reconciled

## Inputs you need before running

Ask the user (AskUserQuestion) only if NOT already supplied in the conversation:

1. **Source** — either:
   - a Google Sheets share URL (the spreadsheet must be viewable without auth), OR
   - a local directory containing `*.csv` files already exported
2. **Target `.wrt` path** — absolute path to the tournament file
3. **Tab filter** (Google Sheets only) — list of tab names to include OR a pattern to skip (default skip: tab name contains "БЕЗ ОПЛАТЫ")

## CSV column convention expected

Tab export columns (0-indexed):
```
0: FullName   "LastName FirstName MiddleName"
1: BirthDate  "DD.MM.YYYY"
2: Weight     numeric, kg
3: Rank       free text (I юн / II юн / Кмс / б/р / ...)
4: City
5: Team       free-text club name (coach-entered; may be transliterated)
6: Coach      "LastName FirstName MiddleName" or abbreviated
7: Timestamp
8: FormName
9: Stage
```

Header row is discarded. If the target spreadsheet uses a different layout, adjust `build_plan.mjs`'s row destructuring block (look for `const [name, bd, w, rank, city, team, coach]`).

## Steps

1. **Fetch** (Google Sheets only):
   ```
   node scripts/fetch_sheets.mjs \
     --id <SPREADSHEET_ID> \
     --tabs "<Tab1>,<Tab2>,..." \
     --out-dir <workdir>/sheets
   ```
   Spreadsheet ID is the long token between `/d/` and `/edit` in the URL. Tab names are the visible tab titles (with Cyrillic / spaces preserved — the script URL-encodes them). If the user didn't list tabs, tell them you cannot enumerate tabs automatically (the gviz API requires explicit sheet names) and ask for the list.

2. **Build plan** — cross-reference + team dedup:
   ```
   node scripts/build_plan.mjs \
     --source-dir <workdir>/sheets \
     --wrt <target.wrt> \
     --out <workdir>/update_plan.json
   ```
   Writes `update_plan.json` with: `alreadyRegistered`, `newWrestlers`, `newTeams` (with `sourceVariants` listing every distinct spelling collapsed into that cluster), `skipped`, `overweight`, `totals`.

3. **Present the summary** using the plan totals and the following tables (do NOT dump raw JSON):
   - Already registered / New to add / New teams to create / Skipped / Needs manual group
   - List of new teams: ShortName, FullName, City, Coach, wrestler count, variant spellings
   - Per-group additions
   - **`needsManualGroup[]` list** — name, year, weight, reason, heaviest weight in age range — so the user can decide whether to correct the weight or add a group
   - Any rows that need human attention (unresolved birthdate, etc.)

4. **Ask for confirmation** with AskUserQuestion. If the user wants edits (different team names, manual group override for an edge case, etc.), modify `update_plan.json` directly before applying.

5. **Apply on approval**:
   ```
   node scripts/apply_plan.mjs --wrt <target.wrt> --plan <workdir>/update_plan.json
   ```
   The script writes a timestamped backup into `<wrt-dir>/Backups/<filename>/`, performs an atomic write (`.tmp` → `rename`), verifies by re-parsing, and restores the backup on any verification failure — same three defenses as `TournamentDataAccess.SaveToFile`.

## Team deduplication rule (important)

A single real club shows up in registration forms under many spellings: "ГШБ" / "Гатчинская школа борьбы" / "Гатч.Школа Борьбы", "DavudovTeam" / "Davudov Team" / "Давудов Тима", etc. The stable fingerprint is **coach surname + normalized city**, not the free-text team name. `build_plan.mjs` uses this in the following order:

1. Exact triple match `(rawTeam, rawCity, rawCoach)` against prior/in-wrt assignments
2. Duo match `(rawTeam, rawCity)`
3. **Coach-city match `(coachSurname, normCity)`** — the workhorse; tolerates team-name transliteration and coach-patronym variations ("Хандохов Азамат Альбертович" vs "Хандохов А.А.")
4. Normalized FullName / ShortName against existing teams
5. Substring tolerance with same city

Rows that don't match any existing team are clustered by the same coach-city key, producing `newTeams` where `sourceVariants[]` lists every distinct team-name spelling that folded into the cluster. For each cluster:
- `fullName` = longest variant
- `shortName` = shortest variant (truncated to 12 chars, collision-suffixed with a 2-char city prefix if needed)

## Group assignment

For each new wrestler:
1. Filter groups where `IsFemale === false` AND birth year falls in `[BirthYearMin, BirthYearMax]`
2. Among those, pick smallest `WeightMax` with `WeightMax >= weight` — the nearest-by-weight group the wrestler actually fits into
3. If no group fits (age not covered, or weight exceeds every group in the age range), **do not** auto-assign — add the wrestler to `plan.needsManualGroup[]` with the reason (`Overweight` / `NoAgeMatch` / `BadInput`) and the observed `maxWeightInAgeRange`. These rows are reported to the user and are NOT written to the .wrt. The user then either:
   - fixes the wrestler's weight in the source sheet / plan file, then re-runs `build_plan.mjs`, or
   - adds a suitable group to the `.wrt` (via the app UI or direct edit) and re-runs

## Invariants enforced on every run

These are hard constraints from the `.wrt` schema and printing/UI layer. `build_plan.mjs` generates values that respect them, and `apply_plan.mjs` re-validates before write and after write — any violation aborts with the backup restored.

- **`TeamApplication.ShortName` is unique and ≤ 12 characters.** `build_plan.mjs` chooses the shortest team-name variant, truncates to 12, and, on collision (with existing teams or with other new teams in the same plan), appends a 2-char Cyrillic city initialism; still colliding, appends a numeric suffix. `apply_plan.mjs` rejects any ShortName that collides or exceeds 12 chars — both pre-flight and post-write.
- **`HashTag` is always `null` on created entities** (teams and wrestlers). Plan values for `HashTag`, if present, are ignored at write time. Matches the observed convention in current `.wrt` files (`Teams with non-empty HashTag: 0`).
- **Every `TeamApplication.ID` and every `Wrestler.ID` is globally unique within the tournament.** `build_plan.mjs` checks new team IDs against existing IDs (regenerates on collision). `apply_plan.mjs` re-checks the full set (existing + plan) pre-flight and fails loudly; after write it confirms `Set(IDs).size === array.length` for both wrestlers and teams.

## Error handling

- Pre-flight: refuse to run `apply_plan.mjs` if `newTeams` / `newWrestlers` are empty (nothing to do)
- Pre-flight: abort (without writing) if any of the invariants above are violated
- During apply: verify via `JSON.parse` after write; restore backup on parse or integrity failure and exit non-zero
- Integrity check after apply: all `Wrestler.TeamID` and `Wrestler.GroupID` must resolve; all IDs unique; `sum(Group.Wrestlers[].length) === Wrestlers.length`; ShortNames unique and ≤ 12 chars

## Schema touchpoints

The scripts write wrestlers/teams matching the current `.wrt` schema (see `CLAUDE.md` → "Persistence is file-based"). If `EntityToInfoAdapter` gains a new required field, update `apply_plan.mjs`'s wrestler/team object literals in lockstep — missing fields load fine (Newtonsoft defaults), but new required-non-null fields will break at Adapter time.

## Output artifacts

After a successful run:
- `<workdir>/sheets/*.csv` — raw downloaded tabs
- `<workdir>/update_plan.json` — audit trail of the merge
- `<wrt-dir>/Backups/<filename>/<timestamp>.wrt` — pre-save backup
- Updated `.wrt` file

Keep `update_plan.json` in the repo's `.gitignore` territory (it's generated data); don't commit it.

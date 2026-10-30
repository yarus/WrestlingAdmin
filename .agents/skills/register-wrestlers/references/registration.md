# Registration formats and rules

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

## Team deduplication rule (important)

A single real club shows up in registration forms under many spellings: "ГШБ" / "Гатчинская школа борьбы" / "Гатч.Школа Борьбы", "DavudovTeam" / "Davudov Team" / "Давудов Тима", etc. The stable fingerprint is **coach surname + normalized city**, not the free-text team name. `build_plan.mjs` uses this in the following order:

1. Normalized FullName against existing teams.
2. Normalized ShortName against existing teams.
3. Coach-city match `(coachSurname, normCity)`.
4. Substring tolerance with the same city.

The name matches precede coach/city matching and do not require the same city. Review ambiguous names and coach/city clusters rather than assuming these heuristics prove identity.

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

- Pre-flight: exit successfully without writing `apply_plan.mjs` if both `newTeams` and `newWrestlers` are empty (nothing to do)
- Pre-flight: abort (without writing) if any of the invariants above are violated
- During apply: verify via `JSON.parse` after write; restore backup on parse or integrity failure and exit non-zero
- The apply script checks that `Wrestler.TeamID` and `Wrestler.GroupID` resolve, group references point to existing wrestlers, IDs are unique within each table, and ShortNames are unique and ≤ 12 characters. Separately check that each wrestler appears exactly once in its assigned group's list; the script does not enforce that full correspondence.

## Schema touchpoints

The scripts write wrestlers/teams matching the current `.wrt` schema (see `AGENTS.md` → "Persistence is file-based"). If `EntityToInfoAdapter` gains a new required field, update `apply_plan.mjs`'s wrestler/team object literals in lockstep — missing fields load fine (Newtonsoft defaults), but new required-non-null fields will break at Adapter time.

## Output artifacts

After a successful run:
- `<workdir>/sheets/*.csv` — raw downloaded tabs
- `<workdir>/update_plan.json` — audit trail of the merge
- `<wrt-dir>/Backups/<filename>/<timestamp>.wrt` — pre-save backup
- Updated `.wrt` file

Keep `update_plan.json` in the repo's `.gitignore` territory (it's generated data); don't commit it.

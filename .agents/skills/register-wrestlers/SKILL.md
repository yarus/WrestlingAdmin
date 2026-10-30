---
name: register-wrestlers
description: Import registrations from Google Sheets or CSV files into a WrestlingAdmin .wrt tournament, reconciling teams and assigning eligible groups.
---

# Register wrestlers

Merge registrations into an existing tournament using the bundled Node.js scripts (Node 18+; no npm packages). Resolve scripts relative to this skill directory. Run commands from the repository root; quote paths containing spaces. Examples below use PowerShell and repository-relative paths.

## Inputs and preparation

Use the source and target paths already supplied. Ask only for missing essentials: the target .wrt and either a local CSV directory or a publicly readable Google Sheets URL with explicit tab names. The fetcher cannot enumerate tabs. By default exclude tabs containing "БЕЗ ОПЛАТЫ"; confirm ambiguous inclusion choices.

Create an isolated work directory under .tmp/ and its sheets/ subdirectory. Read [registration rules](references/registration.md) before interpreting CSVs or editing a plan; they describe the fixed column layout, male-only group assignment, coach/city deduplication, and schema invariants. Do not infer gender, payment status, or ambiguous identities beyond the supported source convention; surface mismatches before applying.

## Workflow

1. For Google Sheets, extract the spreadsheet ID from /d/ in its URL and fetch only the requested tabs:

   ~~~powershell
   node .agents/skills/register-wrestlers/scripts/fetch_sheets.mjs --id "SPREADSHEET_ID" --tabs "Tab1,Tab2" --out-dir ".tmp/registration/sheets"
   ~~~

   For local CSVs, use their directory as --source-dir. Stop and report failed tab fetches; do not silently import a partial registration set.

2. Build the reviewable plan:

   ~~~powershell
   node .agents/skills/register-wrestlers/scripts/build_plan.mjs --source-dir ".tmp/registration/sheets" --wrt "target.wrt" --out ".tmp/registration/update_plan.json"
   ~~~

3. Report totals, team spelling clusters, per-group additions, skipped rows, and needsManualGroup entries. Review uncertain team matches and existing identity collisions. Edit the plan for requested corrections, or fix the input and rebuild. Rows without a valid group remain excluded.

4. Apply when the user has authorized the import and material ambiguities are resolved. Reuse authorization already given; ask only for unresolved decisions or changes outside it. Ensure the app and sync peers will not overwrite this file during the operation. If the target changes after planning, rebuild and review the affected changes.

   ~~~powershell
   node .agents/skills/register-wrestlers/scripts/apply_plan.mjs --wrt "target.wrt" --plan ".tmp/registration/update_plan.json"
   ~~~

The apply script backs up the target, writes via a temporary file and rename, then verifies references and uniqueness with rollback on verification failure. Empty plans exit successfully without changes. On failure, inspect the error and backup before retrying; do not blindly reapply an import plan.

Report actual added counts, excluded rows, the updated file, and backup path. Keep CSVs and plans in .tmp/; they contain participant data and must not be committed.

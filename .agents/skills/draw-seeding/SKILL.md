---
name: draw-seeding
description: Seed WrestlingAdmin .wrt groups using an external CSV rating or past results. Use for rating-based draws; ordinary club/city seeding is built into the app.
---

# Seed a tournament from ratings

Use the bundled Node.js scripts (Node 18+; no npm packages). Resolve scripts relative to this skill directory. Examples run from the repository root in PowerShell.

For ordinary seeding without external ratings, direct the user to the app's Draw page and "Пересоздать все сетки". Do not substitute this external-rating workflow.

## Inputs and constraints

Use the supplied .wrt, rating CSV, optional group subset, and explicit position locks. Ask only for missing essential inputs or ambiguous rating semantics. Read [seeding rules](references/seeding.md) before planning: it covers supported rating formats, ranking direction, bracket limitations, and IsSeedFixed behavior.

The script reassigns all positions in each selected group and sets IsSeedFixed=true. Existing fixed flags are not locks; carry forward positions the user wants preserved with --lock. Apply resets selected brackets by default, requiring regeneration in the app. Explain this effect before applying. Completed groups are skipped unless the user explicitly authorizes discarding their results; only then pass those group IDs through --force. Never force merely to make the command succeed.

## Workflow

Create a work directory under .tmp/. Build a plan without changing the tournament:

~~~powershell
node .agents/skills/draw-seeding/scripts/plan.mjs --wrt "target.wrt" --rating "rating.csv" --random-seed 42 --out ".tmp/seeding/seeding_plan.json"
~~~

Optional arguments: --groups "groupId,groupId", --lock "wrestlerId:seed,...", --force "groupId,...". The random seed makes runs reproducible; it is not an iteration count.

Review assignments, matched and unmatched ratings, club/city conflicts, skipped groups, and any forced resets. Explain unavoidable compromises. Correct inputs or edit assignments and review again when requested. Do not apply to an unsupported bracket configuration; inspect the current processor when the reference differs from application code.

Apply within the user's existing authorization once material decisions are resolved. Ask only for unresolved choices or destructive resets not already authorized. Ensure the target is not being written by the app or sync peers; if it changed after planning, rebuild the plan.

~~~powershell
node .agents/skills/draw-seeding/scripts/apply.mjs --wrt "target.wrt" --plan ".tmp/seeding/seeding_plan.json"
~~~

The script validates group membership and contiguous unique seeds, backs up, writes through a temporary file and rename, and verifies with rollback on failure. Report the actual processed/skipped groups and backup path. Tell the user to reopen the file and regenerate the selected brackets in the app. Do not claim to have verified the UI unless you did so. Keep plans in .tmp/.

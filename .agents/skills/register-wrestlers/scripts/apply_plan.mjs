#!/usr/bin/env node
// Apply an update plan (produced by build_plan.mjs) to a .wrt tournament.
// Mirrors TournamentDataAccess.SaveToFile defenses:
//   1. pre-save backup into <dir>/Backups/<filename>/<timestamp>.wrt
//   2. atomic write via .tmp → rename
//   3. post-save verification (re-parse); on failure, restore backup
//
// Usage:
//   node apply_plan.mjs --wrt <target.wrt> --plan <update_plan.json>

import { readFileSync, writeFileSync, copyFileSync, mkdirSync, renameSync } from 'node:fs';
import { dirname, basename, join } from 'node:path';
import { randomUUID } from 'node:crypto';

function parseArgs(argv) {
    const out = {};
    for (let i = 2; i < argv.length; i++) {
        const a = argv[i];
        if (a.startsWith('--')) { out[a.slice(2)] = argv[++i]; }
    }
    return out;
}
const args = parseArgs(process.argv);
for (const req of ['wrt', 'plan']) {
    if (!args[req]) {
        console.error(`Missing --${req}`);
        console.error('Usage: apply_plan.mjs --wrt <target.wrt> --plan <update_plan.json>');
        process.exit(2);
    }
}

function stamp() {
    const d = new Date();
    const p = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}${p(d.getMonth()+1)}${p(d.getDate())}_${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}_${String(d.getMilliseconds()).padStart(3, '0')}`;
}

function preSaveBackup(wrtPath) {
    const dir = dirname(wrtPath);
    const base = basename(wrtPath);
    const backupRoot = join(dir, 'Backups', base);
    mkdirSync(backupRoot, { recursive: true });
    const target = join(backupRoot, `${stamp()}.wrt`);
    copyFileSync(wrtPath, target);
    return target;
}

// ---------- Load ----------
const wrt = JSON.parse(readFileSync(args.wrt, 'utf8'));
const plan = JSON.parse(readFileSync(args.plan, 'utf8'));

if (!plan.newTeams?.length && !plan.newWrestlers?.length) {
    console.log('Nothing to apply (0 new teams, 0 new wrestlers). Aborting.');
    process.exit(0);
}

const existingWrestlerIds = new Set(wrt.Wrestlers.map(w => w.ID));
const existingTeamIds = new Set(wrt.TeamApplications.map(t => t.ID));
const existingShortNames = new Set(wrt.TeamApplications.map(t => t.ShortName));

// ---------- Pre-flight invariant checks ----------
// Fail early and LOUDLY rather than silently renaming / re-IDing, so a badly
// edited plan surfaces the problem instead of being quietly mangled.
const SHORT_MAX = 12;
const errors = [];

const planTeamIds = new Set();
const planShortNames = new Set();
for (const t of plan.newTeams || []) {
    if (!t.id) errors.push(`team without id: ${t.shortName || t.fullName}`);
    else if (existingTeamIds.has(t.id)) errors.push(`team id ${t.id} collides with existing team`);
    else if (planTeamIds.has(t.id)) errors.push(`team id ${t.id} duplicated within plan`);
    planTeamIds.add(t.id);

    if (!t.shortName) errors.push(`team ${t.id} has empty ShortName`);
    else if (t.shortName.length > SHORT_MAX) errors.push(`ShortName "${t.shortName}" exceeds ${SHORT_MAX} chars`);
    else if (existingShortNames.has(t.shortName)) errors.push(`ShortName "${t.shortName}" collides with existing team`);
    else if (planShortNames.has(t.shortName)) errors.push(`ShortName "${t.shortName}" duplicated within plan`);
    planShortNames.add(t.shortName);
}

// Optional: plan may ship pre-assigned wrestler IDs; enforce uniqueness if so.
const planWrestlerIds = new Set();
for (const w of plan.newWrestlers || []) {
    if (!w.wrestlerId) continue;
    if (existingWrestlerIds.has(w.wrestlerId)) errors.push(`wrestler id ${w.wrestlerId} collides with existing wrestler`);
    else if (planWrestlerIds.has(w.wrestlerId)) errors.push(`wrestler id ${w.wrestlerId} duplicated within plan`);
    planWrestlerIds.add(w.wrestlerId);
}

if (errors.length) {
    console.error('Plan failed pre-flight checks:');
    for (const e of errors) console.error('  - ' + e);
    console.error('Aborting without writing.');
    process.exit(1);
}

// ---------- Append teams ----------
// HashTag is deliberately forced to null for every team we create — the .wrt
// convention keeps hashtags unused on import (plan values, if any, are ignored).
for (const t of plan.newTeams) {
    existingTeamIds.add(t.id);
    existingShortNames.add(t.shortName);
    wrt.TeamApplications.push({
        ID: t.id,
        FullName: t.fullName,
        ShortName: t.shortName,
        HashTag: null,
        MainCoach: t.coach,
        Representative: null,
        Country: 'Россия',
        City: t.city,
        FullAddress: null,
        PhoneNumber: null,
        Email: null,
        EmblemPath: null
    });
}

// ---------- Append wrestlers ----------
const groupById = new Map(wrt.Groups.map(g => [g.ID, g]));
const seedNext = new Map();
for (const g of wrt.Groups) {
    const ids = g.Wrestlers || [];
    const seeds = ids.map(id => wrt.Wrestlers.find(w => w.ID === id)).filter(Boolean).map(w => w.SeedNumber || 0);
    seedNext.set(g.ID, seeds.length ? Math.max(...seeds) + 1 : 1);
}

function mapRank(raw) {
    const t = String(raw || '').trim().toLowerCase();
    if (!t || t === 'отсутствует' || t === 'б/р' || t === 'бр' || t === 'нет') return 'б/р';
    if (t === '1') return 'I юн';
    if (t === '2') return 'II юн';
    if (t === '3') return 'III юн';
    return raw;
}

let added = 0;
for (const w of plan.newWrestlers) {
    const group = groupById.get(w.groupId);
    if (!group) { console.warn('no group for', w.name); continue; }
    const teamId = w.teamId;
    // Respect any pre-assigned wrestlerId (validated above for uniqueness);
    // otherwise generate a fresh UUID that does not collide with existing ones
    // or with previously-added wrestlers in this same run.
    let wid = w.wrestlerId || randomUUID();
    while (existingWrestlerIds.has(wid)) wid = randomUUID();
    existingWrestlerIds.add(wid);

    const seed = seedNext.get(group.ID);
    seedNext.set(group.ID, seed + 1);

    // HashTag is forced to null here too — same rule as for teams.
    wrt.Wrestlers.push({
        ID: wid,
        PaidAmount: null,
        TeamID: teamId,
        GroupID: group.ID,
        FirstName: w.firstName,
        LastName: w.lastName,
        MiddleName: w.middleName,
        BirthDate: w.dobIso,
        Weight: w.weight,
        FinalPlace: null,
        IsSeedFixed: false,
        SeedNumber: seed,
        IsFemale: false,
        IsEntryFeePaid: true,
        IsWeightApproved: false,
        HashTag: null,
        Level: mapRank(w.rank),
        Timestamp: null
    });
    (group.Wrestlers ||= []).push(wid);
    added++;
}

// ---------- Save atomically + verify ----------
const backup = preSaveBackup(args.wrt);
const tmp = args.wrt + '.tmp.' + randomUUID();
writeFileSync(tmp, JSON.stringify(wrt), 'utf8');
renameSync(tmp, args.wrt);

try {
    const verified = JSON.parse(readFileSync(args.wrt, 'utf8'));

    // Integrity check: every wrestler resolves to a team + group; Group.Wrestlers[]
    // IDs resolve to top-level wrestlers; per-group counts are consistent.
    const wIds = new Set(verified.Wrestlers.map(w => w.ID));
    const tIds = new Set(verified.TeamApplications.map(x => x.ID));
    const gIds = new Set(verified.Groups.map(g => g.ID));
    if (wIds.size !== verified.Wrestlers.length) throw new Error('duplicate wrestler IDs detected');
    if (tIds.size !== verified.TeamApplications.length) throw new Error('duplicate team IDs detected');

    let badT = 0, badG = 0;
    for (const w of verified.Wrestlers) {
        if (!tIds.has(w.TeamID)) badT++;
        if (!gIds.has(w.GroupID)) badG++;
    }
    let orphan = 0;
    for (const g of verified.Groups) for (const wid of (g.Wrestlers || [])) if (!wIds.has(wid)) orphan++;
    if (badT || badG || orphan) {
        throw new Error(`integrity: badTeamID=${badT} badGroupID=${badG} orphanInGroup=${orphan}`);
    }

    // ShortName invariants across the whole team table.
    const shortCounts = new Map();
    for (const t of verified.TeamApplications) {
        if (t.ShortName && t.ShortName.length > SHORT_MAX) throw new Error(`ShortName "${t.ShortName}" exceeds ${SHORT_MAX} chars`);
        shortCounts.set(t.ShortName, (shortCounts.get(t.ShortName) || 0) + 1);
    }
    for (const [name, count] of shortCounts) {
        if (count > 1) throw new Error(`duplicate ShortName "${name}" across ${count} teams`);
    }

    console.log(`Backup:   ${backup}`);
    console.log(`Applied:  +${plan.newTeams.length} teams, +${added} wrestlers`);
    console.log(`Totals:   ${verified.TeamApplications.length} teams, ${verified.Wrestlers.length} wrestlers, ${verified.Groups.length} groups`);
} catch (e) {
    copyFileSync(backup, args.wrt);
    console.error('Verification failed, restored from backup:', e.message);
    process.exit(1);
}

#!/usr/bin/env node
// Build an import plan by cross-referencing registration CSVs against a .wrt.
// Produces <out>: a JSON plan listing new wrestlers, new teams (deduped by
// coach surname + city among identical-coach rows), already-registered rows,
// skipped rows, and overweight warnings.
//
// Usage:
//   node build_plan.mjs --source-dir <csv-dir> --wrt <target.wrt> --out <plan.json>

import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { join, basename } from 'node:path';
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
for (const req of ['source-dir', 'wrt', 'out']) {
    if (!args[req]) {
        console.error(`Missing --${req}`);
        console.error('Usage: build_plan.mjs --source-dir <csv-dir> --wrt <target.wrt> --out <plan.json>');
        process.exit(2);
    }
}

// ---------- CSV parser (handles quoted commas) ----------
function parseCsv(text) {
    const rows = [];
    let row = [], field = '', inQ = false, i = 0;
    while (i < text.length) {
        const c = text[i];
        if (inQ) {
            if (c === '"') {
                if (text[i+1] === '"') { field += '"'; i += 2; continue; }
                inQ = false; i++; continue;
            }
            field += c; i++; continue;
        }
        if (c === '"') { inQ = true; i++; continue; }
        if (c === ',') { row.push(field); field = ''; i++; continue; }
        if (c === '\r') { i++; continue; }
        if (c === '\n') { row.push(field); rows.push(row); row = []; field = ''; i++; continue; }
        field += c; i++;
    }
    if (field.length || row.length) { row.push(field); rows.push(row); }
    return rows;
}

// ---------- Normalization helpers ----------
function norm(s) {
    if (!s) return '';
    return String(s).toLowerCase()
        .replace(/ё/g, 'е')
        .replace(/[«»""'`]/g, '')
        .replace(/[^\p{L}\p{N}\s]/gu, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}
function coachSurname(coach) {
    const t = norm(coach);
    if (!t) return '';
    return t.split(' ')[0];
}
function splitFio(full) {
    const p = String(full || '').trim().replace(/\s+/g, ' ').split(' ');
    return { last: p[0] || '', first: p[1] || '', middle: p.slice(2).join(' ') || '' };
}
function parseDob(s) {
    const m = String(s || '').trim().match(/^(\d{1,2})[.\/-](\d{1,2})[.\/-](\d{2,4})/);
    if (!m) return null;
    let y = +m[3]; if (y < 100) y += 2000;
    const d = String(+m[1]).padStart(2, '0');
    const mo = String(+m[2]).padStart(2, '0');
    return { y, key: `${y}-${mo}-${d}`, iso: `${y}-${mo}-${d}T00:00:00` };
}

// ---------- Load ----------
const wrt = JSON.parse(readFileSync(args.wrt, 'utf8'));

// Wrestler lookup by (last, first, middle, dob).
function wKey(l, f, m, dob) { return [norm(l), norm(f), norm(m), dob].join('|'); }
const registered = new Map();
for (const w of wrt.Wrestlers) {
    registered.set(wKey(w.LastName, w.FirstName, w.MiddleName, (w.BirthDate || '').slice(0, 10)), w);
}

// Existing team lookup: build from wrt.TeamApplications + the wrestlers'
// cross-ref to see "which raw team-name spelling maps to which existing team".
// We derive by scanning the actual wrestler records and their TeamID, plus
// the TeamApplication's FullName/ShortName as direct name keys.
const teamById = new Map(wrt.TeamApplications.map(t => [t.ID, t]));

// Direct name lookups against existing teams.
const fullNameToTeamId = new Map();
const shortNameToTeamId = new Map();
for (const t of wrt.TeamApplications) {
    fullNameToTeamId.set(norm(t.FullName), t.ID);
    shortNameToTeamId.set(norm(t.ShortName), t.ID);
}

// Coach-city → existing team: if an existing team's MainCoach + City matches,
// prefer that team over creating a new one.
const coachCityToTeamId = new Map();
for (const t of wrt.TeamApplications) {
    const k = [coachSurname(t.MainCoach), norm(t.City)].join('|');
    if (k !== '|' && !coachCityToTeamId.has(k)) coachCityToTeamId.set(k, t.ID);
}

function resolveTeam(rawTeam, rawCity, rawCoach) {
    const t = norm(rawTeam), c = norm(rawCity), s = coachSurname(rawCoach);
    if (fullNameToTeamId.has(t)) return { id: fullNameToTeamId.get(t), how: 'full-name' };
    if (shortNameToTeamId.has(t)) return { id: shortNameToTeamId.get(t), how: 'short-name' };
    const ck = [s, c].join('|');
    if (coachCityToTeamId.has(ck)) return { id: coachCityToTeamId.get(ck), how: 'coach-city' };
    for (const ti of wrt.TeamApplications) {
        const full = norm(ti.FullName);
        if (full && norm(ti.City) === c && (full.includes(t) || t.includes(full))) {
            return { id: ti.ID, how: 'substring' };
        }
    }
    return null;
}

// ---------- Group assignment ----------
function pickGroup(year, weight) {
    // Nearest-weight group where the birth year is inside the group's range.
    // Does NOT fall back to the heaviest group when overweight — overweight
    // rows are surfaced to the user via the needsManualGroup[] list so they
    // can either adjust the wrestler's weight or add a heavier group.
    if (year == null || weight == null || !isFinite(weight)) {
        return { ok: false, reason: 'BadInput' };
    }
    const cand = wrt.Groups.filter(g => {
        if (g.IsFemale) return false;
        if (g.BirthYearMin != null && year < g.BirthYearMin) return false;
        if (g.BirthYearMax != null && year > g.BirthYearMax) return false;
        return true;
    });
    if (cand.length === 0) {
        return { ok: false, reason: 'NoAgeMatch' };
    }
    const fits = cand.filter(g => g.WeightMax != null && weight <= g.WeightMax)
                     .sort((a, b) => a.WeightMax - b.WeightMax);
    if (fits.length) return { ok: true, group: fits[0] };

    // Overweight: no group in the age range has a WeightMax >= weight.
    const maxInRange = cand.reduce((max, g) =>
        (g.WeightMax != null && g.WeightMax > max) ? g.WeightMax : max, 0);
    return { ok: false, reason: 'Overweight', maxWeightInAgeRange: maxInRange };
}

// ---------- Process CSVs ----------
const alreadyRegistered = [];
const matched = []; // new wrestler, existing team
const pendingTeam = []; // new wrestler, team not in .wrt
const skipped = [];           // unparseable rows (no DOB, no weight, etc.)
const needsManualGroup = [];  // row is parseable but no group fits; user must decide

const files = readdirSync(args['source-dir']).filter(f => f.toLowerCase().endsWith('.csv'));
for (const f of files) {
    const text = readFileSync(join(args['source-dir'], f), 'utf8');
    const rows = parseCsv(text);
    rows.shift(); // header
    for (let idx = 0; idx < rows.length; idx++) {
        const r = rows[idx];
        if (!r || r.every(x => !x || !x.trim())) continue;
        const [name, bd, w, rank, city, team, coach] = r;
        if (!name || !name.trim()) continue;
        const dob = parseDob(bd);
        if (!dob) {
            skipped.push({ sourceFile: f, row: idx + 1, reason: 'BadDob', name, bd });
            continue;
        }
        const weight = parseFloat(String(w).replace(',', '.'));
        if (!isFinite(weight) || weight <= 0) {
            skipped.push({ sourceFile: f, row: idx + 1, reason: 'BadWeight', name, w });
            continue;
        }
        const fio = splitFio(name);
        const key = wKey(fio.last, fio.first, fio.middle, dob.key);
        const existing = registered.get(key);
        if (existing) {
            alreadyRegistered.push({
                sourceFile: f, row: idx + 1, name,
                wrestlerId: existing.ID, teamId: existing.TeamID, groupId: existing.GroupID
            });
            continue;
        }
        const gp = pickGroup(dob.y, weight);
        if (!gp.ok) {
            // Row is well-formed but no existing group fits. Do NOT auto-assign.
            // Surface to the user so they can: (a) fix the wrestler's weight in
            // the source sheet / plan, or (b) add a new group to the .wrt and
            // re-run.
            needsManualGroup.push({
                sourceFile: f, row: idx + 1,
                name, year: dob.y, weight, rank, city, team, coach,
                reason: gp.reason,
                maxWeightInAgeRange: gp.maxWeightInAgeRange || null
            });
            continue;
        }
        const rec = {
            sourceFile: f, row: idx + 1,
            name, rank, city, team, coach, weight,
            lastName: fio.last, firstName: fio.first, middleName: fio.middle,
            dobKey: dob.key, dobIso: dob.iso, year: dob.y,
            groupId: gp.group.ID,
            groupKey: `${gp.group.BirthYearMin}-${gp.group.BirthYearMax || ''}|${gp.group.WeightMax}`,
            teamId: null,
            teamHow: null
        };
        const teamRes = resolveTeam(team, city, coach);
        if (teamRes) {
            rec.teamId = teamRes.id;
            rec.teamHow = teamRes.how;
            matched.push(rec);
        } else {
            pendingTeam.push(rec);
        }
    }
}

// ---------- Dedup new teams by (coach surname, normalized city) ----------
const newTeamClusters = new Map();
for (const p of pendingTeam) {
    const k = [coachSurname(p.coach), norm(p.city)].join('|');
    if (!newTeamClusters.has(k)) {
        newTeamClusters.set(k, {
            id: randomUUID(),
            coach: p.coach, city: p.city,
            variants: new Set(),
            wrestlers: []
        });
    }
    const cl = newTeamClusters.get(k);
    cl.variants.add(p.team);
    cl.wrestlers.push(p);
}

// Team ID must be unique across the entire .wrt. Seed the collision set with
// existing IDs so a freshly-generated UUID can't collide with an existing team.
const allTeamIds = new Set(wrt.TeamApplications.map(t => t.ID));
// Short name must be unique AND ≤ 12 chars (UI/printing constraint).
const SHORT_MAX = 12;
const existingShortNames = new Set(wrt.TeamApplications.map(x => x.ShortName));

function makeShortName(variants, city) {
    // Start with the shortest variant (the coach's own abbreviation, usually).
    let base = variants.slice().sort((a, b) => a.length - b.length)[0] || '';
    if (base.length > SHORT_MAX) base = base.slice(0, SHORT_MAX);
    if (!existingShortNames.has(base) && base) return base;

    // Collision (or empty): tack on a 2-char city initialism, still ≤ 12.
    const tag = (city || '').replace(/[^\p{L}]/gu, '').slice(0, 2);
    let candidate = (base + (tag ? ' ' + tag : '')).slice(0, SHORT_MAX);
    if (candidate && !existingShortNames.has(candidate)) return candidate;

    // Still colliding: numeric suffix. Always fits in SHORT_MAX because we
    // trim base to leave room for the " 99" tail.
    for (let n = 2; n <= 99; n++) {
        const tail = ' ' + n;
        const b = base.slice(0, SHORT_MAX - tail.length);
        candidate = (b + tail);
        if (!existingShortNames.has(candidate)) return candidate;
    }
    // Extreme fallback: UUID chunk (will never repeat).
    return ('t' + randomUUID().slice(0, SHORT_MAX - 1));
}

function makeTeamId(seedId) {
    let id = seedId;
    while (allTeamIds.has(id)) id = randomUUID();
    allTeamIds.add(id);
    return id;
}

const newTeams = [];
for (const cl of newTeamClusters.values()) {
    const variants = Array.from(cl.variants);
    const fullName = variants.slice().sort((a, b) => b.length - a.length)[0];
    const shortName = makeShortName(variants, cl.city);
    existingShortNames.add(shortName);
    const id = makeTeamId(cl.id);
    newTeams.push({
        id,
        fullName,
        shortName,
        city: cl.city,
        coach: cl.coach,
        sourceVariants: variants,
        wrestlerCount: cl.wrestlers.length
    });
    for (const p of cl.wrestlers) { p.teamId = id; p.teamHow = 'new-team'; }
}

// ---------- Aggregate ----------
const newWrestlers = matched.concat(pendingTeam);
const perGroup = {};
for (const w of newWrestlers) {
    perGroup[w.groupKey] = (perGroup[w.groupKey] || 0) + 1;
}

const plan = {
    totals: {
        alreadyRegistered: alreadyRegistered.length,
        newWrestlersToAdd: newWrestlers.length,
        newTeamsToCreate: newTeams.length,
        skipped: skipped.length,
        needsManualGroup: needsManualGroup.length,
        projectedWrestlerTotal: wrt.Wrestlers.length + newWrestlers.length,
        projectedTeamTotal: wrt.TeamApplications.length + newTeams.length,
        currentWrestlers: wrt.Wrestlers.length,
        currentTeams: wrt.TeamApplications.length
    },
    wrtPath: args.wrt,
    wrtName: wrt.Name,
    newTeams,
    newWrestlers,
    alreadyRegistered,
    skipped,
    needsManualGroup,
    perGroup
};

writeFileSync(args.out, JSON.stringify(plan, null, 2), 'utf8');

// Console summary for the invoker.
console.log('=== PLAN SUMMARY ===');
console.log(JSON.stringify(plan.totals, null, 2));
if (newTeams.length) {
    console.log('\nNew teams:');
    for (const t of newTeams) {
        console.log(`  [${t.shortName}] ${t.fullName} — ${t.city} — ${t.coach} (+${t.wrestlerCount}) variants=[${t.sourceVariants.join(', ')}]`);
    }
}
if (needsManualGroup.length) {
    console.log('\nNeeds manual group assignment (not auto-added):');
    for (const n of needsManualGroup) {
        const detail = n.reason === 'Overweight'
            ? `вес ${n.weight}kg > самой тяжёлой группы ${n.maxWeightInAgeRange}kg в возрасте ${n.year}`
            : n.reason === 'NoAgeMatch'
            ? `${n.year} г.р. не попадает ни в один возрастной диапазон`
            : n.reason;
        console.log(`  ${n.name} — ${detail}`);
    }
}
if (skipped.length) {
    console.log('\nSkipped (unparseable rows):');
    for (const s of skipped) console.log(`  ${s.name} — ${s.reason}`);
}
console.log(`\nPlan written to ${args.out}`);

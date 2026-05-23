#!/usr/bin/env node
// draw-seeding: apply.mjs
// Применяет план жеребьёвки (seeding_plan.json от plan.mjs) к .wrt-файлу.
// Три защиты: pre-save backup, atomic write (.tmp → rename), post-write verify.
// При ошибке восстанавливает из бэкапа.
//
// Usage:
//   node apply.mjs --wrt <target.wrt> --plan <seeding_plan.json> [--no-reset-brackets]

import { readFileSync, writeFileSync, copyFileSync, mkdirSync, renameSync } from 'node:fs';
import { dirname, basename, join } from 'node:path';
import { randomUUID } from 'node:crypto';

function parseArgs(argv) {
    const out = {};
    for (let i = 2; i < argv.length; i++) {
        const a = argv[i];
        if (a.startsWith('--no-')) { out[a.slice(5)] = false; continue; }
        if (a.startsWith('--')) {
            const eq = a.indexOf('=');
            if (eq >= 0) out[a.slice(2, eq)] = a.slice(eq + 1);
            else if (i + 1 < argv.length && !argv[i + 1].startsWith('--')) out[a.slice(2)] = argv[++i];
            else out[a.slice(2)] = true;
        }
    }
    return out;
}
const args = parseArgs(process.argv);
for (const req of ['wrt', 'plan']) {
    if (!args[req]) {
        console.error(`Missing --${req}`);
        console.error('Usage: apply.mjs --wrt <target.wrt> --plan <plan.json> [--no-reset-brackets]');
        process.exit(2);
    }
}
const resetBrackets = args['reset-brackets'] !== false; // default true

function stamp() {
    const d = new Date();
    const p = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}_${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}_${String(d.getMilliseconds()).padStart(3, '0')}`;
}
function preSaveBackup(wrtPath) {
    const dir = dirname(wrtPath);
    const base = basename(wrtPath);
    const root = join(dir, 'Backups', base);
    mkdirSync(root, { recursive: true });
    const target = join(root, `${stamp()}.wrt`);
    copyFileSync(wrtPath, target);
    return target;
}

// ────────── Load ──────────
const wrt = JSON.parse(readFileSync(args.wrt, 'utf8'));
const plan = JSON.parse(readFileSync(args.plan, 'utf8'));

const appliedGroups = plan.groups.filter(g => g.status === 'ok' || g.status === 'force');
if (appliedGroups.length === 0) {
    console.log('План не содержит групп для применения. Выход.');
    process.exit(0);
}

// ────────── Pre-flight ──────────
const errors = [];
const wrestlerById = new Map(wrt.Wrestlers.map(w => [w.ID, w]));
const groupById = new Map(wrt.Groups.map(g => [g.ID, g]));

for (const gr of appliedGroups) {
    const group = groupById.get(gr.groupId);
    if (!group) { errors.push(`Группа ${gr.groupId} не найдена в .wrt`); continue; }
    // 1..N contiguous
    const seeds = gr.assignments.map(a => a.seed).sort((a, b) => a - b);
    for (let i = 0; i < seeds.length; i++) {
        if (seeds[i] !== i + 1) {
            errors.push(`Группа ${gr.groupId}: некорректный набор seed'ов (${seeds.join(',')})`);
            break;
        }
    }
    // All wrestlers exist and belong to this group
    for (const a of gr.assignments) {
        const w = wrestlerById.get(a.wrestlerId);
        if (!w) { errors.push(`Группа ${gr.groupId}: спортсмен ${a.wrestlerId} отсутствует в .wrt`); continue; }
        if (w.GroupID !== group.ID) errors.push(`Группа ${gr.groupId}: спортсмен ${a.wrestlerId} привязан к другой группе (${w.GroupID})`);
    }
    // Group.Wrestlers list matches
    const planIds = new Set(gr.assignments.map(a => a.wrestlerId));
    const actualIds = new Set(group.Wrestlers || []);
    if (planIds.size !== actualIds.size || [...planIds].some(id => !actualIds.has(id))) {
        errors.push(`Группа ${gr.groupId}: список участников в плане не совпадает с .wrt`);
    }
}
if (errors.length) {
    console.error('Предзапись прервана (ошибки валидации):');
    for (const e of errors) console.error('  · ' + e);
    process.exit(1);
}

// ────────── Mutate ──────────
let mutatedWrestlers = 0;
let mutatedBrackets = 0;
for (const gr of appliedGroups) {
    const group = groupById.get(gr.groupId);
    for (const a of gr.assignments) {
        const w = wrestlerById.get(a.wrestlerId);
        w.SeedNumber = a.seed;
        w.IsSeedFixed = true;
        if (gr.status === 'force') {
            // Losing completed matches is explicit user choice (passed via --force).
            // Clear FinalPlace so nothing from the old bracket state leaks through.
            w.FinalPlace = null;
        }
        mutatedWrestlers++;
    }
    if (resetBrackets) {
        group.Bracket = null;
        mutatedBrackets++;
    }
}

// ────────── Atomic save ──────────
const backup = preSaveBackup(args.wrt);
const tmp = args.wrt + '.tmp.' + randomUUID();
writeFileSync(tmp, JSON.stringify(wrt), 'utf8');
renameSync(tmp, args.wrt);

// ────────── Verify ──────────
try {
    const verified = JSON.parse(readFileSync(args.wrt, 'utf8'));
    const wIdsVerif = new Map(verified.Wrestlers.map(w => [w.ID, w]));
    for (const gr of appliedGroups) {
        const seeds = [];
        for (const a of gr.assignments) {
            const w = wIdsVerif.get(a.wrestlerId);
            if (!w) throw new Error(`после записи пропал спортсмен ${a.wrestlerId}`);
            if (w.SeedNumber !== a.seed) throw new Error(`seed mismatch ${a.wrestlerId}: ожидал ${a.seed}, в файле ${w.SeedNumber}`);
            if (w.IsSeedFixed !== true) throw new Error(`IsSeedFixed не true у ${a.wrestlerId}`);
            seeds.push(w.SeedNumber);
        }
        seeds.sort((a, b) => a - b);
        for (let i = 0; i < seeds.length; i++) {
            if (seeds[i] !== i + 1) throw new Error(`группа ${gr.groupId}: seed-номера не 1..N после записи`);
        }
    }
    console.log(`Backup:   ${backup}`);
    console.log(`Применено: ${appliedGroups.length} групп, ${mutatedWrestlers} спортсменов${resetBrackets ? `, сброшено сеток: ${mutatedBrackets}` : ''}`);
    console.log(`Итого в файле: ${verified.Wrestlers.length} спортсменов, ${verified.Groups.length} групп`);
} catch (e) {
    copyFileSync(backup, args.wrt);
    console.error('Верификация не прошла, файл восстановлен из бэкапа:', e.message);
    process.exit(1);
}

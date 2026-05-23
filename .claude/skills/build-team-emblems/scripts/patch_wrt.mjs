#!/usr/bin/env node
// Apply EmblemPath updates to a .wrt file. Steps:
//  1. Pre-save backup: copy current .wrt to <wrt-dir>/Backups/<timestamp>_emblem_patch.wrt
//  2. Walk graph, set EmblemPath on each TeamApplication matching (City, Country) → file
//  3. Atomic write: write to .tmp, then rename
//  4. Post-write verification: re-parse + check expected updates landed
//  5. On failure, restore from backup
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    wrt: { type: 'string' },
    workdir: { type: 'string' },
    'dry-run': { type: 'boolean', default: false },
  },
});
if (!values.wrt || !values.workdir) {
  console.error('Usage: patch_wrt.mjs --wrt <path.wrt> --workdir <dir> [--dry-run]');
  process.exit(2);
}

const mapping = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_mapping.json'), 'utf8'));

const wrtDir = path.dirname(values.wrt);
const backupsDir = path.join(wrtDir, 'Backups');
if (!values['dry-run']) fs.mkdirSync(backupsDir, { recursive: true });

const ts = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 17);
const backupPath = path.join(backupsDir, `${ts}_emblem_patch.wrt`);
if (!values['dry-run']) {
  fs.copyFileSync(values.wrt, backupPath);
  console.log(`Backup → ${backupPath} (${fs.statSync(backupPath).size} B)`);
} else {
  console.log('[dry-run] would back up to', backupPath);
}

const raw = fs.readFileSync(values.wrt, 'utf8');
const wrt = JSON.parse(raw);

const updates = [];
function walk(obj, seen) {
  if (!obj || typeof obj !== 'object') return;
  if (seen.has(obj)) return;
  seen.add(obj);
  if (obj.HashTag !== undefined && obj.City !== undefined && obj.Country !== undefined && obj.ShortName !== undefined && !obj.HashTag) {
    const key = (obj.City || '') + '|' + (obj.Country || '');
    const file = mapping[key];
    if (file && obj.EmblemPath !== file) {
      const before = obj.EmblemPath;
      if (!values['dry-run']) obj.EmblemPath = file;
      updates.push({ short: obj.ShortName, city: obj.City, country: obj.Country, before, after: file });
    }
  }
  if (Array.isArray(obj)) for (const x of obj) walk(x, seen);
  else for (const k of Object.keys(obj)) walk(obj[k], seen);
}
walk(wrt, new WeakSet());

console.log(`\n${updates.length} TeamApplication node(s) ${values['dry-run'] ? 'would be' : 'will be'} updated:`);
for (const u of updates) {
  const prev = u.before ? ` (was: ${u.before})` : '';
  console.log(`  ${u.short} | ${u.city}  →  ${u.after}${prev}`);
}

if (values['dry-run']) {
  console.log('\n[dry-run] no file written.');
  process.exit(0);
}

if (updates.length === 0) {
  console.log('\nNothing to patch — exiting without writing.');
  process.exit(0);
}

// Atomic write: tmp → rename. Then verify by re-parsing and confirming counts.
const out = JSON.stringify(wrt);
const tmp = values.wrt + '.tmp.' + Math.random().toString(36).slice(2);
fs.writeFileSync(tmp, out);
fs.renameSync(tmp, values.wrt);

let verifyOk = false;
try {
  const reparsed = JSON.parse(fs.readFileSync(values.wrt, 'utf8'));
  let appliedCount = 0;
  function check(obj, seen) {
    if (!obj || typeof obj !== 'object') return;
    if (seen.has(obj)) return; seen.add(obj);
    if (obj.HashTag !== undefined && obj.City !== undefined && !obj.HashTag) {
      const key = (obj.City || '') + '|' + (obj.Country || '');
      const expected = mapping[key];
      if (expected && obj.EmblemPath === expected) appliedCount++;
    }
    if (Array.isArray(obj)) for (const x of obj) check(x, seen);
    else for (const k of Object.keys(obj)) check(obj[k], seen);
  }
  check(reparsed, new WeakSet());
  verifyOk = appliedCount >= updates.length;
  console.log(`\nVerify: ${appliedCount} TeamApplication node(s) carry expected EmblemPath after re-parse.`);
} catch (e) {
  console.error('Verify FAILED — could not re-parse .wrt:', e.message);
}

if (!verifyOk) {
  console.error('Restoring backup …');
  fs.copyFileSync(backupPath, values.wrt);
  console.error('Restored. Aborting non-zero.');
  process.exit(1);
}

fs.writeFileSync(path.join(values.workdir, 'emblem_patch_report.json'), JSON.stringify({
  backup: backupPath,
  updates,
  totalUpdates: updates.length,
  timestamp: new Date().toISOString(),
}, null, 2));
console.log('\nPatch complete.');

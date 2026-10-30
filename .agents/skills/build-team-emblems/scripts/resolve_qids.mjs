#!/usr/bin/env node
// Resolve Wikidata QID for each target via wbsearchentities, with overrides.
// Output: <workdir>/emblem_qids.json
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const { values } = parseArgs({
  options: {
    workdir: { type: 'string' },
    overrides: { type: 'string' },
  },
});
if (!values.workdir) {
  console.error('Usage: resolve_qids.mjs --workdir <dir> [--overrides <path.json>]');
  process.exit(2);
}

const targets = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_targets.json'), 'utf8')).targets;

const KNOWN = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'data', 'known_overrides.json'), 'utf8'));
const overridesQids = { ...KNOWN.qids };
if (values.overrides && fs.existsSync(values.overrides)) {
  const user = JSON.parse(fs.readFileSync(values.overrides, 'utf8'));
  Object.assign(overridesQids, user.qids || {});
}

const UA = { 'User-Agent': 'WrestlingAdmin-EmblemSkill/1.0 (+local-tournament-tooling)' };
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function search(term) {
  const url = `https://www.wikidata.org/w/api.php?action=wbsearchentities&search=${encodeURIComponent(term)}&language=ru&uselang=ru&format=json&type=item&limit=5`;
  const r = await fetch(url, { headers: UA });
  return (await r.json()).search || [];
}
async function getEntity(qid) {
  const url = `https://www.wikidata.org/w/api.php?action=wbgetentities&ids=${qid}&props=labels|descriptions&languages=ru|en&format=json`;
  const r = await fetch(url, { headers: UA });
  return (await r.json()).entities[qid];
}

const results = [];
for (const t of targets) {
  const overrideKey = (t.primary || '') + '|' + (t.region || '');
  let qid = overridesQids[overrideKey];
  let candidates = [];
  if (!qid) {
    const term = t.region ? `${t.primary} ${t.region}` : t.primary;
    candidates = await search(term);
    const filtered = candidates.filter(c => /город|посёл|село|селение|деревня|пгт|муниципал|administrat|town|city|village|country|страна|столица/i.test(c.description || ''));
    qid = (filtered[0] || candidates[0])?.id;
    await sleep(200);
  }
  let label = null, desc = null;
  if (qid) {
    const ent = await getEntity(qid);
    label = ent?.labels?.ru?.value || ent?.labels?.en?.value || null;
    desc = ent?.descriptions?.ru?.value || ent?.descriptions?.en?.value || null;
    await sleep(200);
  }
  results.push({ ...t, qid, qidLabel: label, qidDesc: desc, candidates: candidates.slice(0, 3).map(c => ({ id: c.id, label: c.label, desc: c.description })) });
  console.log(`  ${t.primary}${t.region ? ` (${t.region})` : ''}  →  ${qid || 'NOT FOUND'} ${label || ''} | ${desc || ''}`);
}

const outPath = path.join(values.workdir, 'emblem_qids.json');
fs.writeFileSync(outPath, JSON.stringify(results, null, 2));
const ok = results.filter(r => r.qid).length;
console.log(`\nResolved: ${ok}/${results.length}`);
if (ok < results.length) {
  console.log('Unresolved targets — provide overrides JSON via --overrides:');
  for (const r of results.filter(x => !x.qid)) {
    console.log(`  ${r.primary}|${r.region || ''}`);
  }
  process.exit(1);
}

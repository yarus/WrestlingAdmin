#!/usr/bin/env node
// For each QID get coat-of-arms image (Wikidata P94). On miss, walk up P131
// (located in administrative territorial entity) up to maxDepth times.
// Output: <workdir>/emblem_arms.json
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    workdir: { type: 'string' },
    'max-depth': { type: 'string', default: '4' },
  },
});
if (!values.workdir) {
  console.error('Usage: fetch_arms.mjs --workdir <dir> [--max-depth 4]');
  process.exit(2);
}
const maxDepth = Number(values['max-depth']);

const qids = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_qids.json'), 'utf8'));

const UA = { 'User-Agent': 'WrestlingAdmin-EmblemSkill/1.0 (+local-tournament-tooling)' };
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function getEntityFull(qid) {
  const url = `https://www.wikidata.org/w/api.php?action=wbgetentities&ids=${qid}&props=claims|labels&languages=ru|en&format=json`;
  const r = await fetch(url, { headers: UA });
  return (await r.json()).entities[qid];
}
function extractArms(entity) {
  const claims = entity?.claims?.P94;
  if (!claims) return null;
  for (const c of claims) {
    const v = c.mainsnak?.datavalue?.value;
    if (v) return v;
  }
  return null;
}
function extractParents(entity) {
  const claims = entity?.claims?.P131;
  if (!claims) return [];
  return claims.map(c => c.mainsnak?.datavalue?.value?.id).filter(Boolean);
}

async function findArms(startQid) {
  let qid = startQid;
  const walk = [];
  for (let i = 0; i < maxDepth; i++) {
    if (!qid) break;
    const ent = await getEntityFull(qid);
    const label = ent?.labels?.ru?.value || ent?.labels?.en?.value || qid;
    const arms = extractArms(ent);
    walk.push({ qid, label, arms });
    if (arms) return { arms, source: qid, sourceLabel: label, walk };
    qid = extractParents(ent)[0];
    await sleep(150);
  }
  return { arms: null, source: null, walk };
}

const out = [];
for (const item of qids) {
  process.stdout.write(`  ${item.primary}${item.region ? ` (${item.region})` : ''}  →  `);
  const r = await findArms(item.qid);
  out.push({ ...item, ...r });
  if (r.arms) {
    const fb = r.source !== item.qid ? ` [fallback: ${r.sourceLabel}]` : '';
    console.log(`${r.arms}${fb}`);
  } else {
    console.log('NO ARMS', '— walked:', r.walk.map(w => w.label).join(' → '));
  }
  await sleep(200);
}

const outPath = path.join(values.workdir, 'emblem_arms.json');
fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
const found = out.filter(o => o.arms).length;
console.log(`\nArms found: ${found}/${out.length}`);

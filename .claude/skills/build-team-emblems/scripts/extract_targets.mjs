#!/usr/bin/env node
// Extract unique cities/regions from a .wrt tournament file.
// Output: <workdir>/emblem_targets.json
//   { totalTeams, uniqueTargets, targets: [{primary, primaryKind, region, country, teams: [shortName...]}], perTeam: [{team, cityRaw, countryRaw, primary, region, country}] }
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    wrt: { type: 'string' },
    'workdir': { type: 'string' },
  },
});
if (!values.wrt || !values.workdir) {
  console.error('Usage: extract_targets.mjs --wrt <path.wrt> --workdir <dir>');
  process.exit(2);
}
fs.mkdirSync(values.workdir, { recursive: true });

const wrt = JSON.parse(fs.readFileSync(values.wrt, 'utf8'));

function walk(obj, out, seen) {
  if (!obj || typeof obj !== 'object') return;
  if (seen.has(obj)) return;
  seen.add(obj);
  if (obj.HashTag !== undefined && (obj.City !== undefined || obj.Country !== undefined) && obj.ShortName !== undefined) {
    out.push({ HashTag: obj.HashTag, ShortName: obj.ShortName, City: obj.City, Country: obj.Country, EmblemPath: obj.EmblemPath });
  }
  if (Array.isArray(obj)) for (const x of obj) walk(x, out, seen);
  else for (const k of Object.keys(obj)) walk(obj[k], out, seen);
}

const teams = [];
walk(wrt, teams, new WeakSet());

// Drop tournament-organizer placeholder entries (non-null HashTag = synthetic)
const real = teams.filter(t => !t.HashTag);

function normalize(team) {
  const raw = (team.City || '').trim();
  const country = (team.Country || '').trim();
  if (country && country !== 'Россия') {
    return { raw, primary: country, primaryKind: 'country', region: null, country };
  }
  let s = raw;
  let region = null;
  const commaIdx = s.lastIndexOf(',');
  if (commaIdx > 0) {
    const tail = s.slice(commaIdx + 1).trim();
    const regionPatterns = [
      [/Ленинградская\s*обл\.?/i, 'Ленинградская область'],
      [/Московская\s*обл\.?/i, 'Московская область'],
      [/Липецкая\s*обл\.?/i, 'Липецкая область'],
      [/Красноярский\s*край/i, 'Красноярский край'],
      [/Республика\s*Тыва/i, 'Республика Тыва'],
      [/республика\s*Дагестан/i, 'Республика Дагестан'],
      [/Свердловская\s*обл\.?/i, 'Свердловская область'],
      [/Самарская\s*обл\.?/i, 'Самарская область'],
      [/Ростовская\s*обл\.?/i, 'Ростовская область'],
      [/Краснодарский\s*край/i, 'Краснодарский край'],
    ];
    for (const [re, name] of regionPatterns) {
      if (re.test(tail)) { region = name; s = s.slice(0, commaIdx).trim(); break; }
    }
  }
  s = s.replace(/^(пос\.\s*им\.\s*|пгт\.\s*|с\.\s*|г\.\s*|д\.\s*)/i, '').trim();
  return { raw, primary: s, primaryKind: 'city', region, country: 'Россия' };
}

const perTeam = real.map(t => ({
  team: t.ShortName,
  cityRaw: t.City,
  countryRaw: t.Country,
  currentEmblem: t.EmblemPath || null,
  ...normalize(t),
}));

const byPrimary = new Map();
for (const t of perTeam) {
  const key = t.primary + '|' + t.primaryKind + '|' + (t.region || '');
  if (!byPrimary.has(key)) byPrimary.set(key, { primary: t.primary, primaryKind: t.primaryKind, region: t.region, country: t.country, teams: [] });
  byPrimary.get(key).teams.push(t.team);
}
const targets = [...byPrimary.values()].sort((a, b) => b.teams.length - a.teams.length);

const outPath = path.join(values.workdir, 'emblem_targets.json');
fs.writeFileSync(outPath, JSON.stringify({
  source: values.wrt,
  totalTeams: perTeam.length,
  uniqueTargets: targets.length,
  targets,
  perTeam,
}, null, 2));

console.log(`Teams: ${perTeam.length} | Unique targets: ${targets.length}`);
for (const t of targets) {
  const reg = t.region ? ` (region: ${t.region})` : '';
  const cou = t.country !== 'Россия' ? ` [${t.country}]` : '';
  console.log(`  ${String(t.teams.length).padStart(2)} × ${t.primary}${reg}${cou}`);
}
console.log(`\nWrote ${outPath}`);

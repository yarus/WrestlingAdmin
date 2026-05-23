#!/usr/bin/env node
// Build (cityRaw, countryRaw) → filename mapping consumed by patch_wrt.mjs.
// Also verifies file format / alpha presence and prints a per-team summary.
// Output: <workdir>/emblem_mapping.json + emblem_verified.json
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    workdir: { type: 'string' },
    'images-dir': { type: 'string' },
  },
});
if (!values.workdir || !values['images-dir']) {
  console.error('Usage: build_mapping.mjs --workdir <dir> --images-dir <Images-folder>');
  process.exit(2);
}

const downloads = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_downloaded.json'), 'utf8'));
const targets = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_targets.json'), 'utf8'));
const IMG_DIR = values['images-dir'];

function pngHasAlpha(buf) {
  if (buf.length < 33 || buf[0]!==0x89||buf[1]!==0x50||buf[2]!==0x4E||buf[3]!==0x47) return false;
  const colorType = buf[25];
  if (colorType === 4 || colorType === 6) return true;
  if (colorType === 3) {
    let off = 33;
    while (off < buf.length - 8) {
      const len = buf.readUInt32BE(off);
      const type = buf.slice(off+4, off+8).toString('ascii');
      if (type === 'tRNS') return true;
      if (type === 'IDAT' || type === 'IEND') return false;
      off += 12 + len;
    }
  }
  return false;
}
function gifHasAlpha(buf) {
  if (buf.length < 13 || buf[0]!==0x47||buf[1]!==0x49||buf[2]!==0x46) return false;
  for (let i = 13; i < buf.length - 6; i++) {
    if (buf[i]===0x21 && buf[i+1]===0xF9 && buf[i+2]===0x04 && (buf[i+3] & 0x01)) return true;
  }
  return false;
}

const verified = [];
for (const item of downloads) {
  if (!item.file) { verified.push({ ...item, verified: false }); continue; }
  const fullPath = path.join(IMG_DIR, item.file);
  if (!fs.existsSync(fullPath)) {
    verified.push({ ...item, verified: false, error: 'file missing' });
    console.log(`  ✗ ${item.slug}: file missing on disk`);
    continue;
  }
  const buf = fs.readFileSync(fullPath);
  const ext = path.extname(item.file).toLowerCase();
  let alpha = false, format = ext.slice(1).toUpperCase();
  if (ext === '.png') alpha = pngHasAlpha(buf);
  else if (ext === '.gif') alpha = gifHasAlpha(buf);
  verified.push({ ...item, format, sizeBytes: buf.length, alpha, verified: buf.length > 1000 });
  const ok = buf.length > 1000 ? '✓' : '✗';
  const a = alpha ? 'α' : ' ';
  const fb = item.source !== item.qid && item.source ? ' [fallback]' : '';
  console.log(`  ${ok}${a} ${item.file} (${format}, ${buf.length} B)${fb}`);
}

const targetToFile = {};
for (const item of verified) {
  if (!item.verified) continue;
  const k = item.primary + '|' + item.primaryKind + '|' + (item.region || '');
  targetToFile[k] = item.file;
}
const mapping = {};
for (const pt of targets.perTeam) {
  const k = pt.primary + '|' + pt.primaryKind + '|' + (pt.region || '');
  const file = targetToFile[k];
  if (!file) continue;
  mapping[(pt.cityRaw || '') + '|' + (pt.countryRaw || '')] = file;
}

fs.writeFileSync(path.join(values.workdir, 'emblem_verified.json'), JSON.stringify(verified, null, 2));
fs.writeFileSync(path.join(values.workdir, 'emblem_mapping.json'), JSON.stringify(mapping, null, 2));
console.log(`\nVerified: ${verified.filter(v => v.verified).length}/${verified.length}`);
console.log(`Mapping entries (cityRaw|country → file): ${Object.keys(mapping).length}`);

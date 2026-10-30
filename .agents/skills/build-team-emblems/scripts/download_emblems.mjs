#!/usr/bin/env node
// For each target with an arms filename:
//   1. If <slug>.{png,gif,jpeg,jpg,bmp} exists in --existing-dir → copy to --images-dir, mark as reused
//   2. Else if exists in --images-dir → mark as reused (already there)
//   3. Else → download from Wikimedia Special:FilePath?width=<width>
// Output: <workdir>/emblem_downloaded.json
// Wikimedia thumbnailer renders SVG → PNG with alpha at any width. GIF source files
// are returned as-is (renamed to .gif). PNG sources are returned at requested width.
import fs from 'node:fs';
import path from 'node:path';
import https from 'node:https';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const { values } = parseArgs({
  options: {
    workdir: { type: 'string' },
    'images-dir': { type: 'string' },
    'existing-dir': { type: 'string' },
    width: { type: 'string', default: '512' },
    overrides: { type: 'string' },
  },
});
if (!values.workdir || !values['images-dir']) {
  console.error('Usage: download_emblems.mjs --workdir <dir> --images-dir <Images-folder> [--existing-dir <dir>] [--width 512] [--overrides <path.json>]');
  process.exit(2);
}

const arms = JSON.parse(fs.readFileSync(path.join(values.workdir, 'emblem_arms.json'), 'utf8'));
const IMG_DIR = values['images-dir'];
const EXISTING = values['existing-dir'] || null;

if (!fs.existsSync(IMG_DIR)) {
  console.error('Images dir does not exist:', IMG_DIR);
  process.exit(1);
}

const KNOWN = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'data', 'known_overrides.json'), 'utf8'));
const slugOverrides = { ...KNOWN.slugs };
if (values.overrides && fs.existsSync(values.overrides)) {
  const user = JSON.parse(fs.readFileSync(values.overrides, 'utf8'));
  Object.assign(slugOverrides, user.slugs || {});
}

const TR = {
  а:'a',б:'b',в:'v',г:'g',д:'d',е:'e',ё:'yo',ж:'zh',з:'z',и:'i',й:'i',к:'k',л:'l',м:'m',н:'n',о:'o',п:'p',р:'r',с:'s',т:'t',у:'u',ф:'f',х:'kh',ц:'ts',ч:'ch',ш:'sh',щ:'sch',ъ:'',ы:'y',ь:'',э:'e',ю:'yu',я:'ya',
  А:'a',Б:'b',В:'v',Г:'g',Д:'d',Е:'e',Ё:'yo',Ж:'zh',З:'z',И:'i',Й:'i',К:'k',Л:'l',М:'m',Н:'n',О:'o',П:'p',Р:'r',С:'s',Т:'t',У:'u',Ф:'f',Х:'kh',Ц:'ts',Ч:'ch',Ш:'sh',Щ:'sch',Ъ:'',Ы:'y',Ь:'',Э:'e',Ю:'yu',Я:'ya'
};
function slugify(s) {
  return [...s.toLowerCase()].map(c => TR[c] !== undefined ? TR[c] : c).join('')
    .replace(/[^a-z0-9-]+/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
}

const SUPPORTED_EXT = ['.png', '.gif', '.jpeg', '.jpg', '.bmp'];

function findExistingFile(dir, slug) {
  if (!dir) return null;
  for (const ext of SUPPORTED_EXT) {
    const p = path.join(dir, slug + ext);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

const UA = 'WrestlingAdmin-EmblemSkill/1.0 (+local-tournament-tooling)';
function download(url, dest) {
  return new Promise((resolve, reject) => {
    const req = https.get(url, { headers: { 'User-Agent': UA } }, res => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return download(res.headers.location, dest).then(resolve, reject);
      }
      if (res.statusCode !== 200) {
        res.resume();
        return reject(new Error(`HTTP ${res.statusCode} for ${url}`));
      }
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => {
        const buf = Buffer.concat(chunks);
        fs.writeFileSync(dest, buf);
        resolve(buf);
      });
      res.on('error', reject);
    });
    req.on('error', reject);
    req.setTimeout(30000, () => { req.destroy(new Error('timeout')); });
  });
}

function detectFormat(buf) {
  if (buf[0]===0x89 && buf[1]===0x50 && buf[2]===0x4E && buf[3]===0x47) return 'png';
  if (buf[0]===0x47 && buf[1]===0x49 && buf[2]===0x46) return 'gif';
  if (buf[0]===0xFF && buf[1]===0xD8 && buf[2]===0xFF) return 'jpeg';
  if (buf[0]===0x42 && buf[1]===0x4D) return 'bmp';
  return null;
}

const out = [];
const sleep = ms => new Promise(r => setTimeout(r, ms));

for (const item of arms) {
  const key = (item.primary || '') + '|' + (item.region || '');
  const baseSlug = slugOverrides[key] || slugify(item.primary + (item.region ? '-' + item.region : ''));

  if (!item.arms) {
    out.push({ ...item, slug: baseSlug, file: null, downloaded: false, reused: false, reason: 'no arms found' });
    console.log(`  ✗ ${baseSlug}: no arms`);
    continue;
  }

  // Step 1: existing-dir reuse
  if (EXISTING) {
    const found = findExistingFile(EXISTING, baseSlug);
    if (found) {
      const ext = path.extname(found).toLowerCase();
      const target = path.join(IMG_DIR, baseSlug + ext);
      fs.copyFileSync(found, target);
      const buf = fs.readFileSync(target);
      out.push({ ...item, slug: baseSlug, file: baseSlug + ext, sizeBytes: buf.length, source: 'existing-dir', downloaded: false, reused: true });
      console.log(`  ↻ ${baseSlug}${ext}  ← existing-dir/${path.basename(found)} (${buf.length} B)`);
      continue;
    }
  }

  // Step 2: target dir already has it
  const inTarget = findExistingFile(IMG_DIR, baseSlug);
  if (inTarget) {
    const buf = fs.readFileSync(inTarget);
    out.push({ ...item, slug: baseSlug, file: path.basename(inTarget), sizeBytes: buf.length, source: 'images-dir', downloaded: false, reused: true });
    console.log(`  ✓ ${path.basename(inTarget)} already in target (${buf.length} B)`);
    continue;
  }

  // Step 3: download
  const tmpDest = path.join(IMG_DIR, baseSlug + '.png'); // tentative; renamed if GIF
  const url = `https://commons.wikimedia.org/wiki/Special:FilePath/${encodeURIComponent(item.arms)}?width=${values.width}`;
  process.stdout.write(`  ↓ ${baseSlug} ← ${item.arms} ... `);
  try {
    const buf = await download(url, tmpDest);
    const fmt = detectFormat(buf);
    let finalFile = baseSlug + '.png';
    if (fmt && fmt !== 'png') {
      const realExt = '.' + (fmt === 'jpeg' ? 'jpeg' : fmt);
      const renamed = path.join(IMG_DIR, baseSlug + realExt);
      fs.renameSync(tmpDest, renamed);
      finalFile = baseSlug + realExt;
    }
    out.push({ ...item, slug: baseSlug, file: finalFile, sizeBytes: buf.length, source: 'wikimedia', downloaded: true, reused: false, url });
    console.log(`${buf.length} B (${fmt || 'unknown'})`);
  } catch (e) {
    out.push({ ...item, slug: baseSlug, file: null, downloaded: false, reused: false, reason: e.message, url });
    console.log('ERR:', e.message);
  }
  await sleep(300);
}

const outPath = path.join(values.workdir, 'emblem_downloaded.json');
fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
const ok = out.filter(o => o.file).length;
const reused = out.filter(o => o.reused).length;
const dl = out.filter(o => o.downloaded).length;
console.log(`\nResolved files: ${ok}/${out.length} (downloaded ${dl}, reused ${reused}, skipped ${out.length - ok})`);

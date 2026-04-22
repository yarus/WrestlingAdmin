#!/usr/bin/env node
// Download specific tabs of a public Google Sheet as CSV files.
// Uses the gviz CSV export endpoint — no auth required, but the sheet must
// be viewable by anyone with the link.
//
// Usage:
//   node fetch_sheets.mjs --id <SPREADSHEET_ID> --tabs "Tab1,Tab2" --out-dir <path>
//
// Notes:
// - Tab names with commas: use --tabs-file <file> with one tab per line instead.
// - We cannot enumerate tabs automatically via an unauthenticated request —
//   the edit page's HTML does not expose gids reliably. Require explicit names.

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

function parseArgs(argv) {
    const out = {};
    for (let i = 2; i < argv.length; i++) {
        const a = argv[i];
        if (a.startsWith('--')) {
            const k = a.slice(2);
            const v = argv[i+1];
            out[k] = v;
            i++;
        }
    }
    return out;
}

const args = parseArgs(process.argv);
if (!args.id || !args['out-dir']) {
    console.error('Usage: fetch_sheets.mjs --id <SPREADSHEET_ID> (--tabs "A,B" | --tabs-file <file>) --out-dir <path>');
    process.exit(2);
}

let tabs;
if (args['tabs-file']) {
    tabs = readFileSync(args['tabs-file'], 'utf8')
        .split(/\r?\n/).map(s => s.trim()).filter(Boolean);
} else if (args.tabs) {
    tabs = args.tabs.split(',').map(s => s.trim()).filter(Boolean);
} else {
    console.error('Either --tabs or --tabs-file is required.');
    process.exit(2);
}

mkdirSync(args['out-dir'], { recursive: true });

const results = [];
for (const name of tabs) {
    const enc = encodeURIComponent(name);
    const url = `https://docs.google.com/spreadsheets/d/${args.id}/gviz/tq?tqx=out:csv&sheet=${enc}`;
    try {
        const res = await fetch(url);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const body = await res.text();
        const file = join(args['out-dir'], `${name}.csv`);
        writeFileSync(file, body, 'utf8');
        const lines = body.split('\n').length;
        results.push({ tab: name, file, bytes: body.length, lines });
        console.log(`OK: ${name} -> ${file} (${body.length} bytes, ${lines} lines)`);
    } catch (e) {
        results.push({ tab: name, error: e.message });
        console.error(`ERR: ${name} — ${e.message}`);
    }
}

writeFileSync(join(args['out-dir'], '_fetch_log.json'), JSON.stringify(results, null, 2), 'utf8');

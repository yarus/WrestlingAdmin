#!/usr/bin/env node
// draw-seeding: plan.mjs
// Строит план жеребьёвки (SeedNumber) для .wrt турнира WrestlingAdmin.
// Логика расстановки: клуб → город → рейтинг → Level (в порядке убывания приоритета).
// Не модифицирует .wrt; пишет seeding_plan.json + текстовый отчёт.
//
// Usage:
//   node plan.mjs --wrt <target.wrt> --out <workdir>/seeding_plan.json
//       [--rating <rating.csv>] [--groups <id,id>] [--force <id,id>]
//       [--lock <wid:seed,wid:seed>] [--random-seed 42]

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

// ────────────────────────────────────────────────────────────────
// CLI
// ────────────────────────────────────────────────────────────────
function parseArgs(argv) {
    const out = {};
    for (let i = 2; i < argv.length; i++) {
        const a = argv[i];
        if (a.startsWith('--')) {
            const eq = a.indexOf('=');
            if (eq >= 0) out[a.slice(2, eq)] = a.slice(eq + 1);
            else out[a.slice(2)] = argv[++i];
        }
    }
    return out;
}
const args = parseArgs(process.argv);
for (const req of ['wrt', 'out']) {
    if (!args[req]) {
        console.error(`Missing --${req}`);
        console.error('Usage: plan.mjs --wrt <target.wrt> --out <plan.json> [--rating <csv>] [--groups <ids>] [--force <ids>] [--lock <wid:seed>] [--random-seed 42]');
        process.exit(2);
    }
}

// ────────────────────────────────────────────────────────────────
// Deterministic RNG (mulberry32)
// ────────────────────────────────────────────────────────────────
function mulberry32(seed) {
    let a = seed >>> 0;
    return function () {
        a |= 0; a = a + 0x6D2B79F5 | 0;
        let t = a;
        t = Math.imul(t ^ t >>> 15, t | 1);
        t ^= t + Math.imul(t ^ t >>> 7, t | 61);
        return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
}
const rng = mulberry32(parseInt(args['random-seed'] ?? '42', 10));
function shuffle(arr) {
    for (let i = arr.length - 1; i > 0; i--) {
        const j = Math.floor(rng() * (i + 1));
        [arr[i], arr[j]] = [arr[j], arr[i]];
    }
    return arr;
}

// ────────────────────────────────────────────────────────────────
// Level normalization (weak signal, used as tie-break).
// Valid values (highest-first):
//   МСМК → МС → КМС → I → II → III → I юн → II юн → III юн
// Empty / "б/р" → 0. Anything else → 0 (treated as no rank).
// Adult ranks ALWAYS outrank junior ranks (even III > I юн).
// ────────────────────────────────────────────────────────────────
const LEVEL_WEIGHTS = (() => {
    const m = new Map();
    const add = (weight, ...keys) => keys.forEach(k => m.set(k.toLowerCase(), weight));
    add(9, 'мсмк');
    add(8, 'мс');
    add(7, 'кмс');
    add(6, 'i');
    add(5, 'ii');
    add(4, 'iii');
    add(3, 'i юн');
    add(2, 'ii юн');
    add(1, 'iii юн');
    add(0, 'б/р', '');
    return m;
})();
function normalizeLevel(raw) {
    const t = String(raw ?? '').trim().toLowerCase().replace(/\s+/g, ' ');
    return LEVEL_WEIGHTS.has(t) ? LEVEL_WEIGHTS.get(t) : 0;
}

// ────────────────────────────────────────────────────────────────
// Bracket structure math (mirrors OlympicGroupBracketProcessor.cs)
// ────────────────────────────────────────────────────────────────
function nextPow2(n) {
    let p = 1;
    while (p < n) p <<= 1;
    return p;
}
function olympicStructure(N) {
    const totalCells = nextPow2(N);
    const fullMatches = (2 * N - totalCells) / 2;
    const freeMatches = N - 2 * fullMatches;
    const firstRoundMatches = freeMatches + fullMatches;
    const totalRounds = Math.log2(totalCells);
    return { totalCells, fullMatches, freeMatches, firstRoundMatches, totalRounds };
}

// Slot (1..N) → match index in round 1 (1..firstRoundMatches)
function slotToMatch(slot, freeMatches) {
    if (slot <= freeMatches) return slot;
    return freeMatches + Math.ceil((slot - freeMatches) / 2);
}

// depthOfEncounter: round (1-indexed) where slots a and b first meet
function depthOfEncounter(a, b, N) {
    if (a === b) return 0;
    const { freeMatches, totalRounds } = olympicStructure(N);
    let ma = slotToMatch(a, freeMatches);
    let mb = slotToMatch(b, freeMatches);
    let round = 1;
    while (ma !== mb) {
        ma = Math.ceil(ma / 2);
        mb = Math.ceil(mb / 2);
        round++;
        if (round > totalRounds + 2) return totalRounds; // safety
    }
    return round;
}

// Seeded slots = slots without a "seeded" opponent in round 1.
// These are the slots where favourites should go: all free-winning slots +
// odd-indexed (1st of each pair) slots from the full-matches part.
function computeSeededSlots(N) {
    const { freeMatches } = olympicStructure(N);
    const seeded = [];
    for (let s = 1; s <= N; s++) {
        if (s <= freeMatches) seeded.push(s);
        else if ((s - freeMatches) % 2 === 1) seeded.push(s);
    }
    return seeded;
}
function computeUnseededSlots(N) {
    const seeded = new Set(computeSeededSlots(N));
    const res = [];
    for (let s = 1; s <= N; s++) if (!seeded.has(s)) res.push(s);
    return res;
}

// Order seeded slots so that top-K favourites placed sequentially meet late.
// Classic tennis-draw ordering: top-1 vs top-2 in final, top-3/4 in semis, etc.
// We greedily pick, at each step, the seeded slot with max min-depth to
// already-placed slots.
function bitReverseSeededOrder(N) {
    const seeded = computeSeededSlots(N);
    if (seeded.length === 0) return [];
    const ordered = [seeded[0]];
    const remaining = new Set(seeded.slice(1));
    while (remaining.size > 0) {
        let best = null, bestMin = -1;
        for (const s of remaining) {
            let minDepth = Infinity;
            for (const o of ordered) {
                const d = depthOfEncounter(s, o, N);
                if (d < minDepth) minDepth = d;
            }
            if (minDepth > bestMin || (minDepth === bestMin && (!best || s < best))) {
                bestMin = minDepth;
                best = s;
            }
        }
        ordered.push(best);
        remaining.delete(best);
    }
    return ordered;
}

// ────────────────────────────────────────────────────────────────
// Rating loader — CSV in one of two formats
// ────────────────────────────────────────────────────────────────
function parseCsv(text) {
    const lines = text.split(/\r?\n/).filter(l => l.trim());
    return lines.map(l => {
        // Try semicolon first, fall back to comma
        const sep = l.includes(';') ? ';' : ',';
        return l.split(sep).map(c => c.trim().replace(/^"|"$/g, ''));
    });
}
function loadRating(path) {
    if (!path) return { byId: new Map(), byName: new Map() };
    const rows = parseCsv(readFileSync(path, 'utf8'));
    const byId = new Map();
    const byName = new Map();
    // Detect header
    let start = 0;
    const first = rows[0];
    if (first && !/^\d+(\.\d+)?$/.test(first[first.length - 1])) start = 1;
    for (let i = start; i < rows.length; i++) {
        const r = rows[i];
        if (r.length < 2) continue;
        // Format 1: wrestler_id,rating  (first col is a UUID-like token)
        if (/^[0-9a-f]{8}-/i.test(r[0])) {
            const rating = parseFloat(r[r.length - 1]);
            if (!Number.isNaN(rating)) byId.set(r[0].toLowerCase(), rating);
            continue;
        }
        // Format 2: LastName;FirstName[;BirthYear];rating
        const rating = parseFloat(r[r.length - 1]);
        if (Number.isNaN(rating)) continue;
        const last = (r[0] || '').toLowerCase();
        const first = (r[1] || '').toLowerCase();
        const yearCand = r.length >= 4 ? r[2] : '';
        const year = /^\d{4}$/.test(yearCand) ? yearCand : '';
        const key = `${last}|${first}|${year}`;
        byName.set(key, rating);
        // Also store year-less for looser match
        if (year) byName.set(`${last}|${first}|`, rating);
    }
    return { byId, byName };
}
function lookupRating(w, rating) {
    const byId = rating.byId.get(String(w.ID).toLowerCase());
    if (byId !== undefined) return byId;
    const year = w.BirthDate ? String(w.BirthDate).slice(0, 4) : '';
    const key = `${(w.LastName || '').toLowerCase()}|${(w.FirstName || '').toLowerCase()}|${year}`;
    return rating.byName.get(key) ?? rating.byName.get(`${(w.LastName || '').toLowerCase()}|${(w.FirstName || '').toLowerCase()}|`);
}

// ────────────────────────────────────────────────────────────────
// Enrich wrestlers with conflict-relevant fields
// ────────────────────────────────────────────────────────────────
function enrichWrestlers(wrestlers, teamById, rating) {
    const enriched = wrestlers.map(w => {
        const team = w.TeamID ? teamById.get(w.TeamID) : null;
        return {
            ID: w.ID,
            raw: w,
            fullName: [w.LastName, w.FirstName, w.MiddleName].filter(Boolean).join(' '),
            clubKey: team?.ID ?? null,
            clubName: team?.ShortName || team?.FullName || '',
            cityKey: (team?.City || '').trim().toLowerCase() || null,
            cityName: team?.City || '',
            levelWeight: normalizeLevel(w.Level),
            levelRaw: w.Level || '',
            rating: lookupRating(w, rating),
        };
    });
    // Assign "ratingRank": 1 = strongest (highest rating), Infinity if unrated.
    const rated = enriched.filter(e => e.rating !== undefined && e.rating !== null && !Number.isNaN(e.rating));
    rated.sort((a, b) => b.rating - a.rating || b.levelWeight - a.levelWeight);
    rated.forEach((e, i) => e.ratingRank = i + 1);
    for (const e of enriched) if (e.ratingRank === undefined) e.ratingRank = Infinity;
    // combinedRank: rating if present, else by levelWeight + name (stable).
    enriched.sort((a, b) => {
        if (a.ratingRank !== b.ratingRank) return a.ratingRank - b.ratingRank;
        if (b.levelWeight !== a.levelWeight) return b.levelWeight - a.levelWeight;
        return a.fullName.localeCompare(b.fullName, 'ru');
    });
    enriched.forEach((e, i) => e.combinedRank = i + 1);
    return enriched;
}

// ────────────────────────────────────────────────────────────────
// Conflict weight between two wrestlers (how much we dislike seeing them
// meet early). Multiplied by (1/depth) at cost time.
// ────────────────────────────────────────────────────────────────
const W_CLUB = 10000, W_CITY = 500, W_RATING = 50, W_LEVEL = 5;
function pairConflict(a, b) {
    let w = 0;
    if (a.clubKey && a.clubKey === b.clubKey) w += W_CLUB;
    if (a.cityKey && a.cityKey === b.cityKey && a.clubKey !== b.clubKey) w += W_CITY;
    // Rating: penalize top-K meeting each other early
    if (a.ratingRank <= 8 && b.ratingRank <= 8) {
        // favour pairs where both are top-4 more strongly
        const top = Math.max(a.ratingRank, b.ratingRank);
        w += W_RATING * (9 - top); // top-1/2: weight 8, top-3/4: 6, ...
    }
    // Level: two high-level wrestlers meeting early is undesirable
    const lvl = Math.min(a.levelWeight, b.levelWeight);
    if (lvl >= 2) w += W_LEVEL * lvl;
    return w;
}

// ────────────────────────────────────────────────────────────────
// Depth weight: exponential decay so that early-round encounters dominate
// the cost. A round-1 pair is 2× worse than round-2, 4× worse than round-3,
// and so on — matches the intuition that any early meeting of two same-city
// wrestlers wastes the point of spreading them at all.
// ────────────────────────────────────────────────────────────────
function depthWeight(depth, N) {
    if (depth <= 0) return 0;
    const totalRounds = Math.ceil(Math.log2(nextPow2(N)));
    return Math.pow(2, Math.max(0, totalRounds - depth));
}

// ────────────────────────────────────────────────────────────────
// Cost over a placement (slot → wrestler index)
// ────────────────────────────────────────────────────────────────
function placementCost(slots, wrestlers, N, depthFn) {
    let cost = 0;
    for (let a = 1; a <= N; a++) {
        const wa = slots[a];
        if (!wa) continue;
        for (let b = a + 1; b <= N; b++) {
            const wb = slots[b];
            if (!wb) continue;
            const d = depthFn(a, b, N);
            if (d === 0) continue;
            const conflict = pairConflict(wa, wb);
            if (conflict > 0) cost += conflict * depthWeight(d, N);
        }
    }
    return cost;
}

// ────────────────────────────────────────────────────────────────
// Olympic seeding: initial placement + local swap search
// ────────────────────────────────────────────────────────────────
function seedOlympic(wrestlers, N, locks) {
    const slots = new Array(N + 1).fill(null); // 1-indexed
    const lockedSlots = new Set();
    const lockedIds = new Set();
    for (const [wid, seed] of locks) {
        const w = wrestlers.find(x => x.ID === wid);
        if (!w || seed < 1 || seed > N || lockedSlots.has(seed)) continue;
        slots[seed] = w;
        lockedSlots.add(seed);
        lockedIds.add(wid);
    }
    const remaining = wrestlers.filter(w => !lockedIds.has(w.ID)).sort((a, b) => a.combinedRank - b.combinedRank);

    // Place favourites into seeded slots (in bit-reverse order)
    const favOrder = bitReverseSeededOrder(N);
    let idx = 0;
    for (const slot of favOrder) {
        if (lockedSlots.has(slot)) continue;
        while (idx < remaining.length && slots[slot] === null) {
            slots[slot] = remaining[idx++];
            break;
        }
    }
    // Place rest into unseeded slots + remaining seeded (in name order for determinism,
    // then greedy by club-load).
    const freeSlots = [];
    for (let s = 1; s <= N; s++) if (!slots[s]) freeSlots.push(s);
    // Greedy: for each free slot, pick a remaining wrestler minimizing conflict with
    // already-placed neighbours at shallow depths.
    const leftover = remaining.slice(idx);
    shuffle(leftover); // determinism via rng
    for (const s of freeSlots) {
        if (leftover.length === 0) break;
        let bestI = 0, bestDelta = Infinity;
        for (let i = 0; i < leftover.length; i++) {
            slots[s] = leftover[i];
            let local = 0;
            for (let t = 1; t <= N; t++) {
                if (t === s || !slots[t]) continue;
                const d = depthOfEncounter(s, t, N);
                if (d === 0) continue;
                local += pairConflict(slots[s], slots[t]) * depthWeight(d, N);
            }
            if (local < bestDelta) { bestDelta = local; bestI = i; }
            slots[s] = null;
        }
        slots[s] = leftover[bestI];
        leftover.splice(bestI, 1);
    }
    // Local search: swap pairs, then 3-cycles, until no improvement.
    // 3-cycles can escape the pair-swap local minimum (e.g. three same-city
    // wrestlers where any two-swap doesn't reduce cost but a rotation does).
    let cost = placementCost(slots, wrestlers, N, depthOfEncounter);
    for (let iter = 0; iter < 500; iter++) {
        let improved = false;
        for (let a = 1; a <= N; a++) {
            if (lockedSlots.has(a)) continue;
            for (let b = a + 1; b <= N; b++) {
                if (lockedSlots.has(b)) continue;
                [slots[a], slots[b]] = [slots[b], slots[a]];
                const newCost = placementCost(slots, wrestlers, N, depthOfEncounter);
                if (newCost < cost - 1e-9) {
                    cost = newCost;
                    improved = true;
                } else {
                    [slots[a], slots[b]] = [slots[b], slots[a]];
                }
            }
        }
        if (improved) continue;
        // 3-cycle: slot a gets what's in b, b gets c's, c gets a's. Try both rotation
        // directions. Only worth doing when pair-swap can't improve further.
        for (let a = 1; a <= N && !improved; a++) {
            if (lockedSlots.has(a)) continue;
            for (let b = a + 1; b <= N && !improved; b++) {
                if (lockedSlots.has(b)) continue;
                for (let c = b + 1; c <= N && !improved; c++) {
                    if (lockedSlots.has(c)) continue;
                    const [wa, wb, wc] = [slots[a], slots[b], slots[c]];
                    slots[a] = wb; slots[b] = wc; slots[c] = wa;
                    let newCost = placementCost(slots, wrestlers, N, depthOfEncounter);
                    if (newCost < cost - 1e-9) { cost = newCost; improved = true; continue; }
                    slots[a] = wc; slots[b] = wa; slots[c] = wb;
                    newCost = placementCost(slots, wrestlers, N, depthOfEncounter);
                    if (newCost < cost - 1e-9) { cost = newCost; improved = true; continue; }
                    slots[a] = wa; slots[b] = wb; slots[c] = wc;
                }
            }
        }
        if (!improved) break;
    }
    const placement = new Map();
    for (let s = 1; s <= N; s++) placement.set(slots[s].ID, s);
    return { placement, finalCost: cost };
}

// ────────────────────────────────────────────────────────────────
// RoundRobin: pairing doesn't depend on SeedNumber; seed only drives tie-break.
// Simply order strongest-first.
// ────────────────────────────────────────────────────────────────
function seedRoundRobin(wrestlers, N, locks) {
    const slots = new Array(N + 1).fill(null);
    const lockedSlots = new Set();
    const lockedIds = new Set();
    for (const [wid, seed] of locks) {
        const w = wrestlers.find(x => x.ID === wid);
        if (!w || seed < 1 || seed > N || lockedSlots.has(seed)) continue;
        slots[seed] = w;
        lockedSlots.add(seed);
        lockedIds.add(wid);
    }
    const rest = wrestlers.filter(w => !lockedIds.has(w.ID)).sort((a, b) => a.combinedRank - b.combinedRank);
    let i = 0;
    for (let s = 1; s <= N; s++) {
        if (slots[s]) continue;
        slots[s] = rest[i++];
    }
    const placement = new Map();
    for (let s = 1; s <= N; s++) placement.set(slots[s].ID, s);
    return { placement, finalCost: 0 };
}

// ────────────────────────────────────────────────────────────────
// SubGroupsIntoOlympic: processor splits wrestlers into subgroup A (top by
// SeedNumber) and subgroup B (bottom). Inside each subgroup — round-robin, so
// conflicting pairs WILL meet at round 1. Therefore goal here is to minimise
// conflicts *within* each subgroup, not along a bracket tree.
//
// A = seeds 1..kA   (kA = 4 if N==7 else 3)
// B = seeds (N-kB+1)..N  (kB = 3)
// M = middle seeds (only exist when kA+kB < N, i.e. N=8)
//
// Strategy: enumerate every feasible (A-set, B-set, M-set) partition of
// remaining wrestlers respecting locks, pick the one with minimum sum of
// pairConflict within A plus within B. For N ≤ 8 this is at most C(8,3)*C(5,3)
// = 560 combinations, trivial to enumerate.
// ────────────────────────────────────────────────────────────────
function seedSubGroups(wrestlers, N, locks) {
    const kA = N === 7 ? 4 : 3;
    const kB = 3;
    const kM = Math.max(0, N - kA - kB);
    const slotsA = []; for (let s = 1; s <= kA; s++) slotsA.push(s);
    const slotsB = []; for (let s = N; s > N - kB; s--) slotsB.push(s);
    const slotsM = []; for (let s = kA + 1; s <= N - kB; s++) slotsM.push(s);

    // Apply locks first — a locked wrestler is forced into whichever subgroup
    // their seed falls into; only the free wrestlers and free slots get
    // permuted.
    const lockedIds = new Set();
    const lockedInA = [], lockedInB = [], lockedInM = [];
    const freeSlotsA = [...slotsA], freeSlotsB = [...slotsB], freeSlotsM = [...slotsM];
    for (const [wid, seed] of locks) {
        const w = wrestlers.find(x => x.ID === wid);
        if (!w || seed < 1 || seed > N) continue;
        if (seed <= kA) { lockedInA.push({ w, seed }); freeSlotsA.splice(freeSlotsA.indexOf(seed), 1); }
        else if (seed > N - kB) { lockedInB.push({ w, seed }); freeSlotsB.splice(freeSlotsB.indexOf(seed), 1); }
        else { lockedInM.push({ w, seed }); freeSlotsM.splice(freeSlotsM.indexOf(seed), 1); }
        lockedIds.add(wid);
    }
    const free = wrestlers.filter(w => !lockedIds.has(w.ID));

    // Subgroup-internal conflict cost. All pairs within a subgroup meet in
    // round 1, so there's no depth scaling here.
    const subgroupCost = (members) => {
        let c = 0;
        for (let i = 0; i < members.length; i++)
            for (let j = i + 1; j < members.length; j++)
                c += pairConflict(members[i], members[j]);
        return c;
    };

    // Enumerate partitions of `free` into (A-free, B-free, M-free) of sizes
    // matching the free-slot counts.
    const needA = freeSlotsA.length, needB = freeSlotsB.length, needM = freeSlotsM.length;
    if (needA + needB + needM !== free.length) {
        // fall back: shouldn't happen in practice
        return seedRoundRobin(wrestlers, N, locks);
    }

    let best = null; // { aSet: [], bSet: [], mSet: [], cost: number }
    const indices = free.map((_, i) => i);
    const chooseCombinations = function* (pool, k) {
        if (k === 0) { yield []; return; }
        if (k > pool.length) return;
        const idx = new Array(k);
        for (let i = 0; i < k; i++) idx[i] = i;
        while (true) {
            yield idx.map(i => pool[i]);
            let i = k - 1;
            while (i >= 0 && idx[i] === pool.length - k + i) i--;
            if (i < 0) break;
            idx[i]++;
            for (let j = i + 1; j < k; j++) idx[j] = idx[j - 1] + 1;
        }
    };

    for (const aPick of chooseCombinations(indices, needA)) {
        const remAfterA = indices.filter(i => !aPick.includes(i));
        for (const bPick of chooseCombinations(remAfterA, needB)) {
            const mPick = remAfterA.filter(i => !bPick.includes(i));
            const aMembers = [...lockedInA.map(x => x.w), ...aPick.map(i => free[i])];
            const bMembers = [...lockedInB.map(x => x.w), ...bPick.map(i => free[i])];
            const cost = subgroupCost(aMembers) + subgroupCost(bMembers);
            if (!best || cost < best.cost) {
                best = {
                    aFreeWrestlers: aPick.map(i => free[i]),
                    bFreeWrestlers: bPick.map(i => free[i]),
                    mFreeWrestlers: mPick.map(i => free[i]),
                    cost,
                };
                if (cost === 0) break; // perfect split found
            }
        }
        if (best && best.cost === 0) break;
    }
    if (!best) {
        // Degenerate (e.g. all free list empty because all locked) — synthesize empty split
        best = { aFreeWrestlers: [], bFreeWrestlers: [], mFreeWrestlers: [], cost: 0 };
    }

    // Order within each subgroup: top combinedRank first (so top-1 → seed 1 in A,
    // top-2 → seed N in B, alternating via rank).
    const sortRank = arr => arr.sort((a, b) => a.combinedRank - b.combinedRank);
    const aFinal = sortRank(best.aFreeWrestlers);
    const bFinal = sortRank(best.bFreeWrestlers); // best (lowest combinedRank) → highest-priority B seat
    const mFinal = sortRank(best.mFreeWrestlers);

    const slots = new Array(N + 1).fill(null);
    for (const { w, seed } of lockedInA) slots[seed] = w;
    for (const { w, seed } of lockedInB) slots[seed] = w;
    for (const { w, seed } of lockedInM) slots[seed] = w;
    for (const s of freeSlotsA) { slots[s] = aFinal.shift(); }
    // B seats are ordered N, N-1, N-2 so the strongest remaining B wrestler
    // (bFinal[0]) goes into the seat with highest number — which is the
    // "bottom seed", but within the subgroup it's still just a participant.
    for (const s of freeSlotsB) { slots[s] = bFinal.shift(); }
    for (const s of freeSlotsM) { slots[s] = mFinal.shift(); }

    const placement = new Map();
    for (let s = 1; s <= N; s++) if (slots[s]) placement.set(slots[s].ID, s);
    return { placement, finalCost: best.cost };
}

// ────────────────────────────────────────────────────────────────
// Group-level orchestration
// ────────────────────────────────────────────────────────────────
const BRACKET_DEFAULTS = { 5: 'RoundRobin', 7: 'SubGroupsIntoOlympic' };
function defaultBracketTypeByN(N) {
    if (N <= 5) return 'RoundRobin';
    if (N <= 7) return 'SubGroupsIntoOlympic';
    return 'OlympicConsilationFinalists';
}
function groupLabel(group) {
    const gender = group.IsFemale ? 'Ж' : 'М';
    const years = group.BirthYearMin === group.BirthYearMax
        ? String(group.BirthYearMin ?? '—')
        : `${group.BirthYearMin ?? '—'}-${group.BirthYearMax ?? '—'}`;
    const weight = group.WeightMax ? `до ${group.WeightMax} кг` : '';
    return `${gender} ${years} ${weight}`.trim();
}

function planGroup(group, enrichedById, opts) {
    const wrestlerIds = group.Wrestlers || [];
    const N = wrestlerIds.length;
    const label = groupLabel(group);
    const baseReport = { groupId: group.ID, label, N, bracketCode: null, status: null, assignments: [], warnings: [], notes: [] };

    if (N < 2) {
        baseReport.status = 'skip';
        baseReport.notes.push(N === 0 ? 'Группа пуста' : 'Только один участник — жеребьёвка не требуется');
        return baseReport;
    }
    const bracket = group.Bracket;
    const completed = bracket?.CompletedMatchesCount ?? 0;
    const bracketCode = bracket?.BracketTypeCode || defaultBracketTypeByN(N);
    baseReport.bracketCode = bracketCode;
    baseReport.currentCompletedMatches = completed;

    if (completed > 0 && !opts.forceGroups.has(group.ID)) {
        baseReport.status = 'skip';
        baseReport.warnings.push(`Пропущена: сыграно ${completed} из ${bracket?.MatchesCount ?? '?'} матчей. Передайте ID в --force, чтобы перезаписать.`);
        return baseReport;
    }
    const willForce = completed > 0 && opts.forceGroups.has(group.ID);

    const wrestlers = wrestlerIds.map(id => enrichedById.get(id)).filter(Boolean);
    if (wrestlers.length !== N) {
        baseReport.status = 'skip';
        baseReport.warnings.push(`Потеряно ${N - wrestlers.length} ссылок на спортсменов (несогласованность .wrt)`);
        return baseReport;
    }
    const locks = new Map();
    for (const [wid, seed] of opts.locks) {
        if (wrestlers.some(w => w.ID === wid)) locks.set(wid, seed);
    }

    let result;
    if (bracketCode === 'RoundRobin') result = seedRoundRobin(wrestlers, N, locks);
    else if (bracketCode === 'SubGroupsIntoOlympic') result = seedSubGroups(wrestlers, N, locks);
    else result = seedOlympic(wrestlers, N, locks);

    // Build assignments
    const assignments = wrestlers.map(w => ({
        wrestlerId: w.ID,
        name: w.fullName,
        team: w.clubName,
        city: w.cityName,
        level: w.levelRaw,
        ratingRank: Number.isFinite(w.ratingRank) ? w.ratingRank : null,
        seed: result.placement.get(w.ID),
        locked: locks.has(w.ID),
    }));
    assignments.sort((a, b) => a.seed - b.seed);
    baseReport.assignments = assignments;
    baseReport.status = willForce ? 'force' : 'ok';
    if (willForce) baseReport.warnings.push('Сетка была с результатами — матчи будут стёрты (--force)');

    // Diagnostics: conflicts by depth
    const clubConflicts = [];
    const cityConflicts = [];
    if (bracketCode !== 'RoundRobin') {
        const seededByClub = new Map();
        for (const a of assignments) {
            if (a.team) {
                (seededByClub.get(a.team) ?? seededByClub.set(a.team, []).get(a.team)).push(a);
            }
        }
        for (const [clubName, members] of seededByClub) {
            if (members.length < 2) continue;
            let minDepth = Infinity;
            for (let i = 0; i < members.length; i++) {
                for (let j = i + 1; j < members.length; j++) {
                    const d = bracketCode === 'SubGroupsIntoOlympic'
                        ? (sameSubgroup(members[i].seed, members[j].seed, N) ? 1 : 4)
                        : depthOfEncounter(members[i].seed, members[j].seed, N);
                    if (d < minDepth) minDepth = d;
                }
            }
            clubConflicts.push({ team: clubName, count: members.length, minEncounterRound: minDepth });
        }
        const seededByCity = new Map();
        for (const a of assignments) {
            if (a.city) {
                const key = a.city.toLowerCase();
                (seededByCity.get(key) ?? seededByCity.set(key, { name: a.city, members: [] }).get(key)).members.push(a);
            }
        }
        for (const { name, members } of seededByCity.values()) {
            if (members.length < 2) continue;
            let minDepth = Infinity;
            for (let i = 0; i < members.length; i++) {
                for (let j = i + 1; j < members.length; j++) {
                    const d = bracketCode === 'SubGroupsIntoOlympic'
                        ? (sameSubgroup(members[i].seed, members[j].seed, N) ? 1 : 4)
                        : depthOfEncounter(members[i].seed, members[j].seed, N);
                    if (d < minDepth) minDepth = d;
                }
            }
            cityConflicts.push({ city: name, count: members.length, minEncounterRound: minDepth });
        }
    }
    baseReport.diagnostics = { clubConflicts, cityConflicts, finalCost: Number(result.finalCost.toFixed(2)) };
    return baseReport;
}
function sameSubgroup(seedA, seedB, N) {
    const kA = N === 7 ? 4 : 3;
    const kB = 3;
    const inA = s => s >= 1 && s <= kA;
    const inB = s => s > N - kB && s <= N;
    return (inA(seedA) && inA(seedB)) || (inB(seedA) && inB(seedB));
}

// ────────────────────────────────────────────────────────────────
// Main
// ────────────────────────────────────────────────────────────────
const wrt = JSON.parse(readFileSync(args.wrt, 'utf8'));
const rating = loadRating(args.rating);
const teamById = new Map(wrt.TeamApplications.map(t => [t.ID, t]));
const wrestlerById = new Map(wrt.Wrestlers.map(w => [w.ID, w]));

const enriched = enrichWrestlers(wrt.Wrestlers, teamById, rating);
const enrichedById = new Map(enriched.map(e => [e.ID, e]));

const groupFilter = args.groups ? new Set(args.groups.split(',').map(s => s.trim())) : null;
const forceGroups = args.force ? new Set(args.force.split(',').map(s => s.trim())) : new Set();
const locks = new Map();
if (args.lock) {
    for (const tok of args.lock.split(',')) {
        const [wid, seedStr] = tok.split(':');
        const seed = parseInt(seedStr, 10);
        if (wid && Number.isFinite(seed)) locks.set(wid.trim(), seed);
    }
}
const opts = { forceGroups, locks };

const reports = [];
for (const g of wrt.Groups) {
    if (groupFilter && !groupFilter.has(g.ID)) continue;
    reports.push(planGroup(g, enrichedById, opts));
}

// ────────────────────────────────────────────────────────────────
// Write plan JSON + text report
// ────────────────────────────────────────────────────────────────
const plan = {
    generatedAt: new Date().toISOString(),
    wrt: args.wrt,
    rating: args.rating || null,
    randomSeed: parseInt(args['random-seed'] ?? '42', 10),
    groups: reports,
};
mkdirSync(dirname(args.out), { recursive: true });
writeFileSync(args.out, JSON.stringify(plan, null, 2), 'utf8');

// ────────────────────────────────────────────────────────────────
// Pretty text report to stdout
// ────────────────────────────────────────────────────────────────
const totals = { ok: 0, skip: 0, force: 0 };
for (const r of reports) totals[r.status] = (totals[r.status] || 0) + 1;

console.log(`План жеребьёвки: ${args.wrt}`);
console.log(`Групп в отчёте: ${reports.length}   обработано: ${totals.ok}   пропущено: ${totals.skip}   с --force: ${totals.force}`);
if (args.rating) console.log(`Рейтинг: ${args.rating}  (ID-match: ${rating.byId.size}, name-match: ${rating.byName.size})`);
console.log('');

for (const r of reports) {
    const tag = r.status === 'skip' ? '[SKIP]' : r.status === 'force' ? '[FORCE]' : '[OK]';
    console.log(`${tag} ${r.label} · N=${r.N} · ${r.bracketCode}`);
    if (r.status === 'skip') {
        for (const w of r.warnings) console.log(`   ! ${w}`);
        for (const n of r.notes) console.log(`   · ${n}`);
        continue;
    }
    for (const w of r.warnings) console.log(`   ! ${w}`);
    const cc = r.diagnostics?.clubConflicts || [];
    const sc = r.diagnostics?.cityConflicts || [];
    if (cc.length) {
        console.log('   Клубы с несколькими участниками:');
        for (const c of cc.sort((a, b) => a.minEncounterRound - b.minEncounterRound)) {
            const where = c.minEncounterRound === 1 ? '1-й круг ⚠' : `раунд ${c.minEncounterRound}`;
            console.log(`     · ${c.team} (${c.count}): встреча не раньше ${where}`);
        }
    }
    if (sc.length) {
        console.log('   Города с несколькими участниками:');
        for (const c of sc.sort((a, b) => a.minEncounterRound - b.minEncounterRound)) {
            const where = c.minEncounterRound === 1 ? '1-й круг' : `раунд ${c.minEncounterRound}`;
            console.log(`     · ${c.city} (${c.count}): встреча не раньше ${where}`);
        }
    }
    const topFavs = r.assignments.filter(a => a.ratingRank !== null).sort((a, b) => a.ratingRank - b.ratingRank).slice(0, 4);
    if (topFavs.length) {
        console.log('   Фавориты:');
        for (const f of topFavs) {
            console.log(`     top-${f.ratingRank} → seed ${f.seed}: ${f.name} (${f.team})`);
        }
    }
    console.log('   Расстановка:');
    for (const a of r.assignments) {
        const lock = a.locked ? ' [LOCK]' : '';
        const rank = a.ratingRank !== null ? ` top-${a.ratingRank}` : '';
        console.log(`     #${String(a.seed).padStart(2)} ${a.name}${lock}${rank} — ${a.team}${a.city ? ', ' + a.city : ''}${a.level ? ' · ' + a.level : ''}`);
    }
    console.log('');
}

console.log(`План сохранён в ${args.out}`);
console.log(`Запустите apply.mjs --wrt "${args.wrt}" --plan "${args.out}" для применения.`);

# Rating input and seeding behavior

Supported CSV formats:

- wrestler_id,rating (UUID identifiers preferred).
- LastName;FirstName;rating or LastName;FirstName;BirthYear;rating.

Inspect loadRating and enrichWrestlers in ../scripts/plan.mjs when mapping unfamiliar data. Higher numeric ratings rank first. Convert finishing places (1 = best) to a higher-is-better score in a temporary CSV before planning. UUID matching is preferred; name fallback can conflate same-name wrestlers, including its yearless fallback. Report unmatched or ambiguous identities.

IsSeedFixed=true prevents UI reshuffling, but is ignored as a lock by this script. Use --lock for explicit positions. Club separation takes priority over city, rating, and Level; inspect the proposed assignments rather than promising that every favorite meets only in the final.

The implementation notes below came from the Claude skill. Check current processors if their behavior has changed; avoid fixed source line numbers.

## Типы сеток и логика расстановки

### Olympic / OlympicConsolationFinalists (N ≥ 4)

Pairing в первом раунде (см. `OlympicGroupBracketProcessor.cs:198-246`):
- `totalCells = next_pow2(N)`, `fullMatches = (2N-totalCells)/2`, `freeMatches = N - 2*fullMatches`
- Slots 1..freeMatches — свободные победы
- Slots freeMatches+1..N — попарно: `(fm+1, fm+2), (fm+3, fm+4), ...`

Дерево встреч строится **по соседним парам матчей первого раунда**. Функция глубины встречи `depthOfEncounter(seedA, seedB, N)` возвращает номер раунда, в котором два слота могут впервые сойтись (1 — первый круг, log2(totalCells) — финал).

Алгоритм расстановки:
1. Вычислить **seeded slots** — позиции, противник которых в первом раунде либо free-winner, либо не-seeded. Это даёт ровно 2^⌈log2(N)⌉ / 2 позиций "для посева".
2. Фаворитов (top-K по рейтингу) расставить по seeded slots в порядке bit-reverse: top-1 → самая "одинокая" seed-позиция (slot 1), top-2 → самая дальняя от неё по дереву (финал), top-3 и top-4 — в противоположные полуфинальные ветви, и т.д.
3. Остальных спортсменов — в unseeded slots + оставшиеся seeded slots.
4. **Greedy local search**: swap-пары, если обмен уменьшает cost-функцию:
   ```
   cost = Σ (clubPenalty * 10000 / depth)
        + Σ (cityPenalty *   500 / depth)
        + Σ (ratingPenalty *   50 / depth)
        + Σ (levelPenalty *    5 / depth)
   ```
   `--random-seed` задаёт воспроизводимый генератор случайных чисел, а не число итераций.

### RoundRobin (N ≤ 5)

Все играют со всеми; `ShuffleWrestlers()` внутри процессора всё равно перемешает. SeedNumber влияет только на tie-break при подсчёте мест (`RoundRobinGroupBracketProcessor.cs:56`).

Скилл просто упорядочивает: сильнейший (по рейтингу + Level) → seed 1, далее по убыванию.

### SubGroupsIntoOlympic (N = 6-8)

Разделение по подгруппам (`SubGroupsToOlympicBracketProcessor.cs:22-35`):
- Подгруппа A = `OrderBy(SeedNumber).Take(N==7 ? 4 : 3)` — top seed'ы
- Подгруппа B = `OrderByDescending(SeedNumber).Take(3)` — bottom seed'ы
- Внутри — RoundRobin, победители в полуфинале

Скилл чередует фаворитов: top-1 → A(1), top-2 → B(N), top-3 → A(2), top-4 → B(N-1)… Это гарантирует что двое лучших встретятся в полуфинале, а не в одной подгруппе. Одноклубники также разводятся между A и B, где возможно.

**Замечание** для N=8: процессор забирает только 3+3=6 спортсменов, 2 выпадают — это особенность кода WrestlingAdmin. Скилл корректно проставит seed'ы всем 8, но user решает использовать SubGroups или Olympic для N=8.

## Edge cases

- N=0 или N=1 — группа пропускается с INFO.
- Все `IsSeedFixed=true` в файле — игнорируется, скилл всё равно переставляет всех (если нужен lock — `--lock`).
- Спортсмен без `TeamID` — трактуется как "без клуба" (не участвует в clubPenalty).
- Команда без `City` — не участвует в cityPenalty.
- `rating.csv` не содержит часть спортсменов — они ставятся в unseeded slots после ранжированных.
- `group.Bracket == null` — default тип сетки по N (≤5 RoundRobin, 6-7 SubGroups, ≥8 OlympicConsolationFinalists).

---
name: draw-seeding
description: Провести жеребьёвку турнира в `.wrt`-файле WrestlingAdmin **с учётом внешнего рейтинга** (CSV с рейтингом или местами прошлых турниров). Расставляет SeedNumber так, чтобы фавориты по рейтингу встретились максимально поздно (в дополнение к разведению по клубу/городу/Level). Для жеребьёвки **без рейтинга** — используй встроенную логику в самом WrestlingAdmin (`ClubCityLevelSeedingStrategy`), просто открой файл в приложении и нажми "Пересоздать все сетки". Скилл нужен только для CSV-рейтинга. Атомарная запись с бэкапом. Используй когда пользователь передаёт CSV с рейтингом / местами прошлых турниров и хочет учесть его в посеве.
---

# draw-seeding

Workflow для проведения жеребьёвки (посева) **с внешним рейтингом** в `.wrt`-файле WrestlingAdmin.

> **Важно.** Базовая логика "разведение по клубу / городу / Level" уже встроена в сам WrestlingAdmin (`Wrestling.Entities/Bracket/Seeding/ClubCityLevelSeedingStrategy.cs`) и работает автоматически при нажатии "Пересоздать все сетки" / "Создать сетку". Скилл нужен **только** когда есть внешний рейтинг (CSV) — он дополнительно расставляет фаворитов по сетке, чтобы они встречались в финале/полуфинале.

## Когда использовать

Пользователь просит:
- "провести жеребьёвку с учётом рейтинга"
- "расставь фаворитов так, чтобы они встретились в финале"
- передаёт CSV с рейтингом или местами прошлых турниров + `.wrt`

Если рейтинга нет — скажи пользователю, что встроенной логики достаточно: открой `.wrt` в приложении → "Жеребьёвка" → "Пересоздать все сетки".

## Входные данные

Уточняй через AskUserQuestion **только если не предоставлено в диалоге**:

1. **Путь к `.wrt`** — абсолютный.
2. **Рейтинг/прошлые результаты (опционально)** — CSV в одном из форматов:
   - `wrestler_id,rating` — по `Wrestler.ID` (предпочтительно)
   - `LastName;FirstName;rating` или `LastName;FirstName;BirthYear;rating` — fallback через ФИО
   
   Если рейтинга нет — скилл разводит только по клубам/городам/уровню (`Wrestler.Level`).
3. **Подмножество групп (опционально)** — список `Group.ID` или фильтр по весу/возрасту. По умолчанию обрабатываются все группы без завершённых матчей.

## Критическая семантика `IsSeedFixed`

`IsSeedFixed=true` — это **директива UI** (`DrawViewModel.SeedWrestlers`) не перетасовывать спортсмена при повторной жеребьёвке. Скилл:

- **игнорирует** текущее значение `IsSeedFixed` в файле (это не lock для скилла)
- **переставляет всех** спортсменов группы с нуля
- **безусловно ставит** `IsSeedFixed=true` всем обработанным спортсменам

Если нужно закрепить конкретного спортсмена на конкретной позиции, используй `--lock=<wrestlerId:seed,...>`. Это единственный способ "ручной фиксации" для скилла.

## Иерархия приоритетов разведения

От жёсткого к мягкому:

1. **Клуб** (`TeamID`) — одноклубники встречаются максимально поздно
2. **Город** (`TeamApplication.City` по `TeamID`) — земляки разнесены, но слабее
3. **Рейтинг** — top-N фавориты расставляются через "bit-reverse" по seeded-слотам, встречаются в финале/полуфинале
4. **Level** (`Wrestler.Level` — `МС`, `КМС`, `I юн`, `II юн`…) — самый слабый сигнал, tie-break

Нормализация `Level` → вес: `МСМК, МС → 5`, `КМС → 4`, `I → 3`, `II → 2`, `III → 1`, `б/р → 0`.

## Шаги

### 1. План (dry-run)

```
node .claude/skills/draw-seeding/scripts/plan.mjs \
  --wrt <target.wrt> \
  [--rating <rating.csv>] \
  [--groups <groupId,groupId,...>] \
  [--force <groupId,...>] \
  [--lock <wrestlerId:seed,...>] \
  [--random-seed 42] \
  --out <workdir>/seeding_plan.json
```

Читает `.wrt`, строит план расстановки, пишет `seeding_plan.json` и выводит текстовый отчёт по каждой группе: распределение одноклубников по слотам, фавориты, предсказанные встречи одноклубников по раундам, предупреждения.

**Safety**: группы с `Bracket.CompletedMatchesCount > 0` пропускаются с WARN. Чтобы всё равно пересеять такую группу, передай её ID в `--force` (сыгранные матчи будут стёрты — это осознанное действие пользователя).

### 2. Покажи отчёт пользователю

Перед apply дай пользователю увидеть:
- Общая статистика: обработано групп / пропущено
- Для каждой группы: N, тип сетки, клубы (с числом спортсменов), фавориты, ожидаемая минимальная глубина встречи одноклубников (раунд)
- Предупреждения: например если в маленькой группе (N=4) 3 одноклубника — физически невозможно развести идеально, скилл показывает best-effort

Не вываливай сырой JSON. Для каждой проблемной группы кратко объясни компромиссы.

### 3. Подтверждение через AskUserQuestion

Если пользователь хочет подправить — отредактируй `seeding_plan.json` напрямую (перестановки `assignments[]` → `seed`) и повтори шаг 2.

### 4. Применение

```
node .claude/skills/draw-seeding/scripts/apply.mjs \
  --wrt <target.wrt> \
  --plan <workdir>/seeding_plan.json
```

Три защиты (как `TournamentDataAccess.SaveToFile`):

1. **Pre-save backup** → `<wrt-dir>/Backups/<filename>/<yyyyMMdd_HHmmss_fff>.wrt`
2. **Atomic write**: serialize → `<wrt>.tmp.<uuid>` → `rename`
3. **Post-write verify**: re-parse + проверка инвариантов (`SeedNumber` 1..N без дыр и дубликатов в каждой группе, все `IsSeedFixed=true` у обработанных). При ошибке — восстановление из бэкапа.

### 5. Проверка в UI

Пользователь открывает `.wrt` в WrestlingAdmin → вкладка "Жеребьёвка":
- Все чекбоксы "Фикс." в плане включены
- SeedNumber 1..N выставлены
- Нажимает **"Пересоздать все сетки"** → поскольку все `IsSeedFixed=true`, `SeedWrestlers` не перетасует, `IGroupBracketProcessor.Generate` построит bracket строго по расстановке скилла

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
   Работает `--random-seed` итераций до сходимости.

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

## Критические файлы кода WrestlingAdmin

| Файл | Что используется |
|------|---|
| `Wrestling.Entities/Bracket/OlympicGroupBracketProcessor.cs:198-246` | Pairing формулы `totalCells`/`fullMatches`/`freeMatches` |
| `Wrestling.Entities/Bracket/SubGroupsToOlympicBracketProcessor.cs:22-35` | Разбиение по подгруппам A/B по SeedNumber |
| `Wrestling.Entities/Bracket/RoundRobinGroupBracketProcessor.cs:94-147` | RoundRobin (SeedNumber на pairing не влияет) |
| `Wrestling.UI.Material/Tournament/Standing/Draw/DrawViewModel.cs:276-310` | Инвариант `SeedNumber` 1..N, семантика `IsSeedFixed` |
| `.claude/skills/register-wrestlers/scripts/apply_plan.mjs` | Reference для atomic write + backup |

## Артефакты после прогона

- `<workdir>/seeding_plan.json` — план расстановки (assignments, warnings, stats)
- `<wrt-dir>/Backups/<filename>/<timestamp>.wrt` — pre-save backup
- Изменённый `.wrt` файл

`seeding_plan.json` — временный артефакт. Держи в `tmp/` вне git-индекса.

# Post-tournament punch-list — 2026-04-28

Проблемы, выявленные пользователем по итогам реального турнира. Решаем по очереди.

## Сквозная архитектурная тема (исторически)

Изначально предполагалось, что #1, #3, #5, #10 потребуют отдельного админ-канала push-обновлений. Это снято: после версионной модели #14 + расширения `TournamentImporter.Apply` обычный pull-импорт умеет разносить структурные правки (`FieldsVersion` для тайминга/возраста/веса/`MatID`, `BracketVersion` для замены сетки целиком, `SyncWrestlers` для новых регистраций). Дополнительный канал не нужен — секретарь редактирует у себя, бампит версию, пиры подтягивают на следующем тике.

Открыта только #3 в части UX «опубликовать сейчас» (принудительный flush вместо ожидания тика) — но она перекрыта существующим импортом по существу.

---

## Done

Из исходного списка:
- ✅ **#2** Кнопки `−1с` / `+1с` у таймера на экране управления матчем + клампы `[0; MaxRoundSecond]` (либо `MaxTimeoutSecond` в перерыве). Лейблы — иконки `materialDesign:PackIcon` `TimerMinusOutline` / `TimerPlusOutline`. В режиме обратного отсчёта дельта инвертируется (кнопка `+1с` = «добавить секунду к оставшемуся времени»). Запись в журнал матча через `AddAction`.

Дополнительные улучшения (вне исходного списка, сделаны попутно):
- ✅ **+5 баллов** для красного и синего на экране управления матчем (`MatchControlView2.xaml`). Грид расширен с 4 до 5 колонок, undo сдвинут вправо. Домен (`AddPoints`) уже принимал любой `int` — изменения только в XAML.
- ✅ **Удалены все ToolTip** на экране управления матчем — мешали во время живой схватки.
- ✅ **Город команды над названием** на экране управления матчем. Новые свойства `Wrestler1TeamCity` / `Wrestler2TeamCity` в `ScoreScreenViewModel`, заполняются из `WrestlerInRed.TeamCity` / `WrestlerInBlue.TeamCity`. Отрисовка: один внешний `Viewbox` со `StackPanel` (две строки одинакового размера и цвета — масштабируются вместе с экраном).
- ✅ **Поиск на экране «Заявки на участие»** — substring (`Contains`, case-insensitive) вместо `StartsWith`, ищет по `Wrestler.FullName` / `Team.ShortName` / `Team.FullName` / `Team.City`. Если совпало по полю команды — внутри карточки видны все её спортсмены. Подсказка: «Поиск по ФИО, команде или городу».
- ✅ **Авто-раскрытие панелей** на «Заявках» с порогом: 1 команда → раскрываем; >1 команды и суммарно >5 видимых спортсменов → схлопнуто. Свойство `ShouldAutoExpand` в `ApplicationsViewModel` пересчитывается при изменении `Items` / фильтра / чекбокса «Только недопущенные».
- ✅ **Экран «Жеребьёвка»**: кнопка «Удалить сетку» удалена; «Создать сетку» теперь видна всегда и всегда открывает `AddBracketDialog`. В диалоге, если сетка уже существует, текущий тип предвыбран в `ComboBox` (логика `AddBracketViewModel.InitData` была уже на месте).
- ✅ **Каскад таймингов группы → Pending-матчи** на экране «Информация о Соревнованиях». При редактировании группы (`DetailsViewModel.EditGroup`) после `Sync` новые `MaxRoundSecond` / `MaxTimeoutSecond` / `MaxActionSecond` автоматически копируются во все матчи группы со статусом ≠ `Completed`. Завершённые не трогаются. Это локальный частичный ответ на #1 — для одной группы; массовая правка по возрасту через push-канал остаётся открытой.
- ✅ **Импорт: ID-based identity check** (часть #6). `TournamentImporter.PrepareAsync` теперь сравнивает турниры по `Tournament.ID` (стабильный GUID) с фолбэком на старую эвристику Name+Date+Groups для очень старых файлов без ID. Переименование турнира на одном пире больше не ломает импорт у соседей.
- ✅ **Импорт: per-candidate diagnostic logging** (часть #6). Все попытки импорта пишут классифицированную причину провала в `data_log_<date>.txt` через `FileLogger` (`Import.http`/`Import.parse`/`Import.match`/`Import.skip`). Следующий турнир даст прямой ответ на «почему такой-то ноут не импортирует».
- ✅ **Импорт: дедупликация ImportSources по физическому хосту** (часть #6). Новая утилита `PeerSourceMatcher` парсит хост из HTTP/UNC/packed-источника. `AddDiscoveredPeer` теперь заменяет старую запись для того же хоста новым packed-источником вместо добавления второй. Решает кейс из реальных файлов турнира: один пир оказывается в подписках в двух разных форматах (HTTP-only + packed) после смены `SelfUncPath` мид-турнира.
- ✅ **#9 Аудит круговой сетки.** `RoundRobinGroupBracketProcessor.CalculateResults` форсировал head-to-head при любой ничьей по числу побед, перебивая UWW-приоритет CP/тех.превосходства. В реальной группе 2012-2013 55кг (`SubGroupsIntoOlympic`, подгруппа B) после распределения 1-го места оставшаяся пара Сурхаев (5 квалбаллов) vs Горячев (3 квалбалла) форсилась через очную встречу — Горячев выходил в полуфинал вместо Сурхаева. Чинено: head-to-head вызывается только при равенстве всех измеримых критериев; для 3+ полностью равных — ранжирование по числу очных побед среди группы. Регрессионный тест дублирует реальный сценарий.
- ✅ **Импорт: ручной выбор сетевого интерфейса для HTTP-анонса** (#15). Новое поле `GlobalSettings.AnnounceIpOverride` (round-trip через адаптер, backward-compat — старые `.wrt` грузятся с пустой строкой = «авто»). `LocalIpAddressProbe.PickAnnounceAddress(override)` валидирует, что указанный IP реально есть на машине; иначе откатывается на старое поведение `PickDefault`. В `SettingsView` появился ComboBox со списком IPv4-адресов и пунктом «(Авто)»; смена override триггерит ребут network-сервисов. Stale override (отсоединённый NIC) сохраняется в списке как видимый, но во время анонса откатывается на авто.
- ✅ **#14 Версионная модель импорта вместо source-string стампов** (2026-04-30). `WrestlingMatch.Version` (int, бампится только в `MatchResultsViewModel.ApproveAsync`/`RejectAsync`) полностью заменил `ImportCompletionSource`+`IsCompletionFromImport` (поля удалены из entity, DTO, адаптера и `ImportPlan`). Импорт сравнивает `remote.Version > local.Version` и применяет любое состояние — Pending→Completed (Case 1), Completed→Pending (Case 2), Completed→Completed-edit (Case 3). На равенстве версий — local wins. Топология теперь не имеет значения: revert от автора A через посредника B доходит до C по тому же `>`-условию, без зависимости от того, через кого C импортировал оригинал. Legacy `.wrt` мигрируются адаптером (Pending→V=0, Completed→V=1), Newtonsoft молча дропает старое поле `ImportCompletionSource`. Регрессионный сьют — `tests/Wrestling.UI.Material.Tests/TournamentImporterApplyTests.cs`: 3-peer transit revert, edit-after-approve, both-Pending sync, equal-version local-wins, stale-remote-ignored.
- ✅ **#11 Дубль матча с одинаковым номером** (коммит `2a06550`). `MatMatchNumbersGenerator.Generate` пересобран: в начале прохода все `MatchNumber` ресетятся в 0, и введён `HashSet<WrestlingMatch> assigned` — один и тот же экземпляр матча не может быть пронумерован дважды. Корневая причина бага: один `WrestlingMatch` фигурировал в нескольких раундах (например, финал и `Get3rdPlaceRound`/`GetSemiFinalRound` возвращали ссылки на тот же объект), и каждый Bind*-проход переписывал ему номер. Теперь Bind* возвращает раньше через `assigned.Add(match) == false`, а catch-all в конце ловит любой матч, не покрытый Bind*-методами (защита на случай, если новый тип сетки введёт раунд, который партиции не знают).
- ✅ **Унификация и чистка экранов MatchControl + WwfScoreScreen** (2026-05-01). Большая UI-серия:
  - **Независимое масштабирование города и команды.** В обеих сетках секция «лого + город + команда» теперь имеет одинаковую разметку: 4 колонки `[лого 1*][инфо 1*][инфо 1*][лого 1*]` (для Wwf — `2*:3*:3*:2*`), info-колонка — `Grid` с двумя равными `RowDefinition`, в каждой свой `Viewbox` для города и команды. Длина одного текста больше не влияет на размер другого. ФИО на `MatchControlView2` оставлено в row 3 (со старой `IsAction[1|2]TimerEnabled` логикой переключения с action-таймером).
  - **Сетчатая разлиновка info-секции.** Вертикальные белые границы между лого и info-колонками; горизонтальная белая линия между городом и командой на уровне родительского `Grid` (`Grid.ColumnSpan="2"`, `VerticalAlignment="Center"`) — тянется через обе info-колонки и касается вертикальных границ. На WwfScoreScreen дополнительно раскомментирована горизонтальная линия выше row 2 (отделяет ФИО от секции город/команда).
  - **Удалён старый `ScoreScreenView`** — заменён на `WwfScoreScreenView` как единственный режим табло. Также удалены: `InternationalScoreScreenView` + `.xaml.cs`, переключатель «Турнирное/Упрощённое» в `SettingsView`, `IsTournamentScoreInternational` (`SettingsViewModel` property + `SetupScoreScreen` method, `GlobalSettings` field, `GlobalSettingsInfo` DTO, `EntityToInfoAdapter` обе ветки, инициализация в `HomeViewModel.GetSettingsObject`, тест-сетап в `AdapterRoundTripTests`). Старые `.wrt` грузятся — Newtonsoft молча дропает удалённое поле.
  - **Удалена «Поединок» Card с Home.** `NewQuickMatchCommand` / `NewQuickMatch()` / `_newQuickMatchCommand` field из `HomeViewModel` убраны вместе с usings `Wrestling.UI.Material.Match`. На стартовом экране остались только «Открыть турнир» / «Новый турнир».
  - **Удалён встроенный блок «Ближайшие поединки» из `WwfScoreScreenView`.** Был избыточен — отдельная фича `UpcomingMatchesSlide` в Slider-системе её закрывает. Из `ScoreScreenViewModel` убраны: `IsUpcomingMatchesVisible`, `UpcomingMatches` (с `_upcomingMatches`), `LastMatchMat` (с `_lastMatchMat`), `BackgroundPath`/`BackgroundOpacity` (использовались только в этом блоке), а также соответствующие инициализации в `InitData` и ветвление в `OnWinnerShowCompleted` (теперь после показа победителя сразу `IsMainScreenVisible = true`). Убран неиспользуемый namespace `materialDesign` и `using System.Collections.ObjectModel`.
  - **Удалён старый `MatchControlView` (v1)** — `MainWindow.xaml` рендерит `MatchControlViewModel` через `MatchControlView2`, v1 был мёртвым кодом.
  - **Удалены все `ToolTip=...` из `WwfScoreScreenView`** — на табло во время живой схватки они мешают.
- ✅ **#4 Bulk-добавление слайдов «Сетки ковра»** (коммит `b958ad9`). Новый макро-тип `MatBracketsSlide` (`Wrestling.UI.Material/Slider/Slides/MatBracketsSlide/`): в `AddSlideDialog` пользователь выбирает ковёр, после подтверждения `SliderControlViewModel.AddSlide` детектит макро-тип и разворачивает его в по одному `GroupBracketSlide` на каждую группу выбранного ковра. Сам макро-слайд в канал не сохраняется — при попытке рендера `CreateViewControl` бросает `NotSupportedException` (fail loudly).
- ✅ **#13 (часть 2) Массовый PDF протоколов взвешивания.** Quick-button «Скачать протоколы взвешивания PDF» (PackIcon `Scale`) рядом с «Скачать сетки PDF» на Dashboard. Переиспользует `BulkBracketPdfExporter` через generic `BulkPdfExportJob.ViewFactory` без правок самого экспортёра. Новые методы `ExportAllApplicationsPdfsAsync` / `BuildApplicationsExportJobs` в `DashboardViewModel` — клон bracket-флоу с фильтром `Wrestlers.Count > 0` (сетка не требуется) и префиксом `Взвешивание_<GroupName>.pdf` (нет коллизии при выгрузке обоих видов в одну папку). `PrintApplicationsView.xaml` подтянут к каноничному виду print-views: заголовок «Протокол Взвешивания», `CompactColumnHeader` style (`Padding="2,0,2,0"`, `FontSize="11"`) на колонках, ширины колонок укладываются в портретный A4, виртуализация ListView отключена (`VirtualizingPanel.IsVirtualizing="False"`), подстановка печати через `Tournament.Settings.SignatureFooterImagePath` + `OptionalImagePathConverter`, имена через `FullNameToShortConverter`. Тот же `CompactColumnHeader` style применён и в `PrintBracketView` для единообразия.
- ✅ **Bulk-PDF: трейлинг-blank-страница не создаётся.** В `BulkBracketPdfExporter.RenderViewToPdf` три слоя защиты от пустой второй страницы (особенно остро в landscape A4, где imageable height ~697 DIPs и StackPanel-layout slack стабильно перетекает за page boundary): (1) `FindLastContentRow` отрезает трейлинг-вайтспейс bitmap'а до нарезки; (2) clamp totalHeightPx до pageHeightPx если overflow ≤25% (slack, не контент); (3) `IsSliceMostlyBlank` row-based: слайс пустой если <3 строк × <10 dark pixels — режет тонкие 1-px разделители, которые проходят первые два фильтра.

---

## Список проблем

### ✅ #1 — Массовая смена длительности раунда для возрастной группы (DONE)
**Симптом:** изменили `RoundDuration` в Положении турнира — на уже созданные `WrestlingMatch` это не распространяется.

**Решение:** Закрыто двумя слоями, оба уже есть:
- **Локально:** `DetailsViewModel.EditGroup` после `Sync` каскадит `MaxRoundSecond/MaxTimeoutSecond/MaxActionSecond` во все Pending-матчи группы и бампит `FieldsVersion`.
- **На пирах:** `TournamentImporter.ApplyGroupFieldChanges` (`TournamentImporter.cs:443`) копирует поля группы и делает тот же каскад в локальные Pending-матчи при `remote.FieldsVersion > local.FieldsVersion`. Завершённые матчи не трогаются.

«Bulk-операция по возрасту» снимается N последовательных EditGroup'ов — отдельный UI не нужен.

---

### ✅ #2 — Корректировка таймера матча на ±N секунд (DONE)
**Симптом:** забыли остановить таймер, на табло «лишние» секунды; нет способа подкрутить.

**Решение:** Кнопки `−1с` / `+1с` рядом с таймером на экране управления матчем. Локальная правка времени, без сетевой синхронизации (таймер всё равно живёт на ковре).

**Где:** `MatchControlViewModel` / `MatchControlView` на ковре, который ведёт матч. Информационные экраны подхватят следующим тиком (если они тянут таймер из импорта — иначе они и так показывают своё локальное время).

**Сложность:** S. Это самый простой пункт, можно начать с него для разогрева.

---

### ✅ #3 — Принудительная раскатка изменений сетки на все ковры (DONE)
**Симптом:** правки в сетке (перепосев, корректировка участников) приходилось разносить флешкой.

**Решение:** Закрыто через `TournamentImporter.ReplaceGroupBracket` (`TournamentImporter.cs:500`). При бампе `BracketVersion` (триггерится регенерацией сетки у секретаря):
- Берётся snapshot локальных матчей по `BracketFullNumber` до замены.
- `local.Bracket = remote.Bracket`, wrestler-refs резолвятся против `target.Wrestlers`.
- Локально-новые завершения переносятся обратно (`localOld.Version > match.Version` и пара `WrestlerInRed/WrestlerInBlue` совпадает по `SameWrestlerPair` — иначе результат бы кредитнулся не той паре после перепосева).

UI-удобство «опубликовать сейчас» (форс-flush вместо ожидания import-тика) — отдельная маленькая задача, если понадобится.

---

### ✅ #4 — Bulk-добавление слайдов в Слайдер (DONE)
**Симптом:** в Слайдере добавляешь по одной весовой категории. На реальном турнире (10+ категорий на ковёр) это долго.

**Решение:** Новый макро-тип `MatBracketsSlide` в `AddSlideDialog`. Пользователь выбирает ковёр, `SliderControlViewModel.AddSlide` детектит макро-тип и разворачивает в по одному `GroupBracketSlide` на каждую группу ковра. См. блок Done выше.

---

### ✅ #5 — Перенос спортсмена между категориями (DONE)
**Симптом:** спортсмен ошибочно зарегистрирован в категории A, нужно перевести в B.

**Решение:** Закрыто стандартной регенерацией сеток + импортом:
- Секретарь убирает спортсмена из A, добавляет в B, ре-генерирует обе сетки → бампятся `BracketVersion` обеих групп.
- Импорт: `SyncWrestlers` (`TournamentImporter.cs:363`) добавляет нового, если он не был известен пиру; `ReplaceGroupBracket` подтягивает обе новые сетки с сохранением локально-новых завершений по `Version` + `SameWrestlerPair`.
- Сценарии 1/2/3 из исходного описания различаются только тем, что секретарь видит у себя на экране при ре-генерации — для пиров это всё одна операция.

Админ-операция «переназначить спортсмена» как single-click — UX-улучшение поверх рабочего механизма, не блокирует.

---

### #6 — Импорт через HTTP ломается, если параллельно настроен и share-путь
**Симптом:** при включённой совместной конфигурации HTTP+UNC формируется путь, по которому HTTP импорт не отрабатывает. Похоже на баг парсинга.

**Анализ 2026-04-28** на трёх .wrt с разных ноутов того же турнира (`D:\20260426*.wrt`). Сам split-flow `PrepareAsync` корректен (тесты `Compound_source_falls_back_to_second_candidate_when_first_is_unreachable` это доказывают). Найдены три смежных недочёта, которые могут давать пользовательский симптом «не работает HTTP импорт» — все три исправлены:

1. ✅ Identity-чек по `Name+Date+Groups` ломался на любом легитимном изменении (переименование турнира, добавление группы, сдвиг даты). Заменён на ID-чек.
2. ✅ Не было per-candidate диагностики — оператор видел только агрегатное «Файл недоступен». Теперь подробный лог в `data_log_<date>.txt`.
3. ✅ После смены формата анонса (HTTP-only → packed) у пира операторы получали по две строки `ImportSources` на одну машину. Дедуп по хосту через `PeerSourceMatcher`.

**Что осталось открытым:** для воспроизведения исходного отчёта нужна сетевая диагностика на КОВЕР С (теперь будет покрыта через `data_log` на следующем турнире). Также см. #15 ниже.

**Сложность:** S-M ✅ done (3 коммита).

---

### ✅ #7 — Нереалистичная оценка оставшегося времени; нет учёта пауз (DONE)
**Симптом:** ETA турнира неточный; нет способа учесть запланированную паузу (открытие, обед).

**Решение:** Экран «Расписание» — `Wrestling.UI.Material/Tournament/Progress/Schedule/` (`ScheduleViewModel` + `ScheduleView.xaml`). Прогноз окончания по каждому ковру и по каждой категории, статистика прошедших/оставшихся матчей. Печатная версия — `PrintSchedule` (`Tournament/Print/PrintSchedule/`). Доступ из шелла (Phase 5/Ковер wrapper) и через `MatsViewModel`.

---

### ✅ #8 — Импорт жёстко завязан на номер схватки (отклонено, не нужно)
**Решение:** Закрыто иначе и без отдельной задачи. На текущий момент в `TournamentImporter.Apply`:
- Группы — по `group.ID` (стабильный GUID), `TournamentImporter.cs:245`.
- Матчи внутри группы — по `BracketFullNumber` (стабильная пара `RoundNumber.BracketNumber`), `TournamentImporter.cs:295`. Комментарий в коде явно объясняет почему не `MatchNumber`: «MatchNumber per-mat и renumber'ится при перемещении группы — using it as the merge key would silently match wrong matches».
- При перепосеве `ReplaceGroupBracket` делает full bracket swap по `BracketVersion`, плюс `SameWrestlerPair`-проверка предохраняет от криво-перенесённых результатов.

Переход на match-GUID не даёт дополнительной пользы — `BracketFullNumber` и так стабилен внутри группы, а смена структуры покрыта `BracketVersion`-replace'ом.

---

### ✅ #9 — Аудит расчёта лидеров в круговой сетке (DONE)
**Симптом:** в категории 2012-2013 55кг (тип сетки `SubGroupsIntoOlympic`) подгруппа B = {Тагиров, Сурхаев, Горячев}. По 1 победе у каждого. Сурхаев имел 5 квалбаллов (победа по преимуществу + поражение по баллам), Горячев — 3 (победа по баллам + поражение по туше). По правилам УВВ Сурхаев должен был выйти вторым, но вышел Горячев.

**Корневая причина:** `RoundRobinGroupBracketProcessor.CalculateResults` проверял очную встречу при ЛЮБОЙ ничьей по числу побед — даже когда классификационные баллы и другие критерии уже различали борцов. В 3-стороннем тае после распределения 1-го места оставшаяся пара (Сурхаев vs Горячев) форсировалась через head-to-head: Горячев когда-то выиграл у Сурхаева по баллам → Горячев получил 2-е, Сурхаев — 3-е. UWW же ставит head-to-head ПОСЛЕ всех остальных тай-брейкеров (CP, Тушé, тех. превосходство, набранные/пропущенные баллы).

**Решение:** головной OrderBy сохранён; head-to-head вызывается только когда все измеримые критерии (Wins, OverallTournamentClassificationPoints, WinsByTushe, WinsByDomination[WithPoints], AllGainedPoints, AllLostPoints) равны. При 3+ полностью равных борцах — ранжирование по числу очных побед среди группы, далее по SeedNumber.

**Тесты:** `Three_way_tie_on_wins_breaks_by_classification_points_not_pair_result` (регрессия, дублирует реальный сценарий 55кг 2012-2013) + `Two_way_tie_on_everything_breaks_by_head_to_head` для ветки настоящей очной встречи.

**Где:** `Wrestling.Entities/Bracket/RoundRobinGroupBracketProcessor.cs`. Также неявно чинит `SubGroupsToOlympicBracketProcessor`, который через `_groupBProcessor.GetResults()[1]` достаёт серебро подгруппы для полуфинала — тот же баг там и проявился.

**Сложность:** M ✅ done.

---

### ✅ #10 — Перенос категории на другой ковёр (DONE)
**Симптом:** ковёр B освободился — хочется передать ему категорию с ковра A.

**Решение:** Закрыто через `ApplyGroupFieldChanges` (`TournamentImporter.cs:443`). Секретарь меняет `MatID` группы у себя → бампится `FieldsVersion`. На пирах:
- `local.MatID = remote.MatID` (line 454).
- `oldMat.Groups.Remove(local)` + `newMat.Groups.Add(local)` (lines 472-487) переподключают группу к нужному ковру в живом графе.
- Структурная правка триггерит `_matchNumbersGenerator.Generate` (line 347), и MatchNumber'а на новом ковре собирается детерминированно — пиры конвергируют без копирования номеров через сеть.

---

### #13 — Массовая печать протоколов
**Симптом:** на экране «Жеребьёвка» каждый протокол группы печатается отдельной кнопкой (иконка «принтер» в строке группы → `PrintProtocolCommand` → preview одной группы → печать → закрыть → следующая группа). На реальном турнире 30+ групп — это 30+ ручных кликов и подтверждений диалога печати.

**Решение (DONE для сеток):** Quick-button «Скачать сетки PDF» (PackIcon FilePdfBox) на Dashboard рядом с «Сохранить турнир» → выбор папки → один PDF на каждую группу со сгенерированной сеткой и результатами (через PdfSharp 6.1, без диалога печати). Реализовано в `Wrestling.UI.Material/Tournament/Print/BulkBracketPdfExporter.cs` — переиспользует `PrintBracketView` через off-tree рендер в RenderTargetBitmap (300 DPI), нарезка по A4 **landscape** (842×595pt), сборка PDF страница за страницей. Имена файлов санитайзятся под Windows. Виртуализация ListView с местами участников отключена (`VirtualizingPanel.IsVirtualizing="False"`) — иначе off-tree рендер показывал только первого финалиста.

**Решение (DONE для протоколов взвешивания):** Quick-button «Скачать протоколы взвешивания PDF» рядом с «Скачать сетки PDF» на Dashboard. Переиспользует `BulkBracketPdfExporter` через generic `BulkPdfExportJob.ViewFactory` (без правок самого экспортёра). Фильтр групп — `Wrestlers.Count > 0` (сетка не требуется). Файлы — `Взвешивание_<GroupName>.pdf` (префикс предотвращает коллизию с bracket-экспортом в одну папку). В `PrintApplicationsView.xaml` отключена виртуализация ListView (`VirtualizingPanel.IsVirtualizing="False"` + `ScrollViewer.CanContentScroll="False"`) — обязательный инвариант для off-tree рендера.

**Что осталось (если понадобится):** выбор подмножества групп через чекбоксы; добавление печати/подписи в шаблон сетки (отдельная задача, обсуждаем после).

**Где живёт сейчас:**
- `Wrestling.UI.Material/Tournament/Standing/Draw/DrawViewModel.cs:225` — `PrintProtocol(group)` ставит `DataContext.Group = group` и зовёт `ShowPrintPreview(new PrintApplicationsViewModel(...))`.
- Шаблон печати — `Wrestling.UI.Material/Tournament/Print/PrintApplications/...` (XAML рендерится через `VisualPrinter`).

**Сложность:** S-M. Зависит от того, как `PrintApplicationsViewModel` сейчас знает про текущую группу через `DataContext.Group`. Возможные варианты:
- Перебрать группы → собрать список FlowDocument'ов → склеить в одно превью.
- Расширить `PrintApplicationsViewModel` второй вью-моделью «всё-в-одном» с переменной `Groups` вместо `Group`.

Опции:
- Печать всех групп подряд.
- Печать только групп с готовой сеткой (`IsBracketGenerated`).
- Чекбоксы по группам с массовым «выбрать все / только с сеткой».

---

### ✅ #15 — Выбор сетевого интерфейса для HTTP-анонса при нескольких NIC (DONE)
**Симптом:** На ноутах с двумя сетевыми интерфейсами (например, у Admin: `192.168.1.60` и `192.168.88.x`) `LocalIpAddressProbe.PickDefault` берёт первую попавшуюся 192.168/16 — порядок зависит от энумерации NIC. Может оказаться неверной подсетью, и HTTP-анонс уйдёт с адресом, до которого пиры физически не доходят.

**Решение:** Добавлено `GlobalSettings.AnnounceIpOverride` + `LocalIpAddressProbe.PickAnnounceAddress(override)` (валидирует наличие IP на машине, иначе fallback на `PickDefault`). В `SettingsView` — ComboBox с пунктом «(Авто)» по умолчанию и текущими IPv4. См. блок Done выше.

**Что осталось открытым:** auto-режим всё ещё «первая 192.168/16» — без подсетевого матчинга к получаемым UDP-анонсам. Если 95% пользы снимает ручной override — авто-матчинг вынесем в отдельную задачу при необходимости.

---

### ✅ #12 — Обоюдная дисквалификация спортсменов (DONE 2026-05-08)
**Уточнение по правилам УВВ (после проработки):** при одиночной DSQ предыдущие результаты борца **не аннулируются** — он переносится на последнее место с отметкой `DSQ`, прошлые победы соперников остаются. Аннулирование «всё назад» — это WADA-кейс (постфактум), не операторская функция. Текущая single-DSQ логика (каскад через `CompleteFullLooserMatches`) этому соответствует и оставлена без изменений.

Реальная задача — поддержка **обоюдной дисквалификации** за грубость в одном матче (M1/M4 авто, M2/M3 алерт + ручная правка):

**Реализовано:**
- Новый `MatchWinTypeEnum.MutualDisqualify` (значение 11, в конце enum — старые `.wrt` грузятся, Newtonsoft дропает неизвестные).
- Сигнатура `IGroupBracketProcessor.CompleteMatch` ослаблена до `bool? isRedWon` — для mutual передаётся `null`, контракт: `Status=Completed && WinType=MutualDisqualify ⇒ IsRedWon=null`.
- Новый флаг `Wrestler.IsDisqualified` (поле + DTO + adapter обе ветки + Sync/Clone). `FinalPlace` остаётся `null` для DSQ-борцов → командные очки 0 через существующий `GetPlacePoints(null)=0`.
- В `GroupBracketProcessorBase.CompleteMatch` для mutual: оба борца получают `IsDisqualified=true`, дважды вызывается `CompleteFullLooserMatches` (для red и blue) с типом `DisqualifyWin` → каскад в round-robin (M4) на все pending-матчи каждого с другими борцами.
- В `ProceedToNextMatch` двунаправленный auto-FreeWin (M1): новая утилита `FindSiblingInBracket`. Если sibling уже завершён обычной победой — текущий mutual триггерит auto-FreeWin в next-match для победителя sibling'а. Если sibling завершается ПОСЛЕ mutual — то его пропагация ловит mutual-сосед и тоже триггерит auto-FreeWin. Каскадирование через все раунды бесплатно за счёт рекурсии существующего `CompleteMatch`.
- В `RevertMatch` при mutual очищаем `IsDisqualified` обоим. Каскадные DisqualifyWin-матчи остаются (наследованное поведение single-DSQ revert) — `CanMatchBeReverted` блокирует revert mutual в M1, пока next-match auto-FreeWin'нутый не сброшен.
- Алерт SF/F: `MatchResultsViewModel.IsMutualDsqRequiringManualRebuild` детектит элиминационную сетку (не RoundRobin) + матч в `GetSemiFinalRound`/`GetFinalRound` → `ShowSnackMessage` «Обоюдная DSQ в полуфинале/финале — требуется ручная перестройка сетки (правила УВВ)». M2/M3 авто-логика **не** реализована (правило: «бронзовые медалисты борются за 1-2», «проигравшие в QF проводят SF» — слишком инвазивно для частоты ~1 на 10000 матчей).
- Audit `IsRedWon.Value` сайтов в 4 концретных процессорах: добавлены `HasValue`-guard'ы; mutual-DSQ матчи пропускаются в `CalculateResults` (FinalPlace остаётся null), `ProceedToAdditionalBracket` рано выходит для mutual SF, auto-FreeWin третьего места подавлен при mutual SF.
- `TournamentImporter`: 4 строки `processor.CompleteMatch(..., IsRedWon.Value, ...)` → `IsRedWon` (nullable), без `.Value`. Без этого фикса — NRE при попытке импорта mutual-DSQ матча.
- UI: `WinTypeToStringConverter` лейбл «Обоюдная дисквалификация (DSQ × DSQ)»; `WrestlingMatch.IsMutualDisqualify` computed property с notify; `CompleteMatchCommand.canExecute` пропускает требование Winner для mutual; в `CompletedMatchesView`, `BracketsView`, `PrintBracketView`, `GroupBracketView` (slider) — иконка `CloseOctagon` (Red) на ячейке матча; в `PersonalResultsView` колонка «Место» показывает «DSQ» вместо номера для дисквалифицированных.
- Sync через `WrestlingMatch.Version` работает естественно — mutual-DSQ это просто очередное состояние матча; пиры применяют его так же, как любой Pending→Completed/Completed→Pending переход. `ApplyResultFields` копирует `IsRedWon` (nullable) корректно.

**Тесты (28 новых, всего 298 проходят):**
- `tests/Wrestling.Entities.Tests/MutualDisqualifyTests.cs` (23 теста): M1 Olympic 8 wrestlers (mutual + sibling completed before/after, FreeWin propagation cascade), M2 Olympic 4 (alert path: SF mutual не пропагирует в final/3rd-place, не auto-FreeWin'ит при completed sibling), M3 Olympic 4 (final mutual: оба DSQ, FinalPlace=null), M4 RoundRobin 2/3/4 wrestlers (cascade DisqualifyWin для соперников, C/D unaffected, FinalPlace=null для DSQ), TournamentResult.Wins не считает mutual, revert clears IsDisqualified, CanMatchBeReverted блокируется при auto-FreeWin'нутом next-match, IsDisqualified default+Sync+Clone, IsMutualDisqualify property, OlympicWithConsolation + SubGroupsToOlympic smoke включая SF mutual.
- `tests/Wrestling.Providers.Tests/AdapterRoundTripTests.cs` (+2): IsDisqualified roundtrip, legacy DTO без поля → false.
- `tests/Wrestling.UI.Material.Tests/TournamentImporterApplyTests.cs` (+3): mutual DSQ import без NRE, revert через Version, edit-after-mutual (Case 3) на нормальную победу очищает IsDisqualified.

**Где живёт код:**
- `Wrestling.Entities/MatchWinTypeEnum.cs`, `Wrestler.cs`, `WrestlingMatch.cs` (IsMutualDisqualify property), `Bracket/GroupBracketProcessorBase.cs` (CompleteMatch + ProceedToNextMatch + FindSiblingInBracket + RevertMatch), `Bracket/OlympicGroupBracketProcessor.cs`, `Bracket/OlympicWithConsolationFromFinalistsGroupBracketProcessor.cs`, `Bracket/RoundRobinGroupBracketProcessor.cs`, `Bracket/SubGroupsToOlympicBracketProcessor.cs`, `Results/TournamentResults.cs` (HasValue guards в Wins/Loses queries).
- `Wrestling.Data/WrestlerInfo.cs`, `Wrestling.Providers/EntityToInfoAdapter.cs`.
- `Wrestling.UI.Material/Match/MatchResultsViewModel.cs`, `Utils/Converters/WinTypeToStringConverter.cs`, `Model/TournamentImporter.cs` + 4 XAML view'а.

---

## Что осталось активным

Активных пунктов нет — весь список закрыт. «Сквозная архитектурная тема» (админ-панель + push-канал) снята: версионная модель + расширенный `Apply` уже покрывают раскатку структурных правок через обычный pull-импорт.

---

## Done — Convergence-driven sync (2026-05-03)

После проектирования push-канала пришли к выводу, что та же UX даётся pull-only архитектурой за счёт UDP-гигрегации `stateHash` + event-driven pull. Реализовано:

- **`PeerStateHasher`** (`Wrestling.Providers/Network/PeerStateHasher.cs`) — SHA256-prefix (16 hex) от канонической `(GroupID, FieldsVersion, BracketVersion) + (BracketFullNumber, MatchVersion)`. Идентичные состояния → идентичный хеш.
- **`PeerAdvertisement.StateHash`** — поле в UDP-анонсе. Каждый пир вычисляет свой хеш перед отправкой через `Func<string> stateHashProvider` callback на `PeerDiscoveryService.StartForTournament`.
- **`PeerSyncService`** (`Wrestling.UI.Material/Model/PeerSyncService.cs`) — слушает `IPeerDiscoveryService.PeerUpserted`. При `peer.StateHash != local`: `PrepareAsync` (threadpool) → `Apply` (UI). Per-peer дедуп по `(InstanceId, lastPulledHash)`. Autosave срабатывает только на `Outcome=Imported`.
- **`PeerSyncStatusTracker`** (`Wrestling.UI.Material/Model/PeerSyncStatusTracker.cs`) — read-model для UI. ObservableCollection<PeerStatusViewModel> с тремя статусами (✅ синхронизирован / ⏳ догоняет / ⚠ не в сети). 5-минутный session-cache: пир, выпавший из `PeerRegistry`, остаётся в карточке как «не в сети» ещё 5 минут — оператор успевает заметить.
- **Card «Синхронизация»** — добавлена в `DashboardView.xaml` после «Слайдер». Имена пиров + статус-иконки.
- **UDP timing tuned** — `PeerDiscoveryService.AnnounceInterval` 2с → 5с, `PeerRegistry` expire 6с → 15с. На LAN в реалистичной нагрузке UDP-болтовня ~120 Б/с aggregate в steady-state, HTTP-pull срабатывает только при реальной дивергенции.

**Удалено:**
- `Tournament.ImportSources` (entity, DTO, adapter обе ветки) — discovery становится единственным механизмом sync.
- `GlobalSettings.IsDiscoveryEnabled` — discovery всегда включён.
- `GlobalSettings.ImportSeconds` (через `ImportViewModel` deprecation) — таймер ушёл.
- `ImportViewModel` целиком + `ImportView.xaml` + DataTemplate в `MainWindow.xaml` + DI-регистрация в `App.xaml.cs` + запись в `NavigationService`.
- Drawer-пункт «Импорт» в `DashboardViewModel`.
- Тесты `ImportAutosaveTests.cs` (бил тестировал ImportViewModel.ImportDataAsync — путь упразднён).

**Migration / backward-compat:**
- Старые `.wrt` грузятся: Newtonsoft молча дропает удалённые поля (`ImportSources`, `IsDiscoveryEnabled`, `AutosaveMaxSecond`).
- `NodeName` пустой → автоматически подставляется `Environment.MachineName` в `EntityToInfoAdapter.GetEntityFromInfo` и в `GlobalSettings` ctor. Старые ноуты после обновления сразу discoverable без явной настройки.

**Live-match-during-pull edge case:** оставлен на ответственность оператора ковра. Карпет, у которого идёт live-матч в группе, у которой `BracketVersion` бампится, потеряет live-state объекта `WrestlingMatch` (как и при текущем pull). Это операторский вопрос, не протокольный — оператор видит в карточке, что он отстал, и физически решает.

**Результаты тестов** на 2026-05-03: 219 пройдено / 0 упало (Wrestling.Entities.Tests 56, Wrestling.Providers.Tests 51, Wrestling.DataAccess.Tests 23, Wrestling.UI.Material.Tests 89).

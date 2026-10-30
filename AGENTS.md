# AGENTS.md

This file provides guidance to coding agents working with code in this repository. Cross-project working principles live in `../CLAUDE.md`.

## Active punch-list

Operational issues raised after real tournaments are tracked in **`docs/TodoList.md`** — load this file at the start of every session and treat it as the source of truth for outstanding work and what has shipped. When issues are resolved or new ones surface, update that file (move entries between the `Done` and `Список проблем` sections, mark headings with ✅ as appropriate). When the user references "пункт N" / "проблема N" / "issue N", look it up there.

## What this is

WPF desktop application for administrating and running freestyle wrestling tournaments: team/wrestler registration, automatic bracket generation, match control with live scoring and timers, multi-mat scheduling, result calculation, broadcast/projection slides, and printed reports. Primary UI language is Russian; English is available via the localization layer (see the Localization subsection under Architecture).

## Build and run

Solution file: `Wrestling.sln`. All projects target `net10.0` (UI/Utils: `net10.0-windows`). Release builds produce x64 binaries via `PlatformTarget` set in the csproj — the `.sln` platform mapping is intentionally kept at `AnyCPU` so a single `dotnet build -c Release` works without `-p:Platform=x64` tweaks. Windows-only (WPF).

```bash
dotnet restore Wrestling.sln
dotnet build   Wrestling.sln                    # Debug build
dotnet build   Wrestling.sln -c Release         # Release build (x64 via PlatformTarget)
dotnet test    Wrestling.sln                    # run all xUnit tests
dotnet run     --project Wrestling.UI.Material  # launch the app
```

## Testing

xUnit test projects under `tests/` — one per library under test:
- `tests/Wrestling.Entities.Tests` — bracket processors, result calculators, entity validation
- `tests/Wrestling.Providers.Tests` — adapter round-trip, cache manager
- `tests/Wrestling.DataAccess.Tests` — storage layer (JSON), atomic write, error paths
- `tests/Wrestling.UI.Material.Tests` — importer, peer sync, navigation, scheduling/parts and other UI logic

Run: `dotnet test Wrestling.sln`. Test projects target `net10.0` (`Wrestling.UI.Material.Tests` is `net10.0-windows`) and use xUnit + FluentAssertions.

## Project layout and dependency direction

Responsibilities and direct project references (see the `.csproj` files):

```
Wrestling.Entities     (net10.0)         — domain model, brackets, seeding, results; no project references
Wrestling.Data         (net10.0)         — serializable DTOs; no project references
Wrestling.DataAccess   (net10.0)         — file repositories; references Data
Wrestling.Providers    (net10.0)         — services, mapping, network; references Entities, Data, DataAccess
Wrestling.UI.Utils     (net10.0-windows) — MVVM, converters, DI, localization; no project references
Wrestling.UI.Material  (net10.0-windows) — WPF app; references Entities, DataAccess, Providers, UI.Utils
```

Domain logic lives in `Wrestling.Entities` (computed properties like `IsApplicationValid`, scoring, validation). Keep it out of view models. DTOs and domain entities are independent; `EntityToInfoAdapter` in Providers connects them. Do not introduce dependencies from these libraries to the UI.

## Architecture — things you need to know before editing

### MVVM is hand-rolled, not CommunityToolkit/Prism

- Base class: `Wrestling.UI.Material/Model/ViewModelBase.cs`. Inherits `ObservableObject` from `Wrestling.Entities` (entities also raise `INotifyPropertyChanged` directly). It receives `IDiContainer` in its constructor.
- Commands: `RelayCommand` and `AsyncRelayCommand` in `Wrestling.UI.Utils`. No `[RelayCommand]` attributes; declare commands as lazy-initialized properties.
- Don't introduce CommunityToolkit.Mvvm / Prism without an explicit ask — the pattern is consistent across the codebase.

### DI is a custom service locator, wired manually in App.xaml.cs

- Container: `DiContainer.Instance` (singleton), interface `IDiContainer` in `Wrestling.UI.Utils`. API: `Add<T>(instance)`, `Resolve<T>()`, plus keyed overloads (string-named registrations).
- Infrastructure registrations live in `Wrestling.UI.Material/App.xaml.cs` `GetContainer()`, called by `OnStartup`; navigation and special view models are initialized by `LoadNavigation()` and `LoadSpecialViewModels()`. Add services in dependency order using explicit `di.Add<I>(new Impl(...))` wiring.
- `ViewModelBase.Resolve<T>()` delegates to the injected `IDiContainer`. View models resolve services in `InitData()`; follow this pattern. Infrastructure services can receive explicit constructor dependencies; there is no automatic constructor resolution.

### Navigation: singleton view models + XAML DataTemplate dispatch

- `NavigationService` (`Wrestling.UI.Material/Model/NavigationService.cs`) holds a **list of pre-instantiated view model singletons** — one per page. Navigation swaps `MainWindowViewModel.CurrentViewModel` to the target instance; XAML `DataTemplate` entries in `MainWindow.xaml` pick the matching view.
- Register a new page by adding its instance to `NavigationService.LoadNavigation()` and its `<DataTemplate DataType="{x:Type …VM}">` to `MainWindow.xaml`. Rail destinations are declared in `App.xaml.cs` `BuildNavigationItems()` / `BuildFooterNavigationItems()`; register overlay parent/visibility behavior in `OnStartup` when needed. Ordinary page VMs are created by NavigationService, not registered individually in DI.
- Because VMs are singletons that live for the whole app session, **do not hold per-navigation state in fields without resetting in `InitData()`**. `InitData()` is the "entering the page" hook; `OnNavigationCompleted()` runs after the view is bound.
- The persistent rail exposes Details → Applications → Draw → Mats → Conducting → Results, with Settings/Close in its footer. `MainWindowViewModel` owns the shared save action and overlay visibility. Conducting opens match control/results, schedule, brackets, mat board and slider controls as overlays; Results hosts personal/team/achievement sub-pages. There is no Dashboard or Import page. Home selects the initial phase from tournament state.

### Persistence is file-based, no database

- Tournaments serialize to `.wrt` files (plain JSON via Newtonsoft.Json 13.x) via `ITournamentsManager` / `TournamentsManager` in `Wrestling.Providers`.
- Entity ↔ DTO mapping lives in `EntityToInfoAdapter` (`Wrestling.Providers`). When you add a new entity field, update **both** directions of the adapter and its `*Info` counterpart in `Wrestling.Data`, otherwise the field won't round-trip through save/load.
- Schema migration is implicit — use constructor defaults on the `*Info` DTO (Newtonsoft calls the parameterless ctor before overlaying JSON, so missing fields in old files stay at the ctor value) **and/or** adapter-level normalization (e.g. `if (entity.MaxBackupCount <= 0) entity.MaxBackupCount = 20`). Old `.wrt` files must keep opening; explicitly persisted valid settings retain their values.
- **Removing a persisted field is safe** — Newtonsoft silently drops unknown JSON properties on load. No migration needed.
- Wrestler / team caches: `Cache_Wrestlers.json`, `Cache_Teams.json` under `%LocalAppData%/WrestlingAdmin/`, managed by `CacheManager`. Crash backups: `%LocalAppData%/WrestlingAdmin/Backups/*.wrt`. Crash log: `%LocalAppData%/WrestlingAdmin/Logs/error_log_<yyyyMMdd>.txt`. Data-access log (load/backup failures, classified): `%LocalAppData%/WrestlingAdmin/Logs/data_log_<yyyyMMdd>.txt` via `FileLogger` in `Wrestling.DataAccess`.
- Image, video and sound paths reference external files; `.wrt` is not an asset bundle. Team emblem filenames and optional stamp/signature images can resolve through the application's `Images` folder (`HomeViewModel.VerifyTeamEmblems`, `OptionalImagePathConverter`). Global and per-slide resource paths can differ, so validate both when preparing a portable tournament setup.

### Tournament save pipeline — atomic write + backup + verify

`TournamentDataAccess.SaveToFile{,Async}` wraps `JsonStorageDataAccess` with the following stages, in order:
1. **Pre-save backup** — copy the existing `.wrt` (if any) to `<dir>/Backups/<filename>/yyyyMMdd_HHmmss_fff.wrt` before overwrite. Default root is `<tournament-dir>/Backups/`; override via `GlobalSettings.BackupFolderPath` (absolute path wins; relative resolves against the tournament's directory). Per-tournament subfolder keeps multiple `.wrt`s sharing a working directory from mixing backups.
2. **Atomic write** — `JsonStorageDataAccess` serializes to `<file>.tmp.{guid}` then swaps it into place (replace for an existing destination). This avoids overwriting the destination with a partial serialization.
3. **Post-save verification** — re-read the file, deserialize into `TournamentInfo`. On failure (rare but possible if serialization produces invalid JSON), restore the newest backup and return `false` (the UI surfaces "save failed" via snack bar; no exception propagates).
4. **Rotation** — drop oldest beyond `GlobalSettings.MaxBackupCount` (current default and invalid-value fallback: 20). The backup subfolder uses the tournament filename without its extension.

Policy lives on the tournament being saved (`info.Settings.IsBackupEnabled` / `MaxBackupCount` / `BackupFolderPath`). When `IsBackupEnabled=false`, steps 1 and 4 are skipped (verification still runs).

**Backup operations are always best-effort.** Any `IOException` / `UnauthorizedAccessException` / `SecurityException` / `PathTooLongException` / `ArgumentException` / `NotSupportedException` is logged via `FileLogger` and swallowed — a backup failure must never prevent the actual save. Anything outside that set (OOM, AccessViolation, NRE from a bug) propagates so the global crash handler fires.

### Load paths never throw for expected I/O or parse errors

`JsonStorageDataAccess.ReadFromFile{,Async}` deliberately returns `default(T)` for file-not-found, `IOException`, `UnauthorizedAccessException`, `JsonException`, `SocketException`, `TimeoutException`, `ArgumentException`, `SecurityException`, `PathTooLongException`, `NotSupportedException`, `FormatException`. Storage supports local/network paths; a failed read during a live tournament must not crash the app. Each failure logs a classification tag (`Corrupt` / `AccessDenied` / `Transient` / `InvalidPath` / `NotFound` / `IO` / `Other`) to `data_log_<date>.txt` plus retry count. Retry counts on transient errors: async path uses 5 (network) / 3 (local), sync path uses 3 (network) / 1 (local), 200 ms initial delay with exponential backoff. Most call sites are on the async path.

`ITournamentImporter` is split into two phases to keep UI responsive during peer-sync pulls:

- **`PrepareAsync(target, fileName, cancellationToken)`** — loads, deserializes and runs the adapter off the UI thread. Does not mutate the target's `ObservableCollection<T>` graph. Returns an `ImportPlan` that either short-circuits (`ImportPlan.Skip(outcome)`) or carries the loaded remote tournament (`ImportPlan.Proceed(remote)`).
- **`Apply(target, plan)`** — UI-thread only. Reconciles registrations and versioned tournament/mat/group fields, replaces newer brackets, and applies newer match completions, reverts or result edits through bracket processors. Touches `ObservableCollection` and raises `INotifyPropertyChanged`.

`PeerSyncService.PullAndApplyAsync` wraps `PrepareAsync` in `Task.Run`, then calls `Apply` on the UI context. It rejects a prepared result when the target tournament has been closed or replaced. The classified `ImportResult` (`Imported` / `NoNewData` / `FileUnavailable` / `TournamentMismatch` / `Error`) drives logging, retry behavior and the autosave gate. `ImportViewModel` and its timer were removed.

### Autosave is event-driven, not timer-based

There is **no** DispatcherTimer for autosave and **no** user-facing toggle. Automatic saves fire after:

1. **Match approval or revert** — `MatchResultsViewModel.ApproveAsync` / `RejectAsync` call `SaveIfAutosaveEnabledAsync()` after updating the bracket and match version.
2. **Peer-sync merge** — `PeerSyncService.SaveAfterMergeAsync` writes the local `.wrt` only for outcome `Imported`, after recalculating results. `NoNewData` and failed pulls do not save.

The UI hook is `TournamentViewModelBase.SaveIfAutosaveEnabledAsync`. It short-circuits when `Tournament` is null or `FileName` is empty, then awaits `TournamentManager.SaveToFileAsync` and surfaces success/failure via snackbar. The peer-sync path has its own guard, cancellation and logging.

The empty-FileName guard matters: background sync and post-match handlers must never pop a SaveAs dialog. `HomeViewModel.OpenNewTournamentPage` prompts for a path up front. If dismissed, the operator can use `MainWindowViewModel.SaveCommand`, which delegates to the current `TournamentViewModelBase.SaveDataAsync` and can show SaveAs. The shared save action is visible on tournament pages while the navigation rail is visible, and hidden on full-screen overlays. Registration, bracket generation and schedule edits still require manual saving; there is no Dashboard re-prompt. The HTTP server serves the saved file, so saving these edits also matters for publication to peers.

### Bracket generation is a Strategy pattern

- Interface: `IGroupBracketProcessor` in `Wrestling.Entities/Bracket/`. Base: `GroupBracketProcessorBase` (template method — `Generate()` orchestrates `GenerateMainRounds()` + `GenerateAdditionalRounds()`).
- Current implementations (registered as a `List<IGroupBracketProcessor>` in `App.xaml.cs`):
  - `OlympicGroupBracketProcessor` — 8+ participants, single elimination + 3rd place match
  - `OlympicWithConsolationFromFinalistsGroupBracketProcessor` — variant consolation
  - `RoundRobinGroupBracketProcessor` — <6 participants, all-play-all
  - `SubGroupsToOlympicBracketProcessor` — 6–7 participants, round-robin subgroups feeding an Olympic final
- Selection rule (per README): <6 → RoundRobin; 6–7 → SubGroups; 8+ → Olympic with consolation.
- To add a new format: implement `IGroupBracketProcessor`, register the singleton in `App.xaml.cs` list. `CompleteMatch` / `RevertMatch` must keep bracket state consistent — winners propagate via the `NextMatchBracketFullNumber` link on `WrestlingMatch`.

### Team results and achievements are also Strategy-based

- Team ranking: `ITeamResultsCalculator` with orderers `OlympicTeamResultsOrderer`, `MedalsTeamResultsOrderer`, `PointsTeamResultsOrderer` (different scoring systems per tournament format).
- Special awards: `IAchievementCalculator` — `FastestWinAchievementCalculator`, `MostAmplitudeActionsAchievementCalculator`, etc. All registered in `App.xaml.cs`. Add new calculators by registering them in the same list.

### Multi-mat synchronization

Each mat PC keeps a local copy of the same tournament `.wrt`. `NetworkServicesLifecycle` watches `IDataContext.TournamentChanged`, starts/stops UDP discovery and the embedded HTTP server, and restarts them when network settings change. Discovery is always enabled; HTTP serving is controlled by `IsHttpServerEnabled` and requires a saved file. Defaults: discovery UDP `24565`, HTTP `24566`; announcements every 5 seconds, peers expire after 15 seconds. `AnnounceIpOverride` selects the advertised local IPv4 address; an invalid override falls back to automatic selection.

`PeerSyncService` listens for peer advertisements. A differing `StateHash` triggers a pull from the advertised HTTP URL through `ITournamentImporter`; there is no Import page, import-source list or timer polling UNC files. Successful peer/hash pairs are cached; failed pulls can retry, with a cooldown after repeated failures. `PeerSyncStatusTracker` exposes status on Conducting and keeps disconnected peers visible for five minutes.

- Identity is `Tournament.ID`; only legacy missing-ID files use the name/date/group heuristic. Renaming a file or tournament does not create a new identity. Create a fresh tournament for a new event, then distribute copies of that saved file.
- Newer versions win: `WrestlingMatch.Version` handles completions, reverts and result edits; `AgeWeightGroup.FieldsVersion` handles category fields, timing, mat/part assignment; `BracketVersion` replaces a bracket; `Tournament.MetaVersion` covers parts; `Mat.FieldsVersion` covers active-part changes. Equal versions preserve local state; this is not conflict-free concurrent editing.
- `PeerStateHasher` hashes tournament meta version, mat IDs/field versions, group IDs/field and bracket versions, and match identities/versions. Changing synchronized data without the appropriate version bump can leave peers unaware of the change.
- Match sync uses bracket identity (`BracketFullNumber` within its group), not the display `MatchNumber`. Structural changes regenerate display numbers.
- Keep DTO round-trip compatibility, but do not assume different app versions support the same synchronization semantics. Use the same build across tournament computers.

### Tournament parts and mat scheduling

`Tournament.Parts` is an ordered `ObservableCollection<TournamentPart>`; a part has an ID and name. `AgeWeightGroup.PartID` assigns a category to a part; `Mat.ActivePartID` selects the part currently running on that physical mat. The schedule editor is `Tournament/Standing/Mats/MatsViewModel.cs`; Conducting exposes mat operations and the mat board.

- `MatMatchNumbersGenerator` restarts numbering from 1 for each `(Mat, Part)` pair. Numbers may repeat across mats and across parts. It resets stale numbers, tracks assigned match instances and includes a catch-all for rounds not handled by the normal ordering.
- `IMatRedistributionService` / `MatRedistributionService` centralize category moves and related version/number updates. Reuse this path for schedule/board operations.
- Moves are guarded against a live match. Moving a category to another part is also blocked once it has completed matches other than automatic `FreeWin` byes, to preserve part results.
- Team results offer the sum of all parts and a per-part filter. Bulk exports that support part selection use `PartSelectorDialog`.
- `EntityToInfoAdapter` migrates files without parts by creating one default part and assigning its ID to groups and mats; it also normalizes orphan part references. Because migration creates a new GUID, open and save an old tournament on one computer before distributing the migrated file to peers.
- Part-list edits bump `Tournament.MetaVersion`; group moves bump group field versions; active-part changes bump mat field versions. Preserve these version contracts when extending scheduling.

### Results, slides and export

`IResultsService` caches calculated results and notifies consumers through `ResultsChanged`. Recalculation is wired to tournament open/close, match approval/revert and peer merge. Keep these consumers consistent when adding a mutation that affects results.

`Tournament.SlideChannels` stores named channels, each with its own slide list and interval. Legacy flat `Slides` are migrated into a default channel. `ISliderWindowManager` manages playback windows; `ISlideType.CreateViewControl()` creates separate view controls per host, rather than sharing a singleton rendering VM across windows. `MatBracketsSlide` expands into group-bracket slides and is not persisted as a playable slide.

Bulk PDF actions now live on their phases: `ApplicationsViewModel` (weighing), `DrawViewModel` (draw), `MatsViewModel` (schedule), `ResultsViewModel` (brackets/results). They reuse `BulkBracketPdfExporter` with view factories. Read `docs/PrintingNotes.md` before changing rendering or adding print views. Match control uses `MatchControlView2`; the score window uses `WwfScoreScreenView`.

### Error handling and crash recovery

`App.xaml.cs` installs three handlers (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`). On crash they log to the Logs folder and save a backup `.wrt` to the Backups folder. Prefer throwing over swallowing **in domain and UI code**, so the handlers can preserve user data. **Exception**: the I/O layer (`JsonStorageDataAccess`, `TournamentDataAccess` backup helpers) deliberately swallows expected FS/parse exceptions — a crash during a live match from a flaky WiFi import tick is worse than a silently-skipped read. See the "Load paths never throw" section above.

### Localization

- Resource files: `Wrestling.UI.Material/i18n/{ru,en}.json`. Add new keys to both. Russian is the original; English is the alternative.
- Helper: `TournamentViewModelBase.T(key, fallback)` — every snack/dialog string follows the shape `T("Snack_TournamentSaved", "Турнир сохранен!")`. The Russian text **is** the fallback, so a missing key never blanks the UI — it just falls back to Russian.
- Backing service: `ILocalizationService` in `Wrestling.UI.Utils.Localization`, registered in DI. Static accessor `LocalizationService.Instance` exists because helpers like `T()` need to be callable without DI plumbing (used from row/column VMs that inherit plain `ObservableObject`).
- Startup resolution order (`App.xaml.cs` `OnStartup`): saved operator preference (`LocalUiSettings.LanguageCode`) → OS UI culture two-letter code → `"en"` → first registered language. The operator's choice is persisted in `LocalUiSettings` (theme storage), not in the tournament `.wrt`.
- For static XAML strings prefer adding a `T()` call in a VM-exposed property over hardcoding — the codebase is mid-migration from hardcoded Russian to `T()` keys; new strings should land in both JSON files.

### UI conventions

- Material Design 3 via `MaterialDesignThemes` 5.x (DeepPurple / Lime, Light theme). Theme bundled inline in `App.xaml`; there is no separate resource dictionary file — add global styles in `App.xaml` or the nearest `Window.Resources`.
- Dialogs: `MvvmDialogs` (`IDialogService` injected via DI). Use it for file-open/save and for modal view-models; don't call `MessageBox` directly except in App-level error paths.
- Converters live in `Wrestling.UI.Utils/Converters/`. Prefer reusing existing ones (there are ~12, including `ValueConverterGroup` for composition) over adding new one-off converters in the UI project.
- Printing uses a custom `VisualPrinter` utility in `Wrestling.UI.Utils` rendering XAML UserControls — no ReportViewer / QuestPDF. Print views are `PrintXxxView` / `PrintXxxViewModel` pairs. Phase-specific bulk PDF actions use `BulkBracketPdfExporter`. **Before touching either utility — or adding a new XAML view to bulk export — read `docs/PrintingNotes.md`**: it documents WPF gotchas (off-tree rendering, why hosting in a Window caps content at screen height, why `IsVirtualizing="False"` matters off-tree, why `Math.Max(DesiredSize, ActualHeight)` is load-bearing after UpdateLayout) that have already burned us once.

## Conventions worth preserving

- Entity classes implement `INotifyPropertyChanged` directly (they double as view-model-bindable objects). Collections use `ObservableCollection<T>`. Don't convert to plain `List<T>` on domain objects.
- `Info` (DTO) classes in `Wrestling.Data` are plain property bags — keep them behaviorless. The **one** allowed piece of logic is a parameterless constructor that seeds safe defaults for new-schema fields so legacy `.wrt` files deserialize to sensible values (Newtonsoft calls it before overlaying JSON).
- File I/O in providers has both sync and async variants. Autosave and peer pulls use the **async** path; sync call sites remain for crash backup and `HomeViewModel`. When moving a command to async, use `AsyncRelayCommand` and await the operation instead of blocking the UI thread with `.Wait()` / `.Result`.
- `GlobalSettings` is persisted per-tournament (inside the `.wrt` file), not globally. New user-tunable settings belong there. Current fields worth knowing about: `IsBackupEnabled` / `MaxBackupCount` / `BackupFolderPath`, the match timing settings, slider settings.
- **Test access to internals**: `Wrestling.UI.Material` exposes internals to `Wrestling.UI.Material.Tests` via `Properties/InternalsVisibleTo.cs`. The csproj-based `AssemblyAttribute` trick doesn't work because `GenerateAssemblyInfo=false`. Use `internal` for methods that need direct test exercise.

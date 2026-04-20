# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

WPF desktop application for administrating and running freestyle wrestling tournaments: team/wrestler registration, automatic bracket generation, match control with live scoring and timers, multi-carpet (mat) scheduling, result calculation, broadcast/projection slides, and printed reports. UI is **Russian-only** (hardcoded `ru-RU` culture, no resource files) — keep new strings in Russian unless the user asks to introduce localization.

## Build and run

Solution file: `Wrestling.sln`. Libraries target `netstandard2.0`, UI projects target `net9.0-windows`. Release builds produce x64 binaries via `PlatformTarget` set in the csproj — the `.sln` platform mapping is intentionally kept at `AnyCPU` so a single `dotnet build -c Release` works without `-p:Platform=x64` tweaks. Windows-only (WPF).

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

Run: `dotnet test Wrestling.sln`. All three test projects target `net9.0` and use xUnit + FluentAssertions.

## Project layout and dependency direction

Strict layering — dependencies go bottom-up, never sideways or reversed:

```
Wrestling.Entities     (netstandard2.0) — domain model, bracket logic, result calculators
        ↑
Wrestling.Data         (netstandard2.0) — serializable DTOs ("*Info" classes)
        ↑
Wrestling.DataAccess   (netstandard2.0) — file-based repositories (JSON/XML)
        ↑
Wrestling.Providers    (netstandard2.0) — services (TournamentsManager, CacheManager, EntityToInfoAdapter)
        ↑
Wrestling.UI.Utils     (net9.0-windows) — hand-rolled MVVM primitives, converters, DI container
        ↑
Wrestling.UI.Material  (net9.0-windows) — WPF app (EXE): views, view models, navigation, DI wiring
```

Domain logic lives in `Wrestling.Entities` (computed properties like `IsApplicationValid`, scoring, validation). Keep it out of view models.

## Architecture — things you need to know before editing

### MVVM is hand-rolled, not CommunityToolkit/Prism

- Base class: `Wrestling.UI.Material/Model/ViewModelBase.cs`. Inherits `ObservableObject` from `Wrestling.Entities` (entities also raise `INotifyPropertyChanged` directly).
- Commands: `RelayCommand` and `AsyncRelayCommand` in `Wrestling.UI.Utils`. No `[RelayCommand]` attributes; declare commands as lazy-initialized properties.
- Don't introduce CommunityToolkit.Mvvm / Prism without an explicit ask — the pattern is consistent across the codebase.

### DI is a custom service locator, wired manually in App.xaml.cs

- Container: `DiContainer.Instance` (singleton), interface `IDiContainer` in `Wrestling.UI.Utils`. API: `Add<T>(instance)`, `Resolve<T>()`, plus keyed overloads (string-named registrations).
- **All registrations live in `Wrestling.UI.Material/App.xaml.cs` `OnStartup`.** ~50 lines of `di.Add<I>(new Impl(di.Resolve<Dep>()))`. Add new services there, in dependency order.
- `ViewModelBase.Resolve<T>()` delegates to `DiContainer.Instance`. View models do service-locator style resolution in `InitData()` — follow that pattern rather than constructor injection (nothing in the wiring supports constructor injection).

### Navigation: singleton view models + XAML DataTemplate dispatch

- `NavigationService` (`Wrestling.UI.Material/Model/NavigationService.cs`) holds a **list of pre-instantiated view model singletons** — one per page. Navigation swaps `MainWindowViewModel.CurrentViewModel` to the target instance; XAML `DataTemplate` entries in `MainWindow.xaml` pick the matching view.
- Register a new page by: (1) adding the VM to DI in `App.xaml.cs`, (2) adding it to the VM list in `NavigationService`, (3) adding a `<DataTemplate DataType="{x:Type …VM}">` entry in `MainWindow.xaml`.
- Because VMs are singletons that live for the whole app session, **do not hold per-navigation state in fields without resetting in `InitData()`**. `InitData()` is the "entering the page" hook; `OnNavigationCompleted()` runs after the view is bound.

### Persistence is file-based, no database

- Tournaments serialize to `.wrt` files (plain JSON via Newtonsoft.Json 13.x) via `ITournamentsManager` / `TournamentsManager` in `Wrestling.Providers`.
- Entity ↔ DTO mapping lives in `EntityToInfoAdapter` (`Wrestling.Providers`). When you add a new entity field, update **both** directions of the adapter and its `*Info` counterpart in `Wrestling.Data`, otherwise the field won't round-trip through save/load.
- Schema migration is implicit — use constructor defaults on the `*Info` DTO (Newtonsoft calls the parameterless ctor before overlaying JSON, so missing fields in old files stay at the ctor value) **and/or** adapter-level normalization (e.g. `if (entity.MaxBackupCount <= 0) entity.MaxBackupCount = 10`). Old `.wrt` files must keep opening.
- **Removing a persisted field is safe** — Newtonsoft silently drops unknown JSON properties on load. No migration needed. (Example: `IsVideoRecordingEnabled` / `VideStoragePath` were removed in 2026-04-20 cleanup; legacy files still open.)
- Wrestler / team caches: `Cache_Wrestlers.json`, `Cache_Teams.json` under `%LocalAppData%/WrestlingAdmin/`, managed by `CacheManager`. Crash backups: `%LocalAppData%/WrestlingAdmin/Backups/*.wrt`. Crash log: `%LocalAppData%/WrestlingAdmin/Logs/error_log_<yyyyMMdd>.txt`. Data-access log (load/backup failures, classified): `%LocalAppData%/WrestlingAdmin/Logs/data_log_<yyyyMMdd>.txt` via `FileLogger` in `Wrestling.DataAccess`.

### Tournament save pipeline — atomic write + backup + verify

`TournamentDataAccess.SaveToFile{,Async}` wraps `JsonStorageDataAccess` with three defense layers, in order:
1. **Pre-save backup** — copy the existing `.wrt` (if any) to `<dir>/Backups/<filename>/yyyyMMdd_HHmmss_fff.wrt` before overwrite. Default root is `<tournament-dir>/Backups/`; override via `GlobalSettings.BackupFolderPath` (absolute path wins; relative resolves against the tournament's directory). Per-tournament subfolder keeps multiple `.wrt`s sharing a working directory from mixing backups.
2. **Atomic write** — `JsonStorageDataAccess` serializes to `<file>.tmp.{guid}` then `File.Replace` swaps in. Torn writes are impossible.
3. **Post-save verification** — re-read the file, deserialize into `TournamentInfo`. On failure (rare but possible if serialization produces invalid JSON), restore the newest backup and return `false` (the UI surfaces "save failed" via snack bar; no exception propagates).
4. **Rotation** — drop oldest beyond `GlobalSettings.MaxBackupCount` (default 10).

Policy lives on the tournament being saved (`info.Settings.IsBackupEnabled` / `MaxBackupCount` / `BackupFolderPath`). When `IsBackupEnabled=false`, steps 1 and 4 are skipped (verification still runs).

**Backup operations are always best-effort.** Any `IOException` / `UnauthorizedAccessException` / `SecurityException` / `PathTooLongException` / `ArgumentException` / `NotSupportedException` is logged via `FileLogger` and swallowed — a backup failure must never prevent the actual save. Anything outside that set (OOM, AccessViolation, NRE from a bug) propagates so the global crash handler fires.

### Load paths never throw for expected I/O or parse errors

`JsonStorageDataAccess.ReadFromFile{,Async}` deliberately returns `default(T)` for file-not-found, `IOException`, `UnauthorizedAccessException`, `JsonException`, `SocketException`, `TimeoutException`, `ArgumentException`, `SecurityException`, `PathTooLongException`, `NotSupportedException`, `FormatException`. The import feature polls network/UNC paths on a timer during live matches; a WiFi drop must not crash the app. Each failure logs a classification tag (`Corrupt` / `AccessDenied` / `Transient` / `InvalidPath` / `NotFound` / `IO` / `Other`) to `data_log_<date>.txt` plus retry count. Network paths (UNC or mapped network drives) get 3–5 retries with exponential backoff; local paths one shot.

`ITournamentImporter` is split into two phases to keep UI responsive during timer-driven imports:

- **`PrepareAsync(target, fileName)`** — thread-pool-safe. Loads + deserializes + runs the adapter (the 50–200 ms CPU hit). Does not touch the target's `ObservableCollection<T>` graph. Returns an `ImportPlan` that either short-circuits (`ImportPlan.Skip(outcome)`) or carries the loaded remote tournament (`ImportPlan.Proceed(remote)`).
- **`Apply(target, plan)`** — UI-thread only. Walks the remote and merges any genuinely-new completions via `IGroupBracketProcessor.CompleteMatch`. Touches `ObservableCollection` and raises `INotifyPropertyChanged`. Fast (~1–10 ms) because only matches that actually flipped Pending→Completed are applied.

`ImportViewModel.ImportDataAsync` wraps `PrepareAsync` in `Task.Run` so the heavy work runs off the UI thread, then calls `Apply` synchronously on the captured (UI) context. This eliminates UI stutter even when an import tick fires during a live round timer. The final classified `ImportResult` (`Imported` / `NoNewData` / `FileUnavailable` / `TournamentMismatch` / `Error`) drives the per-outcome log message and the autosave gate.

### Autosave is event-driven, not timer-based

There is **no** DispatcherTimer for autosave. Saves fire only after:
1. **Match completion** — `MatchResultsViewModel.ApproveAsync` calls `SaveIfAutosaveEnabledAsync()` after the processor's `CompleteMatch()` runs.
2. **Successful import** — `ImportViewModel.ImportDataAsync` calls `SaveIfAutosaveEnabledAsync()` only when the outcome is `Imported` (not for `NoNewData`, `FileUnavailable`, etc.).

The gate is a public method on `TournamentViewModelBase`:

```csharp
public async Task SaveIfAutosaveEnabledAsync()
{
    if (IsAutosaveEnabled && DataContext.Tournament != null)
        await SaveDataAsync();
}
```

The Settings UI exposes only the `IsAutosaveEnabled` toggle. The "Сохранить турнир" quick button is **always visible on the dashboard** regardless of the flag — autosave only covers match/import events, so other mutations (team/wrestler registration, bracket generation, schedule edits) rely on manual save. (The old `AutosaveMaxSecond` interval field was fully removed 2026-04-20; legacy `.wrt` files containing it load fine since Newtonsoft silently drops unknown JSON properties.)

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

### Multi-carpet synchronization

Each carpet (mat) PC keeps its own local copy of the tournament `.wrt`. Results move between carpets via the **Import** feature (`ImportViewModel` + `TournamentImporter`) — not a shared file. The import path polls remote laptops on a timer, typically over UNC paths like `\\192.168.x.x\share\tournament.wrt`, and merges completed matches into the local tournament. Changes to the import flow or `.wrt` schema must keep round-trip compatibility so peer carpets with slightly different app versions can still read each other's saves.

### Error handling and crash recovery

`App.xaml.cs` installs three handlers (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`). On crash they log to the Logs folder and save a backup `.wrt` to the Backups folder. Prefer throwing over swallowing **in domain and UI code**, so the handlers can preserve user data. **Exception**: the I/O layer (`JsonStorageDataAccess`, `TournamentDataAccess` backup helpers) deliberately swallows expected FS/parse exceptions — a crash during a live match from a flaky WiFi import tick is worse than a silently-skipped read. See the "Load paths never throw" section above.

### UI conventions

- Material Design 3 via `MaterialDesignThemes` 5.x (DeepPurple / Lime, Light theme). Theme bundled inline in `App.xaml`; there is no separate resource dictionary file — add global styles in `App.xaml` or the nearest `Window.Resources`.
- Dialogs: `MvvmDialogs` (`IDialogService` injected via DI). Use it for file-open/save and for modal view-models; don't call `MessageBox` directly except in App-level error paths.
- Converters live in `Wrestling.UI.Utils/Converters/`. Prefer reusing existing ones (there are ~12, including `ValueConverterGroup` for composition) over adding new one-off converters in the UI project.
- Printing uses a custom `VisualPrinter` utility in `Wrestling.UI.Utils` rendering XAML UserControls — no ReportViewer / QuestPDF. Print views are `PrintXxxView` / `PrintXxxViewModel` pairs.

## Conventions worth preserving

- Entity classes implement `INotifyPropertyChanged` directly (they double as view-model-bindable objects). Collections use `ObservableCollection<T>`. Don't convert to plain `List<T>` on domain objects.
- `Info` (DTO) classes in `Wrestling.Data` are plain property bags — keep them behaviorless. The **one** allowed piece of logic is a parameterless constructor that seeds safe defaults for new-schema fields so legacy `.wrt` files deserialize to sensible values (Newtonsoft calls it before overlaying JSON).
- File I/O in providers has both sync and async variants. Autosave and import now use the **async** path end-to-end; a few sync call sites remain (settings toggle, crash backup, `HomeViewModel`). When moving a call to async, the invoking command must be an `AsyncRelayCommand` (not `RelayCommand`) or the UI will deadlock.
- `GlobalSettings` is persisted per-tournament (inside the `.wrt` file), not globally. New user-tunable settings belong there. Current fields worth knowing about: `IsAutosaveEnabled`, `IsBackupEnabled` / `MaxBackupCount` / `BackupFolderPath`, the match timing settings, slider settings.
- **Test access to internals**: `Wrestling.UI.Material` exposes internals to `Wrestling.UI.Material.Tests` via `Properties/InternalsVisibleTo.cs`. The csproj-based `AssemblyAttribute` trick doesn't work because `GenerateAssemblyInfo=false`. Use `internal` for methods that need direct test exercise (e.g. `ImportViewModel.ImportDataAsync`).

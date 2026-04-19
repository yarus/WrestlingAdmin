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
- Schema migration is implicit — the adapter applies defaults for missing fields on load (see patterns like `if (entity.Settings.MaxRoundSecond == 0) entity.Settings.MaxRoundSecond = …`). Use that pattern when introducing new persisted properties so existing `.wrt` files still open.
- Wrestler / team caches: `Cache_Wrestlers.json`, `Cache_Teams.json` under `%LocalAppData%/WrestlingAdmin/`, managed by `CacheManager`. Backups on crash: `%LocalAppData%/WrestlingAdmin/Backups/*.wrt`. Error logs: `%LocalAppData%/WrestlingAdmin/Logs/error_log_<yyyyMMdd>.txt`.

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

Carpets (mats) running on networked PCs sync results through a shared folder — the tournament file is the sync medium. Changes to save/load paths or the `.wrt` format must preserve round-trip compatibility across machines that may briefly disagree on state.

### Error handling and crash recovery

`App.xaml.cs` installs three handlers (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`). On crash they log to the Logs folder and save a backup `.wrt` to the Backups folder. When adding code on hot paths, prefer throwing over swallowing — the handlers will preserve user data.

### UI conventions

- Material Design 3 via `MaterialDesignThemes` 5.x (DeepPurple / Lime, Light theme). Theme bundled inline in `App.xaml`; there is no separate resource dictionary file — add global styles in `App.xaml` or the nearest `Window.Resources`.
- Dialogs: `MvvmDialogs` (`IDialogService` injected via DI). Use it for file-open/save and for modal view-models; don't call `MessageBox` directly except in App-level error paths.
- Converters live in `Wrestling.UI.Utils/Converters/`. Prefer reusing existing ones (there are ~12, including `ValueConverterGroup` for composition) over adding new one-off converters in the UI project.
- Printing uses a custom `VisualPrinter` utility in `Wrestling.UI.Utils` rendering XAML UserControls — no ReportViewer / QuestPDF. Print views are `PrintXxxView` / `PrintXxxViewModel` pairs.

## Conventions worth preserving

- Entity classes implement `INotifyPropertyChanged` directly (they double as view-model-bindable objects). Collections use `ObservableCollection<T>`. Don't convert to plain `List<T>` on domain objects.
- `Info` (DTO) classes in `Wrestling.Data` are plain property bags. Keep them behaviorless — all logic goes in the entity or adapter.
- File I/O in providers has both sync and async variants; the UI currently uses the **sync** path from view models. If you move a call to async, make sure the invoking command is an `AsyncRelayCommand` (not `RelayCommand`) or the UI will deadlock / freeze.
- `GlobalSettings` is persisted per-tournament (inside the `.wrt` file), not globally. New user-tunable settings belong there.

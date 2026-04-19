using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using MvvmDialogs;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Wrestling.Entities;
using Wrestling.Providers;
using Wrestling.UI.Material.Model;
using WTournament = Wrestling.Entities.Tournament;

namespace Wrestling.UI.Material.Tests.Fakes;

public sealed class FakeShellViewModel : IShellViewModel
{
    public ViewModelBase CurrentViewModel { get; set; }
    public List<string> Snackbar { get; } = new();
    public int CloseRequests { get; private set; }

    public void ShowSnackbarMessage(string message) => Snackbar.Add(message);
    public void RequestClose() => CloseRequests++;
}

public sealed class FakeNavigationService : INavigationService
{
    public IShellViewModel ShellVm { get; set; } = new FakeShellViewModel();
    public List<Type> NavigatedTo { get; } = new();
    public int CloseApps { get; private set; }
    public List<ViewModelBase> PrintPreviews { get; } = new();

    public void LoadNavigation() { }
    public void NavigateToView<T>() where T : ViewModelBase => NavigatedTo.Add(typeof(T));
    public void ShowPrintPreview(ViewModelBase vm) => PrintPreviews.Add(vm);
    public void CloseApp() => CloseApps++;
}

public sealed class FakeTournamentsManager : ITournamentsManager
{
    public Dictionary<string, WTournament> Store { get; } = new();
    public int SaveCount { get; private set; }
    public int SaveAsyncCount { get; private set; }

    public WTournament LoadFromFile(string fileName) => Store.TryGetValue(fileName, out var t) ? t : null;
    public Task<WTournament> LoadFromFileAsync(string fileName) => Task.FromResult(LoadFromFile(fileName));

    public bool SaveToFile(WTournament item, string fileName)
    {
        Store[fileName] = item;
        item.FileName = fileName;
        SaveCount++;
        return true;
    }

    public Task<bool> SaveToFileAsync(WTournament item, string fileName)
    {
        Store[fileName] = item;
        item.FileName = fileName;
        SaveAsyncCount++;
        return Task.FromResult(true);
    }
}

public sealed class FakeCacheManager : ICacheManager
{
    public List<TeamApplication> Teams { get; set; } = new();
    public List<Wrestler> Wrestlers { get; set; } = new();

    public List<TeamApplication> LoadTeams() => Teams;
    public List<Wrestler> LoadWrestlers() => Wrestlers;
    public bool SaveTeams(List<TeamApplication> list) { Teams = list; return true; }
    public bool SaveWrestlers(List<Wrestler> list) { Wrestlers = list; return true; }
}

// Inherit from the real DialogService so the full interface surface is covered
// across MvvmDialogs versions without hand-rolling every overload.
public sealed class FakeDialogService : DialogService
{
    public List<string> ShownMessages { get; } = new();
    public MessageBoxResult MessageBoxResponse { get; set; } = MessageBoxResult.OK;
    public bool? OpenFileResponse { get; set; }
    public bool? SaveFileResponse { get; set; }

    public MessageBoxResult ShowMessageBox(INotifyPropertyChanged ownerViewModel, string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None, MessageBoxOptions options = MessageBoxOptions.None)
    {
        ShownMessages.Add(messageBoxText);
        return MessageBoxResponse;
    }

    public new bool? ShowOpenFileDialog(INotifyPropertyChanged ownerViewModel, OpenFileDialogSettings settings) => OpenFileResponse;
    public new bool? ShowSaveFileDialog(INotifyPropertyChanged ownerViewModel, SaveFileDialogSettings settings) => SaveFileResponse;
}

public static class TestContainerBuilder
{
    public static TestDiContainer MakeDefault()
    {
        var di = new TestDiContainer();
        var shell = new FakeShellViewModel();
        var nav = new FakeNavigationService { ShellVm = shell };

        di.Add<IDataContext>(new DataContext());
        di.Add<IDialogService>(new FakeDialogService());
        di.Add<INavigationService>(nav);
        di.Add<GlobalSettings>(new GlobalSettings());
        di.Add<ITournamentsManager>(new FakeTournamentsManager());
        di.Add<ICacheManager>(new FakeCacheManager());

        return di;
    }
}

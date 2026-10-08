using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Models;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>设置页：主题、打开配置目录、清空图标缓存。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IStore _store;
    private readonly IShellService _shell;
    private readonly IIconCache _icons;
    private readonly IThemeService _themes;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly Action _requestSave;

    [ObservableProperty]
    private string _theme;

    public SettingsViewModel(
        IStore store,
        IShellService shell,
        IIconCache icons,
        IThemeService themes,
        IDialogService dialogs,
        AppSettings settings,
        Action requestSave)
    {
        _store = store;
        _shell = shell;
        _icons = icons;
        _themes = themes;
        _dialogs = dialogs;
        _settings = settings;
        _requestSave = requestSave;
        _theme = settings.Theme;
    }

    public bool IsDarkTheme
    {
        get => Theme == ThemeOptions.Dark;
        set => Theme = value ? ThemeOptions.Dark : ThemeOptions.Light;
    }

    public string DataDirectory => _store.DataDirectory;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Microsoft.Performance",
        "CA1822:MarkMembersAsStatic",
        Justification = "供 XAML 绑定使用，必须是实例属性。")]
    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    partial void OnThemeChanged(string value)
    {
        var normalized = value == ThemeOptions.Dark ? ThemeOptions.Dark : ThemeOptions.Light;
        if (normalized != value)
        {
            Theme = normalized;
            return;
        }

        _themes.Apply(normalized);
        _settings.Theme = normalized;
        OnPropertyChanged(nameof(IsDarkTheme));
        _requestSave();
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        if (!_shell.Open(_store.DataDirectory, out var error))
        {
            _dialogs.ShowError("打开失败", error ?? _store.DataDirectory);
        }
    }

    [RelayCommand]
    private void ClearIconCache() => _icons.Clear();
}

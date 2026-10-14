using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Models;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>设置页：主题、「常用」收集规则、打开配置目录、清空图标缓存。</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IStore _store;
    private readonly IShellService _shell;
    private readonly IIconCache _icons;
    private readonly IThemeService _themes;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly Action _requestSave;

    /// <summary>「常用」的开关或条数变了，让 MainViewModel 把上限同步给内置 Sheet 并立刻裁剪。</summary>
    private readonly Action _onFrequentChanged;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private bool _frequentEnabled;

    /// <summary>
    /// 「常用」条数上限的输入文本。用字符串而不是直接绑 int：输入框里可以是空的或半截数字，
    /// 提交（默认 LostFocus）时再解析、钳制。
    /// </summary>
    [ObservableProperty]
    private string _frequentMaxCountText;

    public SettingsViewModel(
        IStore store,
        IShellService shell,
        IIconCache icons,
        IThemeService themes,
        IDialogService dialogs,
        AppSettings settings,
        Action requestSave,
        Action onFrequentChanged)
    {
        _store = store;
        _shell = shell;
        _icons = icons;
        _themes = themes;
        _dialogs = dialogs;
        _settings = settings;
        _requestSave = requestSave;
        _onFrequentChanged = onFrequentChanged;
        _theme = settings.Theme;
        // 直接写后备字段：构造期间的赋值不该触发写盘与裁剪
        _frequentEnabled = settings.FrequentEnabled;
        _frequentMaxCountText = settings.FrequentMaxCount.ToString();
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

    partial void OnFrequentEnabledChanged(bool value)
    {
        if (_settings.FrequentEnabled == value)
        {
            return;
        }

        _settings.FrequentEnabled = value;
        _requestSave();
        _onFrequentChanged();
    }

    /// <summary>
    /// 条数上限提交（TextBox 默认在失去焦点时提交）：解析成功且落在 1..100 才采纳。
    /// 空 / 非数字回写成当前值（清空输入框不该把上限变成 0），越界回写成边界值 ——
    /// 回写会再进来一次，那时解析成功且值相同，直接返回，不会递归。
    /// </summary>
    partial void OnFrequentMaxCountTextChanged(string value)
    {
        if (!int.TryParse(value?.Trim(), out var parsed))
        {
            FrequentMaxCountText = _settings.FrequentMaxCount.ToString();
            return;
        }

        var clamped = Math.Clamp(parsed, AppSettings.MinFrequentMax, AppSettings.MaxFrequentMax);
        if (clamped != parsed)
        {
            FrequentMaxCountText = clamped.ToString();
            return;
        }

        if (clamped == _settings.FrequentMaxCount)
        {
            return;
        }

        _settings.FrequentMaxCount = clamped;
        _requestSave();
        _onFrequentChanged();
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

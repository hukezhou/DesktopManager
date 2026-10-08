using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>
/// 添加/修改行的对话框：选择文件、文件夹或快捷方式，名称默认取目标本来的名称，
/// 图标默认取目标原始图标，可换成自选 .ico/.png。
/// </summary>
public sealed partial class LinkEditDialogViewModel : DialogViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IIconCache _icons;
    private readonly IStore _store;
    private readonly IShellService _shell;

    private bool _nameIsAuto = true;
    private bool _applyingAutoName;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _customIconPath;

    /// <summary>
    /// 用户新选中的图标源文件。先不复制，确认时才复制进图标目录，
    /// 这样“选了又取消”不会在磁盘上留下孤儿副本。
    /// </summary>
    [ObservableProperty]
    private string? _pendingIconSource;

    public LinkEditDialogViewModel(IDialogService dialogs, IIconCache icons, IStore store, IShellService shell)
    {
        _dialogs = dialogs;
        _icons = icons;
        _store = store;
        _shell = shell;
    }

    [ObservableProperty]
    private LinkEditMode _mode = LinkEditMode.Add;

    public bool IsAddMode => Mode == LinkEditMode.Add;

    partial void OnModeChanged(LinkEditMode value)
    {
        Title = value == LinkEditMode.Add ? "添加" : "修改";
        OnPropertyChanged(nameof(IsAddMode));
    }

    public bool HasTarget => !string.IsNullOrWhiteSpace(TargetPath);

    /// <summary>当前生效的自定义图标来源（新选的文件优先）。</summary>
    private string? EffectiveIconSource => PendingIconSource ?? CustomIconPath;

    public bool HasCustomIcon => EffectiveIconSource is not null;

    /// <summary>预览图标：优先自定义图标，其次目标原始图标。</summary>
    public ImageSource? Icon
    {
        get
        {
            var source = EffectiveIconSource;
            if (source is not null)
            {
                return _icons.GetImageFile(source);
            }

            return string.IsNullOrWhiteSpace(TargetPath) ? null : _icons.GetShellIcon(TargetPath);
        }
    }

    /// <summary>快捷方式解析后的真实目标，用于提示。</summary>
    public string ResolvedTarget => string.IsNullOrWhiteSpace(TargetPath)
        ? string.Empty
        : _shell.ResolveTarget(TargetPath);

    partial void OnTargetPathChanged(string value)
    {
        if (_nameIsAuto && !string.IsNullOrWhiteSpace(value))
        {
            _applyingAutoName = true;
            DisplayName = FriendlyName(value);
            _applyingAutoName = false;
        }

        OnPropertyChanged(nameof(HasTarget));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(ResolvedTarget));
        AcceptCommand.NotifyCanExecuteChanged();
    }

    partial void OnDisplayNameChanged(string value)
    {
        if (!_applyingAutoName)
        {
            _nameIsAuto = false;
        }

        AcceptCommand.NotifyCanExecuteChanged();
    }

    partial void OnCustomIconPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCustomIcon));
        OnPropertyChanged(nameof(Icon));
    }

    partial void OnPendingIconSourceChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCustomIcon));
        OnPropertyChanged(nameof(Icon));
    }

    private bool CanAccept() => !string.IsNullOrWhiteSpace(TargetPath) && !string.IsNullOrWhiteSpace(DisplayName);

    [RelayCommand]
    private void PickFile() => ApplyTarget(_dialogs.PickFile() ?? TargetPath);

    [RelayCommand]
    private void PickFolder()
    {
        var folder = _dialogs.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ApplyTarget(folder);
        }
    }

    private void ApplyTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        TargetPath = path;
    }

    [RelayCommand]
    private void PickIcon()
    {
        var picked = _dialogs.PickIconFile();
        if (picked is not null)
        {
            PendingIconSource = picked;
        }
    }

    /// <summary>清除自定义图标，恢复目标本来的图标。</summary>
    [RelayCommand]
    private void ClearIcon()
    {
        PendingIconSource = null;
        CustomIconPath = null;
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        // 到这一步才真正复制，取消时磁盘上不会留下副本。
        if (PendingIconSource is not null)
        {
            var imported = _store.ImportIconFile(PendingIconSource);
            if (imported is null)
            {
                _dialogs.ShowError("无法复制图标", "选定的图标文件无法复制到应用数据目录。");
                return;
            }

            CustomIconPath = imported;
            PendingIconSource = null;
        }

        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    private static string FriendlyName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
        {
            return path;
        }

        var extension = Path.GetExtension(name);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }
}

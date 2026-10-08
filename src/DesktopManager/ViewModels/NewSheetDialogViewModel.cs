using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>
/// 侧边栏 “+” 新建 Sheet：命名 + 选图标（12 个预设徽章，或自选 .ico/.png）。
/// 同一个对话框也用于编辑已有项。
/// </summary>
public sealed partial class NewSheetDialogViewModel : DialogViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IIconCache _icons;
    private readonly IStore _store;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _name = string.Empty;

    /// <summary>确认后才写入的最终图标路径（已复制到图标目录）。</summary>
    [ObservableProperty]
    private string? _iconPath;

    /// <summary>用户新选中的图标源文件；先不复制，取消时不留孤儿副本。</summary>
    [ObservableProperty]
    private string? _pendingIconSource;

    /// <summary>选中的预设图标（矢量徽章，不产生文件）。与自定义文件图标互斥。</summary>
    [ObservableProperty]
    private PresetIcon? _selectedPreset;

    public NewSheetDialogViewModel(IDialogService dialogs, IIconCache icons, IStore store, string title = "新建侧边栏项")
    {
        _dialogs = dialogs;
        _icons = icons;
        _store = store;
        Title = title;
    }

    /// <summary>对话框里展示的预设图标列表（实例属性：XAML 要按实例绑定）。</summary>
    public IReadOnlyList<PresetIcon> Presets { get; } = PresetIcons.All;

    public bool HasIcon => PendingIconSource is not null || IconPath is not null || SelectedPreset is not null;

    public ImageSource? Icon
    {
        get
        {
            var source = PendingIconSource ?? IconPath;
            if (source is not null)
            {
                return _icons.GetImageFile(source);
            }

            return SelectedPreset?.Image;
        }
    }

    partial void OnIconPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(Icon));
    }

    partial void OnPendingIconSourceChanged(string? value)
    {
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(Icon));
    }

    partial void OnSelectedPresetChanged(PresetIcon? value)
    {
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(Icon));

        if (value is null)
        {
            return;
        }

        // 选了预设就把待复制的文件图标取消；名称为空时顺手填上预设名
        PendingIconSource = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = value.Label;
        }
    }

    private bool CanAccept() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand]
    private void BrowseIcon()
    {
        var picked = _dialogs.PickIconFile();
        if (picked is null)
        {
            return;
        }

        // 文件图标与预设图标互斥：选了文件就把预设取消掉（null 不会反过来清 PendingIconSource）
        SelectedPreset = null;
        PendingIconSource = picked;
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Path.GetFileNameWithoutExtension(picked);
        }
    }

    [RelayCommand]
    private void ClearIcon()
    {
        PendingIconSource = null;
        IconPath = null;
        SelectedPreset = null;
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        if (PendingIconSource is not null)
        {
            var imported = _store.ImportIconFile(PendingIconSource);
            if (imported is null)
            {
                _dialogs.ShowError("无法复制图标", "选定的图标文件无法复制到应用数据目录。");
                return;
            }

            IconPath = imported;
            PendingIconSource = null;
        }
        else if (SelectedPreset is not null)
        {
            // 选了预设就不留文件图标：这里清掉，调用方发现旧路径与新值不同会删掉旧副本
            IconPath = null;
        }

        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}

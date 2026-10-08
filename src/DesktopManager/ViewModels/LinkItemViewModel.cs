using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Models;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>
/// 用户栏中的一行。直接包装 <see cref="LinkItem"/> 模型，属性写回模型以便持久化。
/// </summary>
public sealed partial class LinkItemViewModel : ObservableObject
{
    private readonly LinkItem _model;
    private readonly SheetViewModel _owner;
    private readonly IIconCache _icons;

    private ImageSource? _icon;
    private bool _iconResolved;

    public LinkItemViewModel(LinkItem model, SheetViewModel owner, IIconCache icons)
    {
        _model = model;
        _owner = owner;
        _icons = icons;
    }

    /// <summary>供 Sheet 做覆盖项管理时使用。</summary>
    public LinkItem Model => _model;

    public string Id => _model.Id;

    public string TargetPath
    {
        get => _model.TargetPath;
        set
        {
            if (_model.TargetPath == value)
            {
                return;
            }

            _model.TargetPath = value;
            OnPropertyChanged();
            RefreshIcon();
        }
    }

    public string DisplayName
    {
        get => _model.DisplayName;
        set
        {
            if (_model.DisplayName == value)
            {
                return;
            }

            _model.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public string? CustomIconPath
    {
        get => _model.CustomIconPath;
        set
        {
            if (_model.CustomIconPath == value)
            {
                return;
            }

            _model.CustomIconPath = value;
            OnPropertyChanged();
            RefreshIcon();
        }
    }

    public bool IsPinned
    {
        get => _model.IsPinned;
        set
        {
            if (_model.IsPinned == value)
            {
                return;
            }

            _model.IsPinned = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PinMenuHeader));
        }
    }

    public DateTime? PinnedAt
    {
        get => _model.PinnedAt;
        set
        {
            if (_model.PinnedAt == value)
            {
                return;
            }

            _model.PinnedAt = value;
            OnPropertyChanged();
        }
    }

    public int OpenCount
    {
        get => _model.OpenCount;
        set
        {
            if (_model.OpenCount == value)
            {
                return;
            }

            _model.OpenCount = value;
            OnPropertyChanged();
        }
    }

    public int SortOrder
    {
        get => _model.SortOrder;
        set
        {
            if (_model.SortOrder == value)
            {
                return;
            }

            _model.SortOrder = value;
            OnPropertyChanged();
        }
    }

    /// <summary>右键菜单第一项的标题，随置顶状态切换。</summary>
    public string PinMenuHeader => IsPinned ? "取消置顶" : "置顶";

    /// <summary>
    /// 桌面 Sheet 的行没有“从列表移除”：那一行来自磁盘扫描，去掉它只能删磁盘文件，
    /// 而本工具从不删磁盘文件。所以右键菜单里直接不显示这一项。
    /// </summary>
    public bool CanRemove => !_owner.IsDesktop;

    /// <summary>行图标：自定义图标优先，其次是目标本来的图标。</summary>
    public ImageSource? Icon
    {
        get
        {
            if (!_iconResolved)
            {
                _icon = ResolveIcon();
                _iconResolved = true;
            }

            return _icon;
        }
    }

    public void RefreshIcon()
    {
        _iconResolved = false;
        OnPropertyChanged(nameof(Icon));
    }

    private ImageSource? ResolveIcon()
    {
        if (!string.IsNullOrWhiteSpace(CustomIconPath))
        {
            var custom = _icons.GetImageFile(CustomIconPath!);
            if (custom is not null)
            {
                return custom;
            }
        }

        return _icons.GetShellIcon(TargetPath);
    }

    [RelayCommand]
    private void Open() => _owner.OpenItem(this);

    [RelayCommand]
    private void Pin() => _owner.TogglePin(this);

    [RelayCommand]
    private void Edit() => _owner.EditItem(this);

    [RelayCommand]
    private void Remove() => _owner.RemoveItem(this);

    /// <summary>让无障碍/UIA 与自动化测试读到行名称而不是类型名。</summary>
    public override string ToString() => DisplayName;
}

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DesktopManager.ViewModels;

/// <summary>侧边栏中的一个图标（对应一个 Sheet 或设置页）。</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    private readonly Action<NavItemViewModel> _onSelect;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>悬停时显示的名称（Tooltip）。改名后侧边栏要跟着变，所以是可通知属性。</summary>
    [ObservableProperty]
    private string _title;

    /// <summary>自定义 Sheet 的图标；换图标后侧边栏要跟着变，所以是可通知属性。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _icon;

    public NavItemViewModel(string title, object content, Action<NavItemViewModel> onSelect, string? glyph = null, ImageSource? icon = null)
    {
        _title = title;
        Content = content;
        _onSelect = onSelect;
        Glyph = glyph;
        _icon = icon;
    }

    /// <summary>要显示到内容区的 ViewModel。</summary>
    public object Content { get; }

    /// <summary>内置图标用字形；自定义 Sheet 用 Icon。</summary>
    public string? Glyph { get; }

    public bool HasIcon => Icon is not null;

    /// <summary>
    /// 能否参与长按拖拽手势 —— 既能作为源（拖到回收站移除、原地释放编辑），
    /// 也能作为交换目标（另一个项拖到它上面释放）。
    /// 内置“桌面”Sheet 与设置页没有可删的记录，删了也没有意义，而且“桌面”启动时永远排第一，
    /// 交换了也保存不住，所以两个角色都不参与。
    /// </summary>
    public bool CanDrag => Content is SheetViewModel { IsDesktop: false };

    [RelayCommand]
    private void Select() => _onSelect(this);
}

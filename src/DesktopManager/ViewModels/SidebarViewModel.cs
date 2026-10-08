using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DesktopManager.ViewModels;

/// <summary>
/// 侧边栏：宽度固定为一列图标。上部是 Sheet 列表与 “+” 添加按钮，底部是设置入口。
/// 长按某个 Sheet 项时 “+” 会变成回收站（<see cref="IsRecycleVisible"/>），
/// 拖到它上方释放即移除该 Sheet，拖到另一个 Sheet 项上释放即交换两者位置，原地释放则编辑它。
/// </summary>
public sealed partial class SidebarViewModel : ObservableObject
{
    private readonly Action _requestAddSheet;
    private readonly Action<NavItemViewModel> _requestEditSheet;
    private readonly Action<NavItemViewModel> _requestRemoveSheet;
    private readonly Action<NavItemViewModel, NavItemViewModel> _requestSwapSheets;

    /// <summary>拖拽态：”+” 显示成回收站。</summary>
    [ObservableProperty]
    private bool _isRecycleVisible;

    /// <summary>拖拽态里指针正压在回收站上：变危险红。</summary>
    [ObservableProperty]
    private bool _isRecycleHot;

    public SidebarViewModel(
        Action requestAddSheet,
        Action<NavItemViewModel> requestEditSheet,
        Action<NavItemViewModel> requestRemoveSheet,
        Action<NavItemViewModel, NavItemViewModel> requestSwapSheets,
        NavItemViewModel settingsItem)
    {
        _requestAddSheet = requestAddSheet;
        _requestEditSheet = requestEditSheet;
        _requestRemoveSheet = requestRemoveSheet;
        _requestSwapSheets = requestSwapSheets;
        SettingsItem = settingsItem;
    }

    /// <summary>桌面 Sheet 与所有自定义 Sheet。</summary>
    public ObservableCollection<NavItemViewModel> Items { get; } = new();

    /// <summary>底部设置入口。</summary>
    public NavItemViewModel SettingsItem { get; }

    [RelayCommand]
    private void AddSheet() => _requestAddSheet();

    /// <summary>拖拽手势结束在回收站上：请求移除该 Sheet（确认与真正删除在 MainViewModel）。</summary>
    public void RequestRemoveSheet(NavItemViewModel item) => _requestRemoveSheet(item);

    /// <summary>拖拽手势原地释放：请求编辑该 Sheet 的名称与图标。</summary>
    public void RequestEditSheet(NavItemViewModel item) => _requestEditSheet(item);

    /// <summary>拖拽手势释放到另一个 Sheet 项上：请求交换两者位置（重排与落盘在 MainViewModel）。</summary>
    public void RequestSwapSheets(NavItemViewModel source, NavItemViewModel target) => _requestSwapSheets(source, target);
}

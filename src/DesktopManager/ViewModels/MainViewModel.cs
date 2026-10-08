using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Models;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>
/// 主窗口 ViewModel：组合侧边栏、内容区（Sheet / 设置）与标题栏命令。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const string DesktopGlyph = "\uE7F4";
    private const string SettingsGlyph = "\uE713";
    private const string DefaultSheetGlyph = "\uE8B7";

    private readonly IStore _store;
    private readonly IDialogService _dialogs;
    private readonly IIconCache _icons;
    private readonly IShellService _shell;
    private readonly IThemeService _themes;
    private readonly IDesktopScanner _scanner;
    private readonly AppSettings _settings;

    private IWindowController? _window;

    [ObservableProperty]
    private object? _selectedContent;

    [ObservableProperty]
    private bool _isTopmost;

    public MainViewModel(
        IStore store,
        IDialogService dialogs,
        IIconCache icons,
        IShellService shell,
        IThemeService themes,
        IDesktopScanner scanner)
    {
        _store = store;
        _dialogs = dialogs;
        _icons = icons;
        _shell = shell;
        _themes = themes;
        _scanner = scanner;
        _settings = store.LoadSettings();

        IsTopmost = _settings.Topmost;

        Settings = new SettingsViewModel(store, shell, icons, themes, dialogs, _settings, Save);
        SettingsNav = new NavItemViewModel("设置", Settings, OnNavSelected, SettingsGlyph);
        Sidebar = new SidebarViewModel(AddSheet, EditSheet, RemoveSheet, SwapSheets, SettingsNav);

        LoadSheets();
    }

    public SidebarViewModel Sidebar { get; }

    public SettingsViewModel Settings { get; }

    public NavItemViewModel SettingsNav { get; }

    public ObservableCollection<SheetViewModel> Sheets { get; } = new();

    /// <summary>上次保存的窗口边界；没有记录时返回 null。</summary>
    public WindowBounds? SavedBounds
    {
        get
        {
            if (_settings.WindowWidth is not double width || _settings.WindowHeight is not double height)
            {
                return null;
            }

            return new WindowBounds(
                _settings.WindowLeft ?? double.NaN,
                _settings.WindowTop ?? double.NaN,
                width,
                height,
                _settings.WindowMaximized);
        }
    }

    /// <summary>
    /// 记录窗口边界。NaN 表示该项不更新（例如最大化时不写宽高与位置）。
    /// 数值没变化时不写盘，避免启动与无操作时的空写入。
    /// </summary>
    public void UpdateWindowBounds(WindowBounds bounds)
    {
        var changed = false;

        if (double.IsFinite(bounds.Width) && bounds.Width != _settings.WindowWidth)
        {
            _settings.WindowWidth = bounds.Width;
            changed = true;
        }

        if (double.IsFinite(bounds.Height) && bounds.Height != _settings.WindowHeight)
        {
            _settings.WindowHeight = bounds.Height;
            changed = true;
        }

        if (double.IsFinite(bounds.Left) && bounds.Left != _settings.WindowLeft)
        {
            _settings.WindowLeft = bounds.Left;
            changed = true;
        }

        if (double.IsFinite(bounds.Top) && bounds.Top != _settings.WindowTop)
        {
            _settings.WindowTop = bounds.Top;
            changed = true;
        }

        if (bounds.Maximized != _settings.WindowMaximized)
        {
            _settings.WindowMaximized = bounds.Maximized;
            changed = true;
        }

        if (changed)
        {
            SaveSettings();
        }
    }

    /// <summary>由主窗口调用，用于最小化/最大化/关闭，并把上次尺寸推给窗口。</summary>
    public void AttachWindow(IWindowController controller)
    {
        _window = controller;
        controller.SetBounds(SavedBounds);
    }

    /// <summary>数据变更：设置与 Sheet 一起写。</summary>
    public void Save()
    {
        _store.SaveSettings(_settings);
        _store.SaveSheets(Sheets.Select(s => s.Model));
    }

    /// <summary>只有设置变化（主题、置顶、窗口几何、上次 Sheet）：不必重写 sheets.json。</summary>
    private void SaveSettings() => _store.SaveSettings(_settings);

    partial void OnIsTopmostChanged(bool value)
    {
        _settings.Topmost = value;
        SaveSettings();
    }

    [RelayCommand]
    private void Minimize() => _window?.Minimize();

    [RelayCommand]
    private void ToggleMaximize() => _window?.ToggleMaximize();

    [RelayCommand]
    private void CloseWindow() => _window?.CloseWindow();

    private void LoadSheets()
    {
        var definitions = _store.LoadSheets();
        var desktop = definitions.FirstOrDefault(d => d.Kind == SheetKind.Desktop);
        if (desktop is null)
        {
            desktop = new SheetDefinition { Title = "桌面", Kind = SheetKind.Desktop, Order = int.MinValue };
        }
        else
        {
            desktop.Title = "桌面";
            definitions.Remove(desktop);
        }

        definitions.Insert(0, desktop);

        foreach (var definition in definitions.OrderBy(d => d.Order))
        {
            CreateSheet(definition);
        }

        var restored = Sheets.FirstOrDefault(s => s.Model.Id == _settings.LastSheetId) ?? Sheets.FirstOrDefault();
        SelectContent(restored);
    }

    private SheetViewModel CreateSheet(SheetDefinition definition)
    {
        var sheet = new SheetViewModel(definition, _dialogs, _icons, _store, _shell, _scanner, Save);
        Sheets.Add(sheet);
        Sidebar.Items.Add(new NavItemViewModel(
            definition.Title,
            sheet,
            OnNavSelected,
            glyph: definition.IsBuiltIn ? DesktopGlyph : DefaultSheetGlyph,
            icon: ResolveSheetIcon(definition)));
        return sheet;
    }

    /// <summary>
    /// 侧边栏图标的取值顺序：自选文件图标 → 预设徽章（矢量，不落盘）→ null（用内置字形）。
    /// </summary>
    private ImageSource? ResolveSheetIcon(SheetDefinition definition)
    {
        if (definition.IconPath is not null)
        {
            return _icons.GetImageFile(definition.IconPath);
        }

        return PresetIcons.Find(definition.PresetIcon)?.Image;
    }

    private void OnNavSelected(NavItemViewModel item) => SelectContent(item.Content);

    private void SelectContent(object? content)
    {
        SelectedContent = content;
        foreach (var nav in Sidebar.Items)
        {
            nav.IsSelected = ReferenceEquals(nav.Content, content);
        }

        SettingsNav.IsSelected = ReferenceEquals(SettingsNav.Content, content);

        if (content is SheetViewModel sheet)
        {
            if (sheet.IsDesktop)
            {
                sheet.Reload();
            }

            // 打开次数在打开当场故意不重排，切回这个 Sheet 时才生效。
            // 桌面 Sheet 的 Reload 在桌面内容没变时会提前返回，所以两种 Sheet 都要显式刷一次
            sheet.RefreshOrder();

            // 只有“上次打开的 Sheet”真的变了才写盘：启动时与重复点击同一项都不必写
            if (_settings.LastSheetId != sheet.Model.Id)
            {
                _settings.LastSheetId = sheet.Model.Id;
                SaveSettings();
            }
        }
    }

    private void AddSheet()
    {
        var dialog = new NewSheetDialogViewModel(_dialogs, _icons, _store);
        if (_dialogs.ShowNewSheetDialog(dialog) != true)
        {
            return;
        }

        var definition = new SheetDefinition
        {
            Title = dialog.Name.Trim(),
            IconPath = dialog.IconPath,
            PresetIcon = dialog.SelectedPreset?.Key,
            Kind = SheetKind.Custom,
            Order = Sheets.Count,
        };

        var sheet = CreateSheet(definition);
        SelectContent(sheet);

        // 新增 Sheet 属于数据变更，而 SelectContent 只写设置，这里补一次完整保存
        Save();
    }

    /// <summary>
    /// 编辑侧边栏项（长按后原地释放触发）：复用新建对话框，预填名称与图标。
    /// 图标真换了才删旧副本；名称与图标同步到侧边栏项（两者都是可通知属性）。
    /// </summary>
    private void EditSheet(NavItemViewModel nav)
    {
        if (nav.Content is not SheetViewModel sheet || sheet.IsDesktop)
        {
            return;
        }

        var dialog = new NewSheetDialogViewModel(_dialogs, _icons, _store, "编辑侧边栏项")
        {
            Name = sheet.Model.Title,
            IconPath = sheet.Model.IconPath,
            SelectedPreset = PresetIcons.Find(sheet.Model.PresetIcon),
        };

        if (_dialogs.ShowNewSheetDialog(dialog) != true)
        {
            return;
        }

        var previousIcon = sheet.Model.IconPath;
        var title = dialog.Name.Trim();

        sheet.Model.Title = title;
        sheet.Model.IconPath = dialog.IconPath;
        sheet.Model.PresetIcon = dialog.SelectedPreset?.Key;

        nav.Title = title;
        nav.Icon = ResolveSheetIcon(sheet.Model);

        // 换图标或清除图标后，旧副本没人再引用，删掉避免图标目录只增不减
        if (previousIcon is not null
            && !string.Equals(previousIcon, dialog.IconPath, StringComparison.OrdinalIgnoreCase))
        {
            _store.DeleteIconFile(previousIcon);
        }

        Save();
    }

    /// <summary>
    /// 交换两个侧边栏项的位置（长按后拖到另一个项上释放触发）。
    /// 两个集合都要换：Sheets 决定 sheets.json 里的先后，Sidebar.Items 决定界面上的先后。
    /// 用索引器对调而不是 Move —— Move 是“插到那个位置”，会把中间几项一起顺移，
    /// 而这里要的是两项互换。
    /// Order 一律重编 1..n —— AddSheet 用 Sheets.Count 当 Order，删过 Sheet 之后会出现重复值，
    /// 只交换两个值在重复值上等于没交换。内置“桌面”保持 int.MinValue（它永远排第一，不参与交换）。
    /// </summary>
    private void SwapSheets(NavItemViewModel source, NavItemViewModel target)
    {
        // 视图层已经过滤过一轮，这里再兜一层：两者必须是不同的、非内置的自定义 Sheet
        if (ReferenceEquals(source, target)
            || source.Content is not SheetViewModel first || first.IsDesktop
            || target.Content is not SheetViewModel second || second.IsDesktop)
        {
            return;
        }

        var sheetFrom = Sheets.IndexOf(first);
        var sheetTo = Sheets.IndexOf(second);
        if (sheetFrom < 0 || sheetTo < 0)
        {
            return;
        }

        Sheets[sheetFrom] = second;
        Sheets[sheetTo] = first;

        var navFrom = Sidebar.Items.IndexOf(source);
        var navTo = Sidebar.Items.IndexOf(target);
        if (navFrom < 0 || navTo < 0)
        {
            return;
        }

        Sidebar.Items[navFrom] = target;
        Sidebar.Items[navTo] = source;

        var order = 1;
        foreach (var sheet in Sheets)
        {
            sheet.Model.Order = sheet.IsDesktop ? int.MinValue : order++;
        }

        Save();
    }

    /// <summary>
    /// 移除侧边栏项（长按后拖到回收站释放触发）：删记录本体 + 它的全部行 + 所有自定义图标副本。
    /// 磁盘上的目标文件与文件夹一律不碰。
    /// </summary>
    private void RemoveSheet(NavItemViewModel nav)
    {
        if (nav.Content is not SheetViewModel sheet || sheet.IsDesktop)
        {
            return;
        }

        var rows = sheet.Model.Items.Count;
        var detail = rows > 0
            ? $"该 Sheet 的 {rows} 条记录与自定义图标会一并删除，磁盘上的文件不受影响。"
            : "它的自定义图标会一并删除，磁盘上的文件不受影响。";

        if (_dialogs.ShowConfirm(new ConfirmDialogViewModel("移除侧边栏项", $"确定移除「{sheet.Title}」？\n{detail}")) != true)
        {
            return;
        }

        var wasSelected = ReferenceEquals(SelectedContent, sheet);

        // 图标副本先删：Sheet 自己的 + 每一行的。
        // 记录本体随 Model 离开 Sheets，下一次 Save 就从 sheets.json 消失（含每行的置顶时间与打开次数）
        _store.DeleteIconFile(sheet.Model.IconPath);
        foreach (var item in sheet.Model.Items)
        {
            _store.DeleteIconFile(item.CustomIconPath);
        }

        Sheets.Remove(sheet);
        Sidebar.Items.Remove(nav);

        // 删掉的正是当前项时回落到第一个 Sheet（通常是内置“桌面”），并更新 LastSheetId
        if (wasSelected)
        {
            SelectContent(Sheets.FirstOrDefault());
        }

        Save();
    }
}

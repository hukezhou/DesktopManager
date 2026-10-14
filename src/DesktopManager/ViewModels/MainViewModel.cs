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

    /// <summary>内置「常用」Sheet 的名称；老数据里同名的自定义 Sheet 会被就地转正。</summary>
    private const string FrequentTitle = "常用";

    /// <summary>新建内置「常用」时用的预设徽章（矢量，不落盘）。</summary>
    private const string FrequentPresetIcon = "star";

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

        // 手改过的 settings.json 可能给出界外的条数：按默认值处理，
        // 否则一个 0 会让启动时的裁剪把「常用」清空
        if (_settings.FrequentMaxCount is < AppSettings.MinFrequentMax or > AppSettings.MaxFrequentMax)
        {
            _settings.FrequentMaxCount = AppSettings.DefaultFrequentMax;
        }

        IsTopmost = _settings.Topmost;

        Settings = new SettingsViewModel(store, shell, icons, themes, dialogs, _settings, Save, ApplyFrequentSettings);
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

    /// <summary>
    /// 装载 Sheet，并保证两个内置项存在且位置固定：「桌面」第一、「常用」第二，
    /// 自定义项按已有 Order 从 2 起顺延（内置项不参与拖拽交换，位置每次启动都重编）。
    /// </summary>
    private void LoadSheets()
    {
        var definitions = _store.LoadSheets();
        var desktop = EnsureDesktop(definitions);
        var frequent = EnsureFrequent(definitions);

        desktop.Order = int.MinValue;
        frequent.Order = 1;

        CreateSheet(desktop);
        var frequentSheet = CreateSheet(frequent);

        var order = 2;
        foreach (var definition in definitions.OrderBy(d => d.Order))
        {
            definition.Order = order++;
            CreateSheet(definition);
        }

        // 上限可能比已有记录数小（例如设置里刚改小），装载完立刻裁一次
        frequentSheet.EnforceFrequentCap();

        var restored = Sheets.FirstOrDefault(s => s.Model.Id == _settings.LastSheetId) ?? Sheets.FirstOrDefault();
        SelectContent(restored);
    }

    /// <summary>内置「桌面」：没有就补一个，名称始终锁定。</summary>
    private static SheetDefinition EnsureDesktop(List<SheetDefinition> definitions)
    {
        var desktop = definitions.FirstOrDefault(d => d.Kind == SheetKind.Desktop);
        if (desktop is null)
        {
            return new SheetDefinition { Title = "桌面", Kind = SheetKind.Desktop, Order = int.MinValue };
        }

        desktop.Title = "桌面";
        definitions.Remove(desktop);
        return desktop;
    }

    /// <summary>
    /// 内置「常用」：已经有就直接用；否则把名为「常用」的自定义 Sheet <b>就地转正</b>
    /// —— 保留它的 Id、图标和全部现有行，用户自己建的这一页不会因为升级而消失；
    /// 两者都没有才新建一个。返回的项会从 definitions 里摘走，由调用方固定到第二个位置。
    /// </summary>
    private static SheetDefinition EnsureFrequent(List<SheetDefinition> definitions)
    {
        var frequent = definitions.FirstOrDefault(d => d.Kind == SheetKind.Frequent);
        if (frequent is null)
        {
            frequent = definitions.FirstOrDefault(d =>
                d.Kind == SheetKind.Custom && string.Equals(d.Title, FrequentTitle, StringComparison.Ordinal));

            if (frequent is not null)
            {
                frequent.Kind = SheetKind.Frequent;
            }
        }
        else
        {
            frequent.Title = FrequentTitle;
        }

        if (frequent is null)
        {
            return new SheetDefinition
            {
                Title = FrequentTitle,
                Kind = SheetKind.Frequent,
                PresetIcon = FrequentPresetIcon,
                Order = 1,
            };
        }

        definitions.Remove(frequent);
        return frequent;
    }

    private SheetViewModel CreateSheet(SheetDefinition definition)
    {
        var sheet = new SheetViewModel(definition, _dialogs, _icons, _store, _shell, _scanner, Save, OnItemOpened);
        if (sheet.IsFrequent)
        {
            sheet.FrequentCapacity = _settings.FrequentMaxCount;
        }

        Sheets.Add(sheet);
        Sidebar.Items.Add(new NavItemViewModel(
            definition.Title,
            sheet,
            OnNavSelected,
            glyph: definition.IsDesktop ? DesktopGlyph : DefaultSheetGlyph,
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

    /// <summary>
    /// 某一行被成功打开：把这条记录收进内置「常用」Sheet 的最前面。
    /// 右键的「打开所在文件夹」不经过 <see cref="SheetViewModel.OpenItem"/>，所以不会到这里 ——
    /// 只是看了一眼目录，不算打开。
    /// </summary>
    private void OnItemOpened(SheetViewModel source, LinkItemViewModel item)
    {
        if (!_settings.FrequentEnabled)
        {
            return;
        }

        var frequent = Sheets.FirstOrDefault(s => s.IsFrequent);
        if (frequent is not null)
        {
            frequent.PromoteOpened(item.Model);
        }
    }

    /// <summary>
    /// 设置页改了「常用」的开关或条数上限：把上限同步给内置 Sheet 并立刻裁一次，
    /// 这样改小数字的当场就能看到末尾的记录被删掉。
    /// </summary>
    private void ApplyFrequentSettings()
    {
        foreach (var sheet in Sheets)
        {
            if (sheet.IsFrequent)
            {
                sheet.FrequentCapacity = _settings.FrequentMaxCount;
                sheet.EnforceFrequentCap();
            }
        }
    }

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
        if (nav.Content is not SheetViewModel sheet || sheet.IsProtected)
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
    /// Order 一律重编 —— AddSheet 用 Sheets.Count 当 Order，删过 Sheet 之后会出现重复值，
    /// 只交换两个值在重复值上等于没交换。两个内置项位置固定：「桌面」永远 int.MinValue（第一）、
    /// 「常用」永远 1（第二），它们都不参与交换。
    /// </summary>
    private void SwapSheets(NavItemViewModel source, NavItemViewModel target)
    {
        // 视图层已经过滤过一轮，这里再兜一层：两者必须是不同的、非内置的自定义 Sheet
        if (ReferenceEquals(source, target)
            || source.Content is not SheetViewModel first || first.IsProtected
            || target.Content is not SheetViewModel second || second.IsProtected)
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

        var order = 2;
        foreach (var sheet in Sheets)
        {
            if (sheet.IsDesktop)
            {
                sheet.Model.Order = int.MinValue;
            }
            else if (sheet.IsFrequent)
            {
                sheet.Model.Order = 1;
            }
            else
            {
                sheet.Model.Order = order++;
            }
        }

        Save();
    }

    /// <summary>
    /// 移除侧边栏项（长按后拖到回收站释放触发）：删记录本体 + 它的全部行 + 所有自定义图标副本。
    /// 磁盘上的目标文件与文件夹一律不碰。
    /// </summary>
    private void RemoveSheet(NavItemViewModel nav)
    {
        if (nav.Content is not SheetViewModel sheet || sheet.IsProtected)
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

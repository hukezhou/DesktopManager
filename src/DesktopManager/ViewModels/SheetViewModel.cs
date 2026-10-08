using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopManager.Models;
using DesktopManager.Services;

namespace DesktopManager.ViewModels;

/// <summary>
/// 一个 Sheet 的用户栏：搜索、排序（置顶在前 → 置顶时间降序 → 打开次数降序 → SortOrder）、
/// 添加/修改/置顶/移除/打开。
/// </summary>
public sealed partial class SheetViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IIconCache _icons;
    private readonly IStore _store;
    private readonly IShellService _shell;
    private readonly IDesktopScanner _scanner;
    private readonly Action _requestSave;

    /// <summary>桌面条目的“覆盖项”路径集合，避免每次 AttachOverride 都线性查找。</summary>
    private readonly HashSet<string> _overridePaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>桌面条目按路径缓存的模型：重复扫描时复用同一个对象，保持视图对象有效。</summary>
    private readonly Dictionary<string, LinkItem> _desktopModels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>模型到行视图对象的缓存，重建列表时复用，避免每次切换都重新分配。</summary>
    private readonly Dictionary<LinkItem, LinkItemViewModel> _itemVms = new(ReferenceEqualityComparer.Instance);

    /// <summary>批量装载中：暂停列表变更通知，装载结束后一次性通知。</summary>
    private bool _isLoading;

    /// <summary>
    /// 构造是否完成。<see cref="MainViewModel.Save"/> 写的是 <c>Sheets</c> 里的模型，
    /// 而构造期间本 Sheet 还没被加进 <c>Sheets</c>，此时写盘会把它整个漏掉，
    /// 所以构造期间禁止任何写盘。
    /// </summary>
    private bool _loaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(FilteredCount))]
    [NotifyPropertyChangedFor(nameof(EmptyHint))]
    private string _searchText = string.Empty;

    public SheetViewModel(
        SheetDefinition model,
        IDialogService dialogs,
        IIconCache icons,
        IStore store,
        IShellService shell,
        IDesktopScanner scanner,
        Action requestSave)
    {
        Model = model;
        _dialogs = dialogs;
        _icons = icons;
        _store = store;
        _shell = shell;
        _scanner = scanner;
        _requestSave = requestSave;

        View = new ListCollectionView(Items)
        {
            Filter = obj => obj is LinkItemViewModel item && MatchesSearch(item),
        };
        View.SortDescriptions.Add(new SortDescription(nameof(LinkItemViewModel.IsPinned), ListSortDirection.Descending));
        // 置顶区内部按置顶时间降序（后置顶的排最前）。非置顶项 PinnedAt 都是 null，降序下彼此相等，
        // 于是自然落到下一条比较规则 —— 置顶项因此不参与“打开次数”的排名。
        View.SortDescriptions.Add(new SortDescription(nameof(LinkItemViewModel.PinnedAt), ListSortDirection.Descending));
        // 非置顶区按打开次数降序，打开越多次越靠前；次数相同退回原来的 SortOrder
        View.SortDescriptions.Add(new SortDescription(nameof(LinkItemViewModel.OpenCount), ListSortDirection.Descending));
        View.SortDescriptions.Add(new SortDescription(nameof(LinkItemViewModel.SortOrder), ListSortDirection.Ascending));
        View.SortDescriptions.Add(new SortDescription(nameof(LinkItemViewModel.DisplayName), ListSortDirection.Ascending));

        // 列表内容变化会影响空状态提示与计数；批量装载期间跳过，装载完再统一通知
        Items.CollectionChanged += (_, _) =>
        {
            if (!_isLoading)
            {
                RaiseListDependentProperties();
            }
        };

        // 老数据里的置顶项可能没有 PinnedAt（那时还没这个字段）：补一个最小值，
        // 让它们排在有时间的置顶项之后，而不是混在一起随机排
        foreach (var item in Model.Items)
        {
            if (item.IsPinned && item.PinnedAt is null)
            {
                item.PinnedAt = DateTime.MinValue;
            }
        }

        if (IsDesktop)
        {
            Reload();
        }
        else
        {
            LoadCustomItems();
        }

        _loaded = true;
    }

    /// <summary>自定义 Sheet：把持久化的条目读入列表。</summary>
    private void LoadCustomItems()
    {
        Load(() =>
        {
            foreach (var model in Model.Items)
            {
                Items.Add(GetItem(model));
            }
        });
    }

    /// <summary>批量重建列表：装载期间抑制通知，结束后只通知一次。</summary>
    private void Load(Action populate)
    {
        _isLoading = true;
        try
        {
            Items.Clear();
            populate();
        }
        finally
        {
            _isLoading = false;
        }

        RefreshView();
        RaiseListDependentProperties();
    }

    private void RaiseListDependentProperties()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(IsTopRowPinned));
    }

    public SheetDefinition Model { get; }

    public ObservableCollection<LinkItemViewModel> Items { get; } = new();

    /// <summary>带过滤与排序的视图，ListBox 绑定它。</summary>
    public ICollectionView View { get; }

    public string Title => Model.Title;

    public bool IsDesktop => Model.Kind == SheetKind.Desktop;

    public bool CanAddItems => !IsDesktop;

    public int FilteredCount => View.Cast<object>().Count();

    /// <summary>用 Any 而不是 Count：只需要知道“有没有”，不必把过滤视图数完。</summary>
    public bool IsEmpty => !View.Cast<object>().Any();

    /// <summary>空列表或搜索无结果时的提示文字。</summary>
    public string EmptyHint => Items.Count == 0
        ? IsDesktop
            ? "桌面上没有可显示的文件"
            : "还没有内容，点击右侧 “+” 添加文件、文件夹或快捷方式"
        : $"没有匹配 “{SearchText}” 的内容";

    /// <summary>
    /// 过滤 + 排序之后第一行是否置顶。搜索栏那条色带要跟第一行同色，
    /// 这样置顶的深色带能从内容区顶部开始，而不是在搜索栏下方突然截断。
    /// </summary>
    public bool IsTopRowPinned => View.Cast<LinkItemViewModel>().FirstOrDefault()?.IsPinned ?? false;

    /// <summary>刷新过滤 / 排序视图，并同步依赖首行的属性。</summary>
    private void RefreshView()
    {
        View.Refresh();
        OnPropertyChanged(nameof(IsTopRowPinned));
    }

    /// <summary>
    /// 切回本 Sheet 时按最新数据重排一次。<see cref="OpenItem"/> 故意不当场重排，
    /// 打开次数就靠这里生效（桌面 Sheet 的 <see cref="Reload"/> 重建列表时也会重排）。
    /// </summary>
    public void RefreshOrder() => RefreshView();

    partial void OnSearchTextChanged(string value)
    {
        RefreshView();
    }

    /// <summary>
    /// 重新扫描桌面（仅桌面 Sheet）。条目模型与行视图对象按路径缓存：
    /// 桌面内容没变时（绝大多数情况）完全不动列表，避免每次切换都重建。
    /// </summary>
    public void Reload()
    {
        if (!IsDesktop)
        {
            return;
        }

        var overrides = new Dictionary<string, LinkItem>(StringComparer.OrdinalIgnoreCase);
        _overridePaths.Clear();
        foreach (var item in Model.Items)
        {
            overrides[item.TargetPath] = item;
            _overridePaths.Add(item.TargetPath);
        }

        var live = new List<LinkItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var entry in _scanner.Scan())
        {
            LinkItem model;
            if (overrides.TryGetValue(entry.Path, out var @override))
            {
                model = @override;
            }
            else if (_desktopModels.TryGetValue(entry.Path, out var cached))
            {
                model = cached;
            }
            else
            {
                model = new LinkItem
                {
                    TargetPath = entry.Path,
                    DisplayName = entry.Name,
                    SortOrder = order,
                };
            }

            if (!overrides.ContainsKey(entry.Path)
                && !string.Equals(model.DisplayName, entry.Name, StringComparison.Ordinal))
            {
                // 磁盘上改了名：丢掉旧的行视图对象，重建这一行
                model.DisplayName = entry.Name;
                _itemVms.Remove(model);
            }

            _desktopModels[entry.Path] = model;
            seen.Add(entry.Path);
            live.Add(model);

            order++;
        }

        // 先回收失效的覆盖项：桌面内容没变时下面会提前返回，那时清理就永远跑不到了
        PruneMissing(seen);

        if (IsSameAsCurrent(live))
        {
            return;
        }

        Load(() =>
        {
            foreach (var model in live)
            {
                Items.Add(GetItem(model));
            }
        });
    }

    private void PruneMissing(HashSet<string> seen)
    {
        if (_desktopModels.Count != seen.Count)
        {
            var missing = _desktopModels.Keys.Where(path => !seen.Contains(path)).ToList();
            foreach (var path in missing)
            {
                var stale = _desktopModels[path];
                _desktopModels.Remove(path);
                _itemVms.Remove(stale);
            }
        }

        // 构造期间不做这件事：内存里删掉之后不会再有写盘机会（见 _loaded），清理就永远落不了盘。
        // 跳过这一次，等 SelectContent 或用户切回桌面 Sheet 时的 Reload 再清，那时写盘是安全的
        if (!_loaded)
        {
            return;
        }

        // 桌面文件被删掉、或在资源管理器里改了名之后，它的覆盖项（置顶 / 改名 / 图标 / 打开次数）
        // 再也用不到了，以前会永久留在 sheets.json 里只增不减，这里一并回收
        var dead = Model.Items.Where(item => !seen.Contains(item.TargetPath)).ToList();
        if (dead.Count == 0)
        {
            return;
        }

        foreach (var item in dead)
        {
            Model.Items.Remove(item);
            _overridePaths.Remove(item.TargetPath);
            _desktopModels.Remove(item.TargetPath);
            _itemVms.Remove(item);
        }

        _requestSave();
    }

    private bool IsSameAsCurrent(List<LinkItem> desired)
    {
        if (desired.Count != Items.Count)
        {
            return false;
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (!ReferenceEquals(desired[i], Items[i].Model)
                || !string.Equals(desired[i].DisplayName, Items[i].DisplayName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>取（或创建）某个模型对应的行视图对象。</summary>
    private LinkItemViewModel GetItem(LinkItem model)
    {
        if (_itemVms.TryGetValue(model, out var existing))
        {
            return existing;
        }

        var item = new LinkItemViewModel(model, this, _icons);
        _itemVms[model] = item;
        return item;
    }

    public void OpenItem(LinkItemViewModel item)
    {
        if (!_shell.Open(item.TargetPath, out var error))
        {
            _dialogs.ShowError("打开失败", error ?? item.TargetPath);
            return;
        }

        // 只有真的打开成功才计数；失败（目标没了 / 没有关联程序）不算一次
        item.OpenCount++;

        // 桌面条目在 sheets.json 里本来可能没有记录，要存这个数就得先把它转正成覆盖项
        AttachOverride(item.Model);

        // 故意不调 RefreshView()：打开完当场不跳位，
        // 等下次进入该 Sheet、搜索、增删或置顶触发重排时才按新次数排
        _requestSave();
    }

    public void TogglePin(LinkItemViewModel item)
    {
        item.IsPinned = !item.IsPinned;
        item.PinnedAt = item.IsPinned ? DateTime.Now : null;
        AttachOverride(item.Model);
        RefreshView();
        _requestSave();
    }

    public void EditItem(LinkItemViewModel item)
    {
        var dialog = new LinkEditDialogViewModel(_dialogs, _icons, _store, _shell)
        {
            Mode = LinkEditMode.Edit,
            TargetPath = item.TargetPath,
            DisplayName = item.DisplayName,
            CustomIconPath = item.CustomIconPath,
        };

        if (_dialogs.ShowLinkEditDialog(dialog) != true)
        {
            return;
        }

        var previousIcon = item.CustomIconPath;
        item.TargetPath = dialog.TargetPath;
        item.DisplayName = dialog.DisplayName;
        item.CustomIconPath = dialog.CustomIconPath;

        // 换图标或清除图标后，旧的副本没人再引用，删掉避免图标目录只增不减
        if (previousIcon is not null
            && !string.Equals(previousIcon, dialog.CustomIconPath, StringComparison.OrdinalIgnoreCase))
        {
            _store.DeleteIconFile(previousIcon);
        }

        AttachOverride(item.Model);
        RefreshView();
        _requestSave();
    }

    /// <summary>
    /// 移除一条记录：只删记录本身和它独占的图标副本，<b>绝不删磁盘上的文件或文件夹</b>。
    /// 桌面 Sheet 的行来自磁盘扫描、没有可删的记录，所以右键菜单里根本不显示这一项（见 <c>CanRemove</c>）。
    /// </summary>
    public void RemoveItem(LinkItemViewModel item)
    {
        Model.Items.Remove(item.Model);
        _itemVms.Remove(item.Model);

        // 记录没了，它独占的图标副本也没人引用，删掉避免图标目录只增不减
        _store.DeleteIconFile(item.CustomIconPath);

        Items.Remove(item);
        RefreshView();
        _requestSave();
    }

    [RelayCommand(CanExecute = nameof(CanAddItems))]
    private void AddItem()
    {
        var dialog = new LinkEditDialogViewModel(_dialogs, _icons, _store, _shell)
        {
            Mode = LinkEditMode.Add,
        };

        if (_dialogs.ShowLinkEditDialog(dialog) != true)
        {
            return;
        }

        var model = new LinkItem
        {
            TargetPath = dialog.TargetPath,
            DisplayName = dialog.DisplayName,
            CustomIconPath = dialog.CustomIconPath,
            SortOrder = NextSortOrder(),
        };

        Model.Items.Add(model);
        Items.Add(GetItem(model));
        RefreshView();
        _requestSave();
    }

    /// <summary>
    /// 新记录的 SortOrder 取当前最小值减一：在打开次数相同（都是 0）的非置顶行里它排最前。
    /// </summary>
    private int NextSortOrder()
    {
        var unpinned = Items.Where(i => !i.IsPinned).Select(i => i.SortOrder).ToList();
        return unpinned.Count > 0 ? unpinned.Min() - 1 : 0;
    }

    private void AttachOverride(LinkItem model)
    {
        if (IsDesktop && _overridePaths.Add(model.TargetPath))
        {
            Model.Items.Add(model);
        }
    }

    private bool MatchesSearch(LinkItemViewModel item) =>
        string.IsNullOrWhiteSpace(SearchText)
        || item.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || item.TargetPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>让无障碍/UIA 读到 Sheet 名称。</summary>
    public override string ToString() => Title;
}

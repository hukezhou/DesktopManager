namespace DesktopManager.Models;

/// <summary>侧边栏中的一个 Sheet 定义。</summary>
public class SheetDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>侧边栏 Tooltip 与标题栏显示的名称。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>侧边栏图标文件（已复制到图标目录），为空则看 <see cref="PresetIcon"/>。</summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// 预设图标标识（见 <c>PresetIcons</c>）。不产生任何文件，渲染时按 key 现取矢量徽章；
    /// 与 IconPath 互斥（UI 保证），两者都为空则用内置字形。老数据没这个键 → null。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PresetIcon { get; set; }

    public SheetKind Kind { get; set; } = SheetKind.Custom;

    /// <summary>侧边栏中的排列顺序。</summary>
    public int Order { get; set; }

    /// <summary>
    /// 自定义 Sheet：全部条目。
    /// 桌面 Sheet：仅存放对桌面文件的覆盖项（置顶/改名/图标/打开次数）。
    /// </summary>
    public List<LinkItem> Items { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBuiltIn => Kind == SheetKind.Desktop;
}

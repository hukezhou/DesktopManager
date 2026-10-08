using System.Text.Json.Serialization;

namespace DesktopManager.Models;

/// <summary>
/// 用户栏中的一行（一条记录）：指向文件、文件夹或快捷方式。
/// 对于内置桌面 Sheet，本对象表示该桌面文件的“覆盖项”（置顶、改名、自定义图标、打开次数）。
/// </summary>
public class LinkItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>目标绝对路径（文件 / 文件夹 / .lnk）。</summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>显示名称，默认取目标本来的名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>用户自定义图标（已复制到图标目录的 .png/.ico），为空则使用目标原始图标。</summary>
    public string? CustomIconPath { get; set; }

    /// <summary>是否置顶。</summary>
    public bool IsPinned { get; set; }

    /// <summary>置顶时间，用于置顶区内部排序（后置顶的在前）。</summary>
    public DateTime? PinnedAt { get; set; }

    /// <summary>
    /// 成功打开过的次数，非置顶区按它降序排（置顶项不参与这个排名）。
    /// 标 <c>WhenWritingDefault</c>：0 不落盘，没打开过的条目不会在 sheets.json 里多一个字段。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int OpenCount { get; set; }

    /// <summary>非置顶区的排序值，越小越靠前；新记录取当前最小值减一。</summary>
    public int SortOrder { get; set; }
}

namespace DesktopManager.Models;

/// <summary>应用级设置。</summary>
public class AppSettings
{
    /// <summary>主题：Light 或 Dark。</summary>
    public string Theme { get; set; } = ThemeOptions.Light;

    /// <summary>标题栏“置顶”按钮状态。</summary>
    public bool Topmost { get; set; }

    /// <summary>上次打开的 Sheet，启动时自动选中。</summary>
    public string? LastSheetId { get; set; }

    /// <summary>
    /// 是否把「打开过」的记录自动收进内置「常用」Sheet。
    /// 只影响新增；上限（<see cref="FrequentMaxCount"/>）无论此项开关都生效。
    /// </summary>
    public bool FrequentEnabled { get; set; } = true;

    /// <summary>
    /// 「常用」最多保留多少条记录，超出的从显示顺序末尾删除。
    /// 旧配置文件缺这个键时取默认值 20。
    /// </summary>
    public int FrequentMaxCount { get; set; } = DefaultFrequentMax;

    public const int DefaultFrequentMax = 20;

    /// <summary>设置页允许输入的条数范围。</summary>
    public const int MinFrequentMax = 1;

    public const int MaxFrequentMax = 100;

    // 窗口状态：可空，旧配置文件缺字段时自动为 null，走默认尺寸。
    // 最大化时不写宽高与位置，避免把“全屏尺寸”污染成正常尺寸。

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public bool WindowMaximized { get; set; }
}

/// <summary>主题标识常量，避免魔法字符串。</summary>
public static class ThemeOptions
{
    public const string Light = "Light";
    public const string Dark = "Dark";
}

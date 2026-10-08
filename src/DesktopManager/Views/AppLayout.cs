namespace DesktopManager.Views;

/// <summary>
/// 布局常量的唯一来源：侧边栏宽度、窗口最小尺寸与默认尺寸。
/// 改侧边栏宽度时只需改 <see cref="SidebarWidth"/>。
/// </summary>
public static class AppLayout
{
    /// <summary>侧边栏宽度（DIP），锁定为一列图标。</summary>
    public const double SidebarWidth = 56;

    /// <summary>
    /// 标题栏左侧标识的边长（DIP）。它就是 <c>Assets/app.ico</c> 那张图，
    /// 图标里墨迹占边长的 72%，所以 21 DIP 得到约 15 DIP 的「狐」墨迹高度。
    /// 左边距按 <see cref="SidebarWidth"/> 居中，让它落在侧边栏图标列的正上方。
    /// </summary>
    public const double TitleBarLogoSize = 21;

    /// <summary>窗口自绘边框宽度（DIP）。<c>WindowStyle=None</c>，边框由 MainWindow 的 Border 画。</summary>
    public const double WindowBorderThickness = 4;

    /// <summary>搜索栏 + 用户栏（以及设置页）这个整体的四角倒角半径（DIP）。</summary>
    public const double ContentCornerRadius = 6;

    /// <summary>窗口外框圆角半径（DIP），指实际渲染出来的外弧半径。最大化时归零。</summary>
    public const double WindowCornerRadius = 7;

    /// <summary>
    /// 赋给 <c>RootBorder.CornerRadius</c> 的值。WPF 的 <c>Border</c> 把边框带画在圆角外侧：
    /// 实测外弧半径 = CornerRadius + WindowBorderThickness / 2（12 DIP 常量画成 14 DIP、8 画成 10、6 画成 8），
    /// 所以要减掉半个边框厚度才能得到 <see cref="WindowCornerRadius"/>。
    /// </summary>
    public static double WindowBorderCornerRadius => WindowCornerRadius - WindowBorderThickness / 2;

    /// <summary>
    /// 窗口内容层的圆角，与 <see cref="ContentCornerRadius"/> 同为 6 DIP：
    /// 内容块的右下角正好就是窗口的内右下角，两条弧重合才不会在同一个角上出现两种半径。
    /// 它比 <see cref="WindowCornerRadius"/>（7 DIP）小 1 DIP，所以转角处的边框带约 5.2 DIP（直边处仍是 4 DIP）。
    /// </summary>
    public const double WindowInnerCornerRadius = 6;

    /// <summary>窗口最小宽度 = 侧边栏宽度的倍数。</summary>
    public const int MinWidthInSidebarUnits = 5;

    public const double MinHeight = 520;

    public const double DefaultWidth = 1040;

    public const double DefaultHeight = 720;

    /// <summary>恢复位置时要求与某个显示器工作区的最小重叠，低于此值视为“在屏幕外”。</summary>
    public const double MinVisibleOverlap = 60;

    public static double MinWindowWidth => SidebarWidth * MinWidthInSidebarUnits;
}

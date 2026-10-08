using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopManager.Models;
using DesktopManager.Services;
using DesktopManager.ViewModels;
using DesktopManager.Views;

namespace DesktopManager;

/// <summary>
/// 主窗口：只负责窗口外观（自定义标题栏、最大化到工作区、尺寸记忆），不含业务逻辑。
/// </summary>
public partial class MainWindow : Window, IWindowController
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int SaveDebounceMs = 500;

    private readonly IMonitorService _monitors;

    private WindowBounds? _pendingBounds;
    private DispatcherTimer? _saveTimer;

    public MainWindow(IMonitorService monitorService)
    {
        InitializeComponent();
        _monitors = monitorService;

        ApplyLayoutConstants();

        StateChanged += OnStateChanged;
        SizeChanged += (_, _) => ScheduleBoundsSave();
        LocationChanged += (_, _) => ScheduleBoundsSave();
    }

    /// <summary>侧边栏宽度、窗口边框与尺寸上下限都来自 <see cref="AppLayout"/>。</summary>
    private void ApplyLayoutConstants()
    {
        // BorderThickness 是 Thickness，XAML 里没法直接 x:Static 一个 double 常量，在这里赋值
        RootBorder.BorderThickness = new Thickness(AppLayout.WindowBorderThickness);
        ApplyWindowCornerRadius(false);

        var sidebar = BodyGrid.ColumnDefinitions[0];
        sidebar.Width = new GridLength(AppLayout.SidebarWidth);
        sidebar.MinWidth = AppLayout.SidebarWidth;
        sidebar.MaxWidth = AppLayout.SidebarWidth;

        // 标题栏标识：按侧边栏列居中，Margin 是 Thickness，和上面一样不能在 XAML 里 x:Static 一个 double 常量
        LogoImage.Width = AppLayout.TitleBarLogoSize;
        LogoImage.Height = AppLayout.TitleBarLogoSize;
        LogoImage.Margin = new Thickness((AppLayout.SidebarWidth - AppLayout.TitleBarLogoSize) / 2, 0, 0, 0);

        Width = AppLayout.DefaultWidth;
        Height = AppLayout.DefaultHeight;
        MinWidth = AppLayout.MinWindowWidth;
        MinHeight = AppLayout.MinHeight;
    }

    /// <summary>
    /// 外框圆角由 <c>RootBorder.CornerRadius</c> 画，内层再用 <see cref="RoundClipBehavior"/> 裁掉四角，
    /// 否则标题栏 / 侧边栏的直角会顶出圆角。赋给 Border 的是 <see cref="AppLayout.WindowBorderCornerRadius"/>
    /// （比目标外弧半径小半个边框厚度，WPF 会把边框带画在圆角外侧），内层用 <see cref="AppLayout.WindowInnerCornerRadius"/>。
    /// <c>CornerRadius</c> 是结构体，和 <c>BorderThickness</c> 一样不能在 XAML 里 <c>x:Static</c> 一个 double 常量。
    /// 最大化时内外都归零，与 Windows 11 的最大化窗口一致。
    /// </summary>
    private void ApplyWindowCornerRadius(bool maximized)
    {
        RootBorder.CornerRadius = new CornerRadius(maximized ? 0 : AppLayout.WindowBorderCornerRadius);
        RoundClipBehavior.SetRadius(RootGrid, maximized ? 0 : AppLayout.WindowInnerCornerRadius);
    }

    public void Minimize() => WindowState = WindowState.Minimized;

    public void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    public void CloseWindow() => Close();

    public void SetBounds(WindowBounds? bounds) => _pendingBounds = bounds;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowProc);

        ApplyPendingBounds();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 防抖计时器可能还没触发，退出前把最终尺寸写盘。
        FlushBounds();
        base.OnClosing(e);
    }

    /// <summary>
    /// 显示前应用上次保存的尺寸与位置：先抬到下限、压到工作区，
    /// 位置不可见时回落到居中，最后才设置最大化状态。
    /// </summary>
    private void ApplyPendingBounds()
    {
        var saved = _pendingBounds;
        var dpi = GetDpiForWindow(new WindowInteropHelper(this).Handle) / 96.0;
        if (dpi <= 0)
        {
            dpi = 1;
        }

        var workAreas = _monitors.GetWorkAreas();

        var width = saved is { } s && double.IsFinite(s.Width) ? s.Width : AppLayout.DefaultWidth;
        var height = saved is { } s2 && double.IsFinite(s2.Height) ? s2.Height : AppLayout.DefaultHeight;

        width = Math.Max(width, MinWidth);
        height = Math.Max(height, MinHeight);

        var host = FindHostWorkArea(workAreas, saved, dpi) ?? (workAreas.Count > 0 ? workAreas[0] : null);
        if (host is not null)
        {
            width = Math.Min(width, host.Value.Width / dpi);
            height = Math.Min(height, host.Value.Height / dpi);
        }

        Width = width;
        Height = height;

        if (saved is { } s3 && double.IsFinite(s3.Left) && double.IsFinite(s3.Top)
            && IsOnScreen(workAreas, s3.Left, s3.Top, width, height, dpi))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s3.Left;
            Top = s3.Top;
        }
        else
        {
            // 没有记录，或保存的位置在已断开的显示器上：保留尺寸，在所在工作区居中。
            // 这里自己算中点：WPF 的 CenterScreen 在 OnSourceInitialized 之前就用旧尺寸算好了。
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (host is not null)
            {
                Left = host.Value.Left / dpi + (host.Value.Width / dpi - width) / 2;
                Top = host.Value.Top / dpi + (host.Value.Height / dpi - height) / 2;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        if (saved?.Maximized == true)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static PixelRect? FindHostWorkArea(IReadOnlyList<PixelRect> areas, WindowBounds? saved, double dpi)
    {
        if (saved is not { } s || !double.IsFinite(s.Left) || !double.IsFinite(s.Top))
        {
            return null;
        }

        var x = (int)(s.Left * dpi);
        var y = (int)(s.Top * dpi);
        foreach (var area in areas)
        {
            if (x >= area.Left && x < area.Right && y >= area.Top && y < area.Bottom)
            {
                return area;
            }
        }

        return null;
    }

    private static bool IsOnScreen(
        IReadOnlyList<PixelRect> areas,
        double left,
        double top,
        double width,
        double height,
        double dpi)
    {
        var min = (int)(AppLayout.MinVisibleOverlap * dpi);
        var rect = new PixelRect(
            (int)(left * dpi),
            (int)(top * dpi),
            (int)((left + width) * dpi),
            (int)((top + height) * dpi));

        foreach (var area in areas)
        {
            var overlapX = Math.Min(rect.Right, area.Right) - Math.Max(rect.Left, area.Left);
            var overlapY = Math.Min(rect.Bottom, area.Bottom) - Math.Max(rect.Top, area.Top);
            if (overlapX >= min && overlapY >= min)
            {
                return true;
            }
        }

        return false;
    }

    private void ScheduleBoundsSave()
    {
        _saveTimer ??= CreateSaveTimer();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private DispatcherTimer CreateSaveTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            FlushBounds();
        };
        return timer;
    }

    private void FlushBounds()
    {
        _saveTimer?.Stop();

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // 最大化/最小化时窗口边界是“全屏尺寸”，只更新状态标志，不覆盖正常尺寸。
        if (WindowState == WindowState.Normal)
        {
            viewModel.UpdateWindowBounds(new WindowBounds(Left, Top, ActualWidth, ActualHeight, false));
        }
        else
        {
            viewModel.UpdateWindowBounds(new WindowBounds(
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN,
                WindowState == WindowState.Maximized));
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = maximized ? "还原" : "最大化";
        ApplyWindowCornerRadius(maximized);
        ScheduleBoundsSave();
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            LimitMaximizeToWorkArea(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    /// <summary>WindowStyle=None 时最大化会盖住任务栏，这里限制到工作区。</summary>
    private static void LimitMaximizeToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var work = monitorInfo.WorkArea;
        var screen = monitorInfo.Rect;

        info.MaxPosition = new PointStruct(work.Left - screen.Left, work.Top - screen.Top);
        info.MaxSize = new PointStruct(work.Right - work.Left, work.Bottom - work.Top);
        Marshal.StructureToPtr(info, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;

        public PointStruct(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointStruct Reserved;
        public PointStruct MaxSize;
        public PointStruct MaxPosition;
        public PointStruct MinTrackSize;
        public PointStruct MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct Rect;
        public RectStruct WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}

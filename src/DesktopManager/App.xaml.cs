using System.Windows;
using System.Windows.Threading;
using DesktopManager.Services;
using DesktopManager.ViewModels;

namespace DesktopManager;

/// <summary>
/// 应用入口与组合根：在这里创建服务并注入 ViewModel，其余各层只依赖接口。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 静态持有：Mutex 对象一旦被 GC 回收就会释放内核句柄，锁会提前失效。
    /// </summary>
    private static SingleInstanceGuard? _guard;

    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例：抢不到锁就把已有实例唤到前台，自己退出。
        // 这一步在建窗口与建 ViewModel 之前，所以不会有窗口闪现，也不会碰配置文件。
        _guard = new SingleInstanceGuard();
        if (!_guard.TryAcquire())
        {
            InstanceActivator.ActivateOtherInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var store = new JsonStore();
        var shell = new ShellService();
        var icons = new IconCache(shell);
        var themes = new ThemeService();
        var dialogs = new DialogService();
        var scanner = new DesktopScanner();
        var monitors = new MonitorService();

        themes.Apply(store.LoadSettings().Theme);

        _mainViewModel = new MainViewModel(store, dialogs, icons, shell, themes, scanner);

        var window = new MainWindow(monitors) { DataContext = _mainViewModel };
        _mainViewModel.AttachWindow(window);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Save();

        // 锁留到最后一刻再放：提前释放会让新实例在旧实例还在写配置时就启动，两个进程互相覆盖
        _guard?.Dispose();
        _guard = null;

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "发生未处理的异常：\n" + e.Exception.Message,
            "桌面管理器",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}

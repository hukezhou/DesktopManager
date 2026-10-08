using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopManager.Services;

/// <summary>把已经在运行的实例唤到前台。</summary>
public static class InstanceActivator
{
    private const int SwRestore = 9;

    /// <summary>
    /// 找到另一个进程的主窗口，最小化时先还原，再切到前台。找不到就什么都不做。
    /// </summary>
    public static void ActivateOtherInstance()
    {
        var self = Environment.ProcessId;

        foreach (var process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
        {
            try
            {
                if (process.Id == self)
                {
                    continue;
                }

                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    // 对方还在启动或正在退出
                    continue;
                }

                if (IsIconic(handle))
                {
                    ShowWindow(handle, SwRestore);
                }

                SetForegroundWindow(handle);
                return;
            }
            finally
            {
                // GetProcessesByName 返回的对象各自持有内核句柄
                process.Dispose();
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}

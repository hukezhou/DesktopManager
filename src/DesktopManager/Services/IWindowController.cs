using DesktopManager.Models;

namespace DesktopManager.Services;

/// <summary>
/// 主窗口的窗口操作抽象。ViewModel 通过它控制窗口，自身不引用 WPF 窗口类型。
/// </summary>
public interface IWindowController
{
    void Minimize();

    void ToggleMaximize();

    void CloseWindow();

    /// <summary>把上次保存的窗口边界交给窗口，由窗口在显示前应用（含显示器校验）。</summary>
    void SetBounds(WindowBounds? bounds);
}

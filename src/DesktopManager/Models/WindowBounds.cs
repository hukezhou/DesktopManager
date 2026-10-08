namespace DesktopManager.Models;

/// <summary>
/// 窗口边界（DIP）。Left/Top/Width/Height 允许为 <see cref="double.NaN"/>，
/// 表示“本次不更新这一项”。
/// </summary>
public readonly record struct WindowBounds(double Left, double Top, double Width, double Height, bool Maximized);

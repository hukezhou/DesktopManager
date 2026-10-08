namespace DesktopManager.Models;

/// <summary>
/// Sheet 的类型。内置桌面 Sheet 由扫描得到，自定义 Sheet 由用户维护。
/// </summary>
public enum SheetKind
{
    /// <summary>内置：显示 Windows 桌面上的文件，只读扫描。</summary>
    Desktop,

    /// <summary>用户新建的 Sheet，可添加/移除条目。</summary>
    Custom,
}

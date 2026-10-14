namespace DesktopManager.Models;

/// <summary>
/// Sheet 的类型。内置桌面 Sheet 由扫描得到，内置常用 Sheet 由「打开过」这个动作维护，
/// 自定义 Sheet 完全由用户维护。
/// </summary>
public enum SheetKind
{
    /// <summary>内置：显示 Windows 桌面上的文件，只读扫描。</summary>
    Desktop,

    /// <summary>用户新建的 Sheet，可添加/移除条目。</summary>
    Custom,

    /// <summary>
    /// 内置：收集打开过的记录，最近的排最前、超出上限的从末尾删除。
    /// 追加在末尾即可 —— <c>Kind</c> 由 <c>JsonStringEnumConverter</c> 按名字落盘，
    /// 老数据里的 "Custom" 不受新增值影响。
    /// </summary>
    Frequent,
}

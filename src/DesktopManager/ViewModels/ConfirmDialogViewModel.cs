using CommunityToolkit.Mvvm.Input;

namespace DesktopManager.ViewModels;

/// <summary>
/// 通用确认对话框：标题 + 一段正文 + 取消 / 确定。
/// 用于破坏性操作（移除侧边栏项），风格与新建 / 编辑条目对话框一致。
/// </summary>
public sealed partial class ConfirmDialogViewModel : DialogViewModelBase
{
    public ConfirmDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    /// <summary>正文，可以是多行。</summary>
    public string Message { get; }

    [RelayCommand]
    private void Accept() => RequestClose(true);

    [RelayCommand]
    private void Cancel() => RequestClose(false);
}

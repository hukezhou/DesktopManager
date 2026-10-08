using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopManager.ViewModels;

/// <summary>
/// 对话框 ViewModel 基类：用 CloseRequested 事件通知 View 关闭窗口，
/// 使对话框 ViewModel 不依赖任何窗口类型。
/// </summary>
public abstract partial class DialogViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool? _dialogResult;

    public event EventHandler? CloseRequested;

    public string Title { get; protected set; } = string.Empty;

    protected void RequestClose(bool? result)
    {
        DialogResult = result;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>对话框模式。</summary>
public enum LinkEditMode
{
    Add,
    Edit,
}

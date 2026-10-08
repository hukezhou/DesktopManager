using System.Windows;
using DesktopManager.ViewModels;

namespace DesktopManager.Views;

/// <summary>把对话框 ViewModel 的 CloseRequested 事件接到窗口的 Close。</summary>
public static class DialogCloser
{
    public static void Attach(Window window) =>
        window.DataContextChanged += (_, _) =>
        {
            if (window.DataContext is DialogViewModelBase viewModel)
            {
                viewModel.CloseRequested += (_, _) => window.Close();
            }
        };
}

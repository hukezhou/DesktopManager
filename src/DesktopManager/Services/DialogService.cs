using System.Windows;
using Microsoft.Win32;
using DesktopManager.ViewModels;
using DesktopManager.Views;

namespace DesktopManager.Services;

/// <summary>文件选择与对话框服务。ViewModel 只依赖接口，不直接接触 WPF 对话框。</summary>
public interface IDialogService
{
    /// <summary>选择 .ico/.png 图标文件。</summary>
    string? PickIconFile();

    /// <summary>选择任意文件（含 .lnk 快捷方式）。</summary>
    string? PickFile();

    /// <summary>选择文件夹。</summary>
    string? PickFolder();

    bool Confirm(string title, string message);

    void ShowError(string title, string message);

    bool? ShowNewSheetDialog(NewSheetDialogViewModel viewModel);

    bool? ShowLinkEditDialog(LinkEditDialogViewModel viewModel);

    /// <summary>自定义风格的确认框（危险操作用），返回 null 表示已有对话框在开着。</summary>
    bool? ShowConfirm(ConfirmDialogViewModel viewModel);
}

public sealed class DialogService : IDialogService
{
    /// <summary>
    /// 本应用自己的模态窗口（新建 Sheet / 编辑条目）。
    /// ShowDialog 跑嵌套消息循环，队列里排队的命令仍会执行，
    /// 连点同一个按钮可以叠出多个窗口（每个都带自己的 GDI/USER 对象），所以加门闩。
    /// </summary>
    private Window? _openDialog;

    /// <summary>
    /// 选择框与消息框的门闩。与上面刻意分开：
    /// “编辑对话框里点选择图标”是正常流程，不能被编辑对话框的门闩挡掉。
    /// </summary>
    private bool _pickerBusy;

    public string? PickIconFile() => Picker<string?>(() =>
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图标（.ico 或 .png）",
            Filter = "图标文件|*.ico;*.png|位图|*.bmp;*.jpg;*.jpeg|所有文件|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }, null);

    public string? PickFile() => Picker<string?>(() =>
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择文件、程序或快捷方式",
            Filter = "所有文件|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }, null);

    public string? PickFolder() => Picker<string?>(() =>
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件夹",
            Multiselect = false,
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }, null);

    public bool Confirm(string title, string message) => Picker(() =>
        MessageBox.Show(Owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes,
        false);

    public void ShowError(string title, string message) => Picker<bool>(() =>
    {
        MessageBox.Show(Owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }, false);

    public bool? ShowNewSheetDialog(NewSheetDialogViewModel viewModel) =>
        ShowTopDialog(new NewSheetDialog { DataContext = viewModel });

    public bool? ShowLinkEditDialog(LinkEditDialogViewModel viewModel) =>
        ShowTopDialog(new LinkEditDialog { DataContext = viewModel });

    public bool? ShowConfirm(ConfirmDialogViewModel viewModel) =>
        ShowTopDialog(new ConfirmDialog { DataContext = viewModel });

    /// <summary>已有顶层对话框时直接按“取消”返回，避免叠窗。</summary>
    private bool? ShowTopDialog(Window window)
    {
        if (_openDialog is not null)
        {
            return null;
        }

        _openDialog = window;
        try
        {
            window.Owner = Owner;
            window.ShowDialog();
            return (window.DataContext as DialogViewModelBase)?.DialogResult;
        }
        finally
        {
            _openDialog = null;
        }
    }

    /// <summary>同一个选择框重复弹出时直接按“取消”返回。</summary>
    private T Picker<T>(Func<T> show, T cancelled)
    {
        if (_pickerBusy)
        {
            return cancelled;
        }

        _pickerBusy = true;
        try
        {
            return show();
        }
        finally
        {
            _pickerBusy = false;
        }
    }

    /// <summary>当前活动窗口：对话框打开时就是那个对话框，选择框挂上去才会显示在最前面。</summary>
    private static Window? Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}

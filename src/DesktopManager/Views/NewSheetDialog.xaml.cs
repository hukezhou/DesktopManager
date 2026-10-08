using System.Windows;
using DesktopManager.ViewModels;

namespace DesktopManager.Views;

public partial class NewSheetDialog : Window
{
    public NewSheetDialog()
    {
        InitializeComponent();
        DialogCloser.Attach(this);
    }
}

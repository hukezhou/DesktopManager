using System.Windows;

namespace DesktopManager.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        DialogCloser.Attach(this);
    }
}

using System.Windows;
using DesktopManager.ViewModels;

namespace DesktopManager.Views;

public partial class LinkEditDialog : Window
{
    public LinkEditDialog()
    {
        InitializeComponent();
        DialogCloser.Attach(this);
    }
}

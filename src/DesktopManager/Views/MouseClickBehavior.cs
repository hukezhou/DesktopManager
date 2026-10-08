using System.Windows;
using System.Windows.Input;

namespace DesktopManager.Views;

/// <summary>
/// 附加属性：把控件的左键抬起事件绑定到命令。
/// 用于“点击用户栏的一行即打开对应文件夹/应用”。
/// </summary>
public static class MouseClickBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command",
        typeof(ICommand),
        typeof(MouseClickBehavior),
        new PropertyMetadata(OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
        "CommandParameter",
        typeof(object),
        typeof(MouseClickBehavior),
        new PropertyMetadata());

    public static void SetCommand(DependencyObject element, ICommand value) => element.SetValue(CommandProperty, value);

    public static ICommand GetCommand(DependencyObject element) => (ICommand)element.GetValue(CommandProperty);

    public static void SetCommandParameter(DependencyObject element, object value) => element.SetValue(CommandParameterProperty, value);

    public static object GetCommandParameter(DependencyObject element) => element.GetValue(CommandParameterProperty);

    private static void OnCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not UIElement uiElement)
        {
            return;
        }

        if (args.OldValue is not null)
        {
            uiElement.PreviewMouseLeftButtonUp -= OnMouseLeftButtonUp;
        }

        if (args.NewValue is not null)
        {
            uiElement.PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        }
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var command = GetCommand(element);
        if (command is null)
        {
            return;
        }

        var parameter = GetCommandParameter(element) ?? element.DataContext;
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
            args.Handled = true;
        }
    }
}

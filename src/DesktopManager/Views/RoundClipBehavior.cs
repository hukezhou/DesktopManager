using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopManager.Views;

/// <summary>
/// 附加属性：把元素裁成四角倒角的矩形。
/// WPF 的 <c>Border.CornerRadius</c> 只画自己的圆角底色，不会裁剪子元素，
/// 行底色、分隔线、滚动条仍然是直角，所以这里按实际尺寸写 <c>UIElement.Clip</c>。
/// </summary>
public static class RoundClipBehavior
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius",
        typeof(double),
        typeof(RoundClipBehavior),
        new PropertyMetadata(0.0, OnRadiusChanged));

    public static void SetRadius(DependencyObject element, double value) => element.SetValue(RadiusProperty, value);

    public static double GetRadius(DependencyObject element) => (double)element.GetValue(RadiusProperty);

    private static void OnRadiusChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not FrameworkElement target)
        {
            return;
        }

        // 尺寸变化（窗口拉伸 / 最大化）时重算，否则裁剪会停在旧尺寸
        target.SizeChanged += (_, _) => Apply(target);
        Apply(target);
    }

    private static void Apply(FrameworkElement target)
    {
        var radius = GetRadius(target);
        var width = target.ActualWidth;
        var height = target.ActualHeight;

        if (radius <= 0 || width <= 0 || height <= 0)
        {
            target.ClearValue(UIElement.ClipProperty);
            return;
        }

        // 极端窄高时不让圆角超过半边
        radius = Math.Min(radius, Math.Min(width, height) / 2);
        target.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
    }
}

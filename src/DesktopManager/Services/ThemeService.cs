using System.Windows;
using DesktopManager.Models;

namespace DesktopManager.Services;

/// <summary>切换全局主题资源字典。</summary>
public interface IThemeService
{
    void Apply(string theme);
}

/// <summary>
/// 主题字典通过其中的 Theme.Name 键识别，替换该字典即可完成全局换色
/// （所有颜色都用 DynamicResource 引用）。
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string ThemeNameKey = "Theme.Name";

    public void Apply(string theme)
    {
        var name = theme == ThemeOptions.Dark ? ThemeOptions.Dark : ThemeOptions.Light;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            if (dictionaries[i].Contains(ThemeNameKey))
            {
                dictionaries[i] = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/DesktopManager;component/Themes/{name}.xaml", UriKind.Absolute),
                };
                return;
            }
        }
    }
}

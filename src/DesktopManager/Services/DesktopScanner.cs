using System.IO;

namespace DesktopManager.Services;

/// <summary>桌面文件条目。</summary>
/// <param name="Path">文件绝对路径。</param>
/// <param name="Name">显示名称（已按资源管理器习惯去掉扩展名）。</param>
public readonly record struct DesktopEntry(string Path, string Name);

/// <summary>扫描 Windows 桌面目录。</summary>
public interface IDesktopScanner
{
    /// <summary>当前用户桌面 + 公共桌面，合并后按名称排序。</summary>
    IReadOnlyList<DesktopEntry> Scan();
}

public sealed class DesktopScanner : IDesktopScanner
{
    private static readonly string[] HiddenFileNames = { "desktop.ini" };

    public IReadOnlyList<DesktopEntry> Scan()
    {
        var entries = new List<DesktopEntry>();
        foreach (var directory in DesktopDirectories())
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    var entry = ToEntry(path);
                    if (entry is not null)
                    {
                        entries.Add(entry.Value);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 单个目录不可读时跳过，不影响其它目录。
            }
        }

        return entries
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> DesktopDirectories()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    private static DesktopEntry? ToEntry(string path)
    {
        var fileName = Path.GetFileName(path);
        if (HiddenFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
            {
                return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return new DesktopEntry(path, DisplayFriendlyName(fileName));
    }

    /// <summary>快捷方式与网址文件按资源管理器习惯隐藏扩展名。</summary>
    private static string DisplayFriendlyName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(fileName);
        }

        return fileName;
    }
}

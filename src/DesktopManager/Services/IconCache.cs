using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopManager.Services;

/// <summary>图标缓存，避免反复调用 Shell 提取图标。</summary>
public interface IIconCache
{
    /// <summary>取目标（文件/文件夹/快捷方式）的原始图标，可能为 null。</summary>
    ImageSource? GetShellIcon(string path);

    /// <summary>读取用户自选的 .png/.ico 图标文件，可能为 null。</summary>
    ImageSource? GetImageFile(string path);

    void Clear();
}

/// <summary>
/// 带上限的 LRU 缓存：长期运行不会无上限增长。
/// 用户自选图片按显示尺寸解码，避免整张大图常驻内存。
/// </summary>
public sealed class IconCache : IIconCache
{
    /// <summary>缓存条目上限，超出后淘汰最久未使用的。</summary>
    private const int MaxEntries = 2048;

    /// <summary>位图解码的宽度上限（DIP 无关，按物理像素）。40 DIP 图标在 400% 缩放下也够用。</summary>
    private const int DecodePixelWidth = 160;

    private readonly IShellService _shell;
    private readonly object _gate = new();

    /// <summary>最近使用在前的链表 + 键索引，构成 LRU。</summary>
    private readonly LinkedList<CacheEntry> _mostRecentFirst = new();

    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _index = new(StringComparer.OrdinalIgnoreCase);

    public IconCache(IShellService shell) => _shell = shell;

    public ImageSource? GetShellIcon(string path) =>
        Get("shell:" + path, path, () => _shell.GetIcon(path));

    public ImageSource? GetImageFile(string path) =>
        Get("file:" + path, path, () => DecodeImageFile(path));

    public void Clear()
    {
        lock (_gate)
        {
            _index.Clear();
            _mostRecentFirst.Clear();
        }
    }

    private ImageSource? Get(string key, string path, Func<ImageSource?> load)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        lock (_gate)
        {
            var stamp = TimestampOf(path);
            if (_index.TryGetValue(key, out var existing))
            {
                if (existing.Value.Stamp == stamp)
                {
                    Touch(existing);
                    return existing.Value.Image;
                }

                _index.Remove(key);
                _mostRecentFirst.Remove(existing);
            }

            var image = load();
            Add(new CacheEntry(key, stamp, image));
            return image;
        }
    }

    private void Add(CacheEntry entry)
    {
        _index[entry.Key] = _mostRecentFirst.AddFirst(entry);

        while (_index.Count > MaxEntries)
        {
            var oldest = _mostRecentFirst.Last;
            if (oldest is null)
            {
                break;
            }

            _mostRecentFirst.RemoveLast();
            _index.Remove(oldest.Value.Key);
        }
    }

    private void Touch(LinkedListNode<CacheEntry> node)
    {
        if (!ReferenceEquals(node, _mostRecentFirst.First))
        {
            _mostRecentFirst.Remove(node);
            _mostRecentFirst.AddFirst(node);
        }
    }

    private static BitmapImage? DecodeImageFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;

            // .ico 本身就只有 16/32/48 等几档，强制放大反而变糊；位图才需要限制解码尺寸。
            if (!path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                bitmap.DecodePixelWidth = DecodePixelWidth;
            }

            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UriFormatException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private static long TimestampOf(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.GetLastWriteTimeUtc(path).Ticks;
            }

            if (Directory.Exists(path))
            {
                return Directory.GetLastWriteTimeUtc(path).Ticks;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 无法读取时间戳时视为已变化，重新提取。
            return -1;
        }

        return 0;
    }

    private readonly record struct CacheEntry(string Key, long Stamp, ImageSource? Image);
}

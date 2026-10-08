using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopManager.Models;

namespace DesktopManager.Services;

/// <summary>设置与 Sheet 数据的持久化。</summary>
public interface IStore
{
    string DataDirectory { get; }

    string IconsDirectory { get; }

    AppSettings LoadSettings();

    void SaveSettings(AppSettings settings);

    List<SheetDefinition> LoadSheets();

    void SaveSheets(IEnumerable<SheetDefinition> sheets);

    /// <summary>把外部图标文件复制进图标目录，返回新路径；失败返回 null。</summary>
    string? ImportIconFile(string sourcePath);

    /// <summary>删除由 <see cref="ImportIconFile"/> 复制进来的副本；只允许图标目录内的文件。</summary>
    void DeleteIconFile(string? path);
}

/// <summary>使用 System.Text.Json 把数据写到 %APPDATA%\DesktopManager。</summary>
public sealed class JsonStore : IStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _settingsPath;
    private readonly string _sheetsPath;

    public string DataDirectory { get; }

    public string IconsDirectory { get; }

    public JsonStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopManager"))
    {
    }

    public JsonStore(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        IconsDirectory = Path.Combine(dataDirectory, "icons");
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        _sheetsPath = Path.Combine(dataDirectory, "sheets.json");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(IconsDirectory);
    }

    public AppSettings LoadSettings() => ReadJson<AppSettings>(_settingsPath) ?? new AppSettings();

    public void SaveSettings(AppSettings settings) => WriteJson(_settingsPath, settings);

    public List<SheetDefinition> LoadSheets() => ReadJson<List<SheetDefinition>>(_sheetsPath) ?? new List<SheetDefinition>();

    public void SaveSheets(IEnumerable<SheetDefinition> sheets) => WriteJson(_sheetsPath, sheets.ToList());

    public string? ImportIconFile(string sourcePath)
    {
        try
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var target = Path.Combine(IconsDirectory, Guid.NewGuid().ToString("N") + extension);
            File.Copy(sourcePath, target, overwrite: true);
            return target;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void DeleteIconFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(IconsDirectory) + Path.DirectorySeparatorChar;

            // 只删本应用复制进来的副本，绝不碰用户自己的文件。
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            {
                return;
            }

            File.Delete(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 删不掉只是留下一个孤儿副本，不影响功能。
        }
    }

    private static T? ReadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        var tempPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, SerializerOptions));
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
                // 清理失败可以忽略，不影响主流程。
            }
        }
    }
}

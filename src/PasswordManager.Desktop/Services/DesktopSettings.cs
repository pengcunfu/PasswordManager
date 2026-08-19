using System.IO;
using System.Text.Json;

namespace PasswordManager.Desktop.Services;

public class DesktopSettings
{
    public string ServerUrl { get; set; } = string.Empty;

    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FNSoftware", "PasswordManager");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string FilePath => Path.Combine(DataDirectory, "desktop.json");

    public static DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new DesktopSettings();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<DesktopSettings>(json) ?? new DesktopSettings();
        }
        catch
        {
            return new DesktopSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}

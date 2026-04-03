using System.Text.Json;
using InventoryManager.Models;

namespace InventoryManager.Services;

public class ConfigService
{
    private readonly string _path;
    private AppConfig? _cache;

    public ConfigService()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "appconfig.json");
    }

    public AppConfig Load()
    {
        if (_cache != null) return _cache;
        if (!File.Exists(_path))
        {
            _cache = new AppConfig();
            Save(_cache);
            return _cache;
        }

        var txt = File.ReadAllText(_path);
        try
        {
            _cache = JsonSerializer.Deserialize<AppConfig>(txt) ?? new AppConfig();
        }
        catch
        {
            _cache = new AppConfig();
        }
        return _cache;
    }

    public void Save(AppConfig cfg)
    {
        _cache = cfg;
        var txt = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, txt);
    }
}

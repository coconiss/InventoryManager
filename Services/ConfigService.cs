using System.IO;
using System.Text.Json;
using System;
using InventoryManager.Models;

namespace InventoryManager.Services;

/// <summary>
/// 애플리케이션의 사용자 설정(바코드 세팅, 백업 경로 등)을 로드하고 저장하는 서비스입니다.
/// </summary>
public class ConfigService
{
    private readonly string _path;
    private AppConfig? _cache;

    /// <summary>
    /// ConfigService 생성자. AppData 폴더에 설정 파일(appconfig.json) 경로를 지정합니다.
    /// </summary>
    public ConfigService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appDataPath, "InventoryManager", "Data");

        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "appconfig.json");
    }

    /// <summary>
    /// 설정 파일에서 설정을 불러옵니다. 파일이 없거나 오류가 발생하면 기본값을 반환합니다.
    /// </summary>
    /// <returns>앱 설정 데이터가 담긴 AppConfig 객체</returns>
    public AppConfig Load()
    {
        if (_cache != null) return _cache;

        if (!File.Exists(_path))
        {
            _cache = new AppConfig();
            Save(_cache);
            return _cache;
        }

        try
        {
            var txt = File.ReadAllText(_path);
            _cache = JsonSerializer.Deserialize<AppConfig>(txt) ?? new AppConfig();
        }
        catch
        {
            // 파일 손상 등의 이유로 로드 실패 시 무결성을 위해 기본값으로 덮어씀
            _cache = new AppConfig();
        }
        return _cache;
    }

    /// <summary>
    /// 현재의 설정 객체를 JSON 파일 형태로 저장하고 메모리 캐시를 갱신합니다.
    /// </summary>
    /// <param name="cfg">저장할 설정 객체</param>
    public void Save(AppConfig cfg)
    {
        _cache = cfg;
        var txt = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, txt);
    }
}
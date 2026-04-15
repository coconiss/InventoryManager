using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace InventoryManager.Services;

public record VersionInfo(
    string Version,
    string MinVersion,
    bool Mandatory,
    string ReleaseNotes,
    string DownloadUrl,
    string Sha256,
    long FileSize);

public enum UpdateStatus { UpToDate, Optional, Mandatory }

public record UpdateCheckResult(
    UpdateStatus Status,
    VersionInfo? Info,
    Version? CurrentVersion,
    Version? LatestVersion);

public class UpdateChecker
{
    // version.json URL — no-cache 헤더로 항상 최신값 수신
    private const string VersionJsonUrl =
        "https://cdn.yourapp.com/releases/version.json";

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>현재 어셈블리 버전을 읽는다. AssemblyVersion = "1.1.0.0" → "1.1.0"</summary>
    public static Version GetCurrentVersion()
    {
        var ver = Assembly.GetExecutingAssembly()
                          .GetName().Version ?? new Version(1, 0, 0);
        return new Version(ver.Major, ver.Minor, ver.Build);
    }

    public async Task<UpdateCheckResult> CheckAsync()
    {
        var current = GetCurrentVersion();

        VersionInfo info;
        try
        {
            // no-cache: CDN이 캐시하지 않도록 강제
            using var req = new HttpRequestMessage(HttpMethod.Get, VersionJsonUrl);
            req.Headers.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            info = ParseVersionJson(json);
        }
        catch (Exception ex)
        {
            // 네트워크 오류는 조용히 무시 (업데이트 실패로 앱 실행이 막히면 안 됨)
            System.Diagnostics.Debug.WriteLine($"[UpdateChecker] {ex.Message}");
            return new UpdateCheckResult(UpdateStatus.UpToDate, null, current, null);
        }

        var latest = Version.Parse(info.Version);
        var minVer = Version.Parse(info.MinVersion);

        // 현재 버전이 최소 버전 미만 → 강제 업데이트
        if (current < minVer || info.Mandatory)
            return new UpdateCheckResult(UpdateStatus.Mandatory, info, current, latest);

        // 최신 버전이 현재보다 높음 → 선택 업데이트
        if (latest > current)
            return new UpdateCheckResult(UpdateStatus.Optional, info, current, latest);

        return new UpdateCheckResult(UpdateStatus.UpToDate, null, current, latest);
    }

    private static VersionInfo ParseVersionJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new VersionInfo(
            r.GetProperty("version").GetString()!,
            r.GetProperty("minVersion").GetString()!,
            r.GetProperty("mandatory").GetBoolean(),
            r.GetProperty("releaseNotes").GetString()!,
            r.GetProperty("downloadUrl").GetString()!,
            r.GetProperty("sha256").GetString()!,
            r.GetProperty("fileSize").GetInt64());
    }
}
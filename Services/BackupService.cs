using InventoryManager.Data;

namespace InventoryManager.Services;

/// <summary>
/// 로컬 DB 백업 서비스 (수동/자동)
/// </summary>
public class BackupService
{
    private readonly DatabaseService _db;
    private System.Timers.Timer? _autoTimer;

    public BackupService(DatabaseService db) => _db = db;

    /// <summary>
    /// 수동 백업 - 대상 경로로 DB 파일 복사
    /// </summary>
    public async Task<string> BackupAsync(string? targetDir = null)
    {
        var src = _db.GetDbPath();
        var dir = targetDir ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Backup");

        Directory.CreateDirectory(dir);
        var fileName = $"inventory_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var dest = Path.Combine(dir, fileName);

        await Task.Run(() => File.Copy(src, dest, overwrite: true));
        return dest;
    }

    /// <summary>
    /// 자동 백업 시작 (intervalHours 주기)
    /// </summary>
    public void StartAutoBackup(int intervalHours = 24, string? targetDir = null)
    {
        _autoTimer?.Dispose();
        _autoTimer = new System.Timers.Timer(TimeSpan.FromHours(intervalHours).TotalMilliseconds);
        _autoTimer.Elapsed += async (_, _) => await BackupAsync(targetDir);
        _autoTimer.AutoReset = true;
        _autoTimer.Start();
    }

    public void StopAutoBackup() => _autoTimer?.Stop();
}

/// <summary>
/// 시스템 로그 서비스 - 파일 기반 (DB와 분리)
/// </summary>
public class LogService
{
    private readonly string _logDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LogService()
    {
        _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(_logDir);
    }

    public async Task InfoAsync(string category, string message)
        => await WriteAsync("INFO", category, message);

    public async Task WarnAsync(string category, string message)
        => await WriteAsync("WARN", category, message);

    public async Task ErrorAsync(string category, string message, Exception? ex = null)
        => await WriteAsync("ERROR", category,
            ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");

    private async Task WriteAsync(string level, string category, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] [{category}] {message}";
        var logFile = Path.Combine(_logDir, $"app_{DateTime.Now:yyyyMM}.log");

        await _lock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(logFile, line + Environment.NewLine);
        }
        finally
        {
            _lock.Release();
        }
    }
}

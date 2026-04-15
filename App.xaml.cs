// App.xaml.cs
using InventoryManager.Helpers;
using InventoryManager.Services;
using InventoryManager.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace InventoryManager;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show($"예기치 않은 오류가 발생했습니다.\n\n{ex.Exception.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        LiveCharts.Configure(config =>
            config.HasGlobalSKTypeface(SKFontManager.Default.MatchCharacter('헬')));

        ServiceLocator.Initialize();

        // ── 업데이트 체크 (메인 윈도우 표시 전) ──
        await CheckForUpdateAsync();
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var checker = new UpdateChecker();
            var result = await checker.CheckAsync();

            if (result.Status == UpdateStatus.UpToDate) return;

            var isMandatory = result.Status == UpdateStatus.Mandatory;
            var dialog = new UpdateDialog(result.Info!, result.CurrentVersion!, isMandatory);
            dialog.ShowDialog();

            if (dialog.UserAccepted)
            {
                LaunchUpdater(result.Info!);
                // Updater에게 인계 후 메인 앱 종료
                Shutdown(0);
            }
            else if (isMandatory)
            {
                // 강제 업데이트 거부(불가) → 앱 종료
                Shutdown(1);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateCheck] 실패 무시: {ex.Message}");
            // 업데이트 체크 실패는 앱 실행을 막지 않는다
        }
    }

    private static void LaunchUpdater(VersionInfo info)
    {
        var updaterPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");

        if (!File.Exists(updaterPath))
            throw new FileNotFoundException("Updater.exe를 찾을 수 없습니다.", updaterPath);

        var installDir = AppDomain.CurrentDomain.BaseDirectory;
        var mainExe = Process.GetCurrentProcess().MainModule!.FileName;
        var pid = Process.GetCurrentProcess().Id;

        // 인자에 공백이 있을 수 있으므로 따옴표로 감싼다
        var args = $"--pid {pid} " +
                   $"--dir \"{installDir}\" " +
                   $"--url \"{info.DownloadUrl}\" " +
                   $"--hash \"{info.Sha256}\" " +
                   $"--exe \"{mainExe}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = args,
            UseShellExecute = false
        });
    }
}
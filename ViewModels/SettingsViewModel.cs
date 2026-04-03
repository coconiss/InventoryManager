using System.Collections.ObjectModel;
using InventoryManager.Helpers;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;

namespace InventoryManager.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly BarcodeService _barcodeService;
    private readonly BackupService _backupService;

    // ── 바코드 설정 ──
    private string _scannerMode = "USB (HID) - 자동";
    public string ScannerMode { get => _scannerMode; set => SetProperty(ref _scannerMode, value); }

    public ObservableCollection<string> AvailablePorts { get; } = [];

    private string _selectedPort = "COM1";
    public string SelectedPort { get => _selectedPort; set => SetProperty(ref _selectedPort, value); }

    private int _baudRate = 9600;
    public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

    // ── 재고 경고 ──
    private int _lowStock_warning = 5;
    public int LowStockWarning
    {
        get => _lowStock_warning;
        set
        {
            SetProperty(ref _lowStock_warning, value);
            StockColorConverter.LowStockThreshold = value; // 전역 적용
        }
    }

    // ── 백업 ──
    private string _backupPath = string.Empty;
    public string BackupPath { get => _backupPath; set => SetProperty(ref _backupPath, value); }

    private bool _autoBackup = true;
    public bool AutoBackup { get => _autoBackup; set => SetProperty(ref _autoBackup, value); }

    // ── 라이선스 ──
    public string LicenseText => """
        재고 관리 시스템 v1.0.0
        Copyright © 2026. All Rights Reserved.

        본 프로그램은 귀하에게 단일 사업장에서의
        사용 권한을 부여합니다. 무단 복제 및 배포를 금합니다.

        SQLite: Public Domain
        .NET Runtime: MIT License
        """;

    // ── Commands ──
    public AsyncRelayCommand TestScannerCommand { get; }
    public RelayCommand SelectBackupFolderCommand { get; }
    public AsyncRelayCommand BackupNowCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    public SettingsViewModel(BarcodeService barcodeService, BackupService backupService)
    {
        _barcodeService = barcodeService;
        _backupService = backupService;

        // COM 포트 목록 로드
        foreach (var p in BarcodeService.GetAvailablePorts())
            AvailablePorts.Add(p);

        TestScannerCommand = new AsyncRelayCommand(TestScannerAsync);
        SelectBackupFolderCommand = new RelayCommand(SelectBackupFolder);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
    }

    private async Task TestScannerAsync()
    {
        StatusMessage = "스캐너 연결 테스트 중...";
        try
        {
            var ok = _barcodeService.ConnectSerial(SelectedPort, BaudRate);
            if (ok)
            {
                StatusMessage = $"✅ 연결 성공: {SelectedPort}@{BaudRate}";
                System.Diagnostics.Debug.WriteLine($"[Settings] ConnectSerial success: {SelectedPort}@{BaudRate}");
            }
            else
            {
                StatusMessage = "❌ 연결 실패. 포트와 속도를 확인하세요.";
                System.Diagnostics.Debug.WriteLine("[Settings] ConnectSerial returned false");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 연결 시 예외 발생: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Settings] ConnectSerial exception: {ex}");
        }
    }

    private void SelectBackupFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            BackupPath = dialog.SelectedPath;
    }

    private async Task BackupNowAsync()
    {
        try
        {
            var dest = await _backupService.BackupAsync(
                string.IsNullOrWhiteSpace(BackupPath) ? null : BackupPath);
            StatusMessage = $"✅ 백업 완료: {dest}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 백업 실패: {ex.Message}";
        }
    }

    private void SaveSettings()
    {
        StockColorConverter.LowStockThreshold = LowStockWarning;
        if (AutoBackup)
            _backupService.StartAutoBackup(24, BackupPath);
        else
            _backupService.StopAutoBackup();
        StatusMessage = "✅ 설정이 저장되었습니다.";
    }
}

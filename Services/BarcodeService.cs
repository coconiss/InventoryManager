using System.IO.Ports;

namespace InventoryManager.Services;

/// <summary>
/// 바코드 스캐너 서비스 - USB(HID), Serial(COM), Bluetooth 지원
/// USB HID 방식: WPF KeyDown 이벤트로 처리 (별도 드라이버 불필요)
/// Serial 방식: SerialPort 직접 연결
/// </summary>
public class BarcodeService : IDisposable
{
    private SerialPort? _serialPort;
    private string _buffer = string.Empty;
    private readonly System.Timers.Timer _hIdTimer;

    public event EventHandler<string>? BarcodeScanned;

    public BarcodeService()
    {
        // USB HID 입력은 글로벌 키 이벤트가 빠르게 들어오므로
        // 50ms 내 버퍼에 쌓인 문자를 바코드로 처리
        _hIdTimer = new System.Timers.Timer(50);
        _hIdTimer.Elapsed += OnHidTimerElapsed;
        _hIdTimer.AutoReset = false;
    }

    // ─── USB HID 방식 (WPF TextInput 이벤트에서 호출) ───────────────────

    /// <summary>
    /// WPF Window에서 PreviewTextInput 이벤트로 각 문자 전달
    /// </summary>
    public void OnTextInput(string text)
    {
        _hIdTimer.Stop();
        _buffer += text;
        _hIdTimer.Start();
    }

    public void OnEnterKey()
    {
        _hIdTimer.Stop();
        FlushBuffer();
    }

    private void OnHidTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        => FlushBuffer();

    private void FlushBuffer()
    {
        if (string.IsNullOrWhiteSpace(_buffer)) return;
        var barcode = _buffer.Trim();
        _buffer = string.Empty;

        // UI 스레드에서 이벤트 발생
        App.Current.Dispatcher.Invoke(() => BarcodeScanned?.Invoke(this, barcode));
    }

    // ─── Serial (COM) 방식 ───────────────────────────────────────────────

    public bool ConnectSerial(string portName, int baudRate = 9600)
    {
        try
        {
            DisconnectSerial();
            _serialPort = new SerialPort(portName, baudRate)
            {
                NewLine = "\r\n",
                ReadTimeout = 500
            };
            _serialPort.DataReceived += OnSerialDataReceived;
            _serialPort.Open();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Barcode] Serial 연결 실패: {ex.Message}");
            return false;
        }
    }

    public void DisconnectSerial()
    {
        if (_serialPort?.IsOpen == true)
            _serialPort.Close();
        _serialPort?.Dispose();
        _serialPort = null;
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var barcode = _serialPort?.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(barcode))
                App.Current.Dispatcher.Invoke(() => BarcodeScanned?.Invoke(this, barcode));
        }
        catch { /* timeout 무시 */ }
    }

    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public void Dispose()
    {
        _hIdTimer.Dispose();
        DisconnectSerial();
    }
}

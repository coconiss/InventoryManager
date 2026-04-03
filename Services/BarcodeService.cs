using System.IO.Ports;
using System.Text;

namespace InventoryManager.Services;

public class BarcodeService : IDisposable
{
    private SerialPort? _serialPort;

    // HID 버퍼
    private string _buffer = string.Empty;
    private readonly System.Timers.Timer _hIdTimer;

    // Serial 버퍼 (Byte 기반)
    private readonly List<byte> _rxBuffer = new();

    public event EventHandler<string>? BarcodeScanned;

    public BarcodeService()
    {
        _hIdTimer = new System.Timers.Timer(50);
        _hIdTimer.Elapsed += OnHidTimerElapsed;
        _hIdTimer.AutoReset = false;
    }

    // ─────────────────────────────────────────────
    // HID 방식
    // ─────────────────────────────────────────────

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

        try
        {
            App.Current.Dispatcher.InvokeAsync(() =>
                BarcodeScanned?.Invoke(this, barcode));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Barcode] HID 오류: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────
    // Serial 방식 (STX/ETX 프레임 기반)
    // ─────────────────────────────────────────────

    public bool ConnectSerial(string portName, int baudRate = 9600)
    {
        try
        {
            DisconnectSerial();

            _serialPort = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Encoding = Encoding.ASCII
            };

            _serialPort.DataReceived += OnSerialDataReceived;
            _serialPort.Open();

            _rxBuffer.Clear();

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
        if (_serialPort != null)
        {
            _serialPort.DataReceived -= OnSerialDataReceived;

            if (_serialPort.IsOpen)
            {
                try { _serialPort.Close(); } catch { }
            }

            _serialPort.Dispose();
            _serialPort = null;
        }

        _rxBuffer.Clear();
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            int len = _serialPort.BytesToRead;
            byte[] buffer = new byte[len];

            _serialPort.Read(buffer, 0, len);

            lock (_rxBuffer)
            {
                _rxBuffer.AddRange(buffer);

                ParsePackets();
            }
        }
        catch (InvalidOperationException)
        {
            // 포트 닫히는 중
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Barcode] Serial 오류: {ex.Message}");
        }
    }

    private void ParsePackets()
    {
        while (true)
        {
            int stxIndex = _rxBuffer.IndexOf(0x02);
            int etxIndex = _rxBuffer.IndexOf(0x03);

            // 유효한 프레임이 없는 경우
            if (stxIndex < 0 || etxIndex < 0 || etxIndex <= stxIndex)
                return;

            // STX ~ ETX 사이 데이터 추출
            int length = etxIndex - stxIndex - 1;
            if (length <= 0)
            {
                _rxBuffer.RemoveRange(0, etxIndex + 1);
                continue;
            }

            byte[] packet = _rxBuffer
                .Skip(stxIndex + 1)
                .Take(length)
                .ToArray();

            // 처리된 데이터 제거
            _rxBuffer.RemoveRange(0, etxIndex + 1);

            string barcode = Encoding.ASCII.GetString(packet).Trim();

            if (!string.IsNullOrEmpty(barcode))
            {
                string captured = barcode;

                App.Current.Dispatcher.InvokeAsync(() =>
                    BarcodeScanned?.Invoke(this, captured));
            }
        }
    }

    public static string[] GetAvailablePorts()
        => SerialPort.GetPortNames();

    public void Dispose()
    {
        _hIdTimer.Dispose();
        DisconnectSerial();
    }
}
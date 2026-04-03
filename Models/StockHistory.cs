namespace InventoryManager.Models;

/// <summary>
/// 재고 이력 테이블 - 모든 재고 변동의 원천 (IN/OUT/ADJUST)
/// </summary>
public class StockHistory
{
    public long Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public StockType Type { get; set; }
    public int Quantity { get; set; }          // +입고 / -출고 / ±조정
    public int QuantityBefore { get; set; }    // 변경 전 수량 (로그용)
    public int QuantityAfter { get; set; }     // 변경 후 수량 (로그용)
    public string Remark { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "system";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum StockType
{
    IN,       // 입고
    OUT,      // 출고 (판매)
    ADJUST    // 재고 조정
}

/// <summary>
/// 판매 Master (1건의 결제 트랜잭션)
/// </summary>
public class SaleMaster
{
    public long Id { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public List<SaleDetail> Details { get; set; } = [];
}

/// <summary>
/// 판매 Detail (판매된 개별 제품 라인)
/// </summary>
public class SaleDetail
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }           // 판매 시점 단가 (스냅샷)
    public decimal LineTotal => Quantity * Price;
}

/// <summary>
/// 앱 전반에서 사용하는 설정 모델
/// </summary>
public class AppConfig
{
    public int LowStockWarning { get; set; } = 5;          // 재고 경고 수량
    public string BarcodePort { get; set; } = "AUTO";       // COM 포트 or AUTO
    public int BarcodeBaudRate { get; set; } = 9600;
    public string ScannerMode { get; set; } = "USB (HID) - 자동";
    public bool AutoBackup { get; set; } = true;
    public string BackupPath { get; set; } = string.Empty;
}

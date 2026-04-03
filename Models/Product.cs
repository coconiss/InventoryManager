namespace InventoryManager.Models;

/// <summary>
/// 제품 테이블 모델
/// </summary>
public class Product
{
    public string Barcode { get; set; } = string.Empty;   // PK
    public string Name { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }                 // 입고단가
    public decimal SalePrice { get; set; }                 // 판매단가
    public int CurrentQuantity { get; set; }               // 계산된 현재수량 (View 전용)
    public bool IsActive { get; set; } = true;             // Soft Delete
    public string Remark { get; set; } = string.Empty;
}

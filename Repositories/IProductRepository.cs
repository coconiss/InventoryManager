using InventoryManager.Models;

namespace InventoryManager.Repositories;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(string? keyword = null, bool includeInactive = false);
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<bool> ExistsAsync(string barcode);
    Task AddAsync(Product product, int initialQuantity);
    Task UpdateAsync(Product product);
    Task SoftDeleteAsync(string barcode);
    Task<bool> HasSalesHistoryAsync(string barcode);
    Task<int> GetCurrentQuantityAsync(string barcode);
}

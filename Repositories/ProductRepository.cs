using InventoryManager.Data;
using InventoryManager.Models;
using Microsoft.Data.Sqlite;

namespace InventoryManager.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly DatabaseService _db;

    public ProductRepository(DatabaseService db) => _db = db;

    public async Task<List<Product>> GetAllAsync(string? keyword = null, bool includeInactive = false)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT p.barcode, p.name, p.cost_price, p.sale_price, p.is_active, p.remark,
                   COALESCE(SUM(sh.quantity), 0) AS current_quantity
            FROM products p
            LEFT JOIN stock_history sh ON sh.barcode = p.barcode
            WHERE (@includeInactive = 1 OR p.is_active = 1)
              AND (@keyword IS NULL OR p.name LIKE @keyword OR p.barcode LIKE @keyword)
            GROUP BY p.barcode
            ORDER BY p.name
            """;

        cmd.Parameters.AddWithValue("@includeInactive", includeInactive ? 1 : 0);
        cmd.Parameters.AddWithValue("@keyword",
            keyword is null ? DBNull.Value : $"%{keyword}%");

        var list = new List<Product>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapProduct(reader));
        return list;
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        // is_active 필터 없음 → 비활성 제품도 반환
        cmd.CommandText = """
            SELECT p.barcode, p.name, p.cost_price, p.sale_price, p.is_active, p.remark,
                   COALESCE(SUM(sh.quantity), 0) AS current_quantity
            FROM products p
            LEFT JOIN stock_history sh ON sh.barcode = p.barcode
            WHERE p.barcode = @barcode
            GROUP BY p.barcode
            """;
        cmd.Parameters.AddWithValue("@barcode", barcode);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapProduct(reader) : null;
    }

    public async Task<bool> ExistsAsync(string barcode)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM products WHERE barcode = @b";
        cmd.Parameters.AddWithValue("@b", barcode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task AddAsync(Product product, int initialQuantity)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = tx;
            cmd1.CommandText = """
                INSERT INTO products (barcode, name, cost_price, sale_price, is_active, remark)
                VALUES (@barcode, @name, @cost, @sale, 1, @remark)
                """;
            cmd1.Parameters.AddWithValue("@barcode", product.Barcode);
            cmd1.Parameters.AddWithValue("@name", product.Name);
            cmd1.Parameters.AddWithValue("@cost", product.CostPrice);
            cmd1.Parameters.AddWithValue("@sale", product.SalePrice);
            cmd1.Parameters.AddWithValue("@remark", product.Remark);
            await cmd1.ExecuteNonQueryAsync();

            await using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = tx;
            cmd2.CommandText = """
                INSERT INTO stock_history (barcode, type, quantity, quantity_before, quantity_after, remark)
                VALUES (@barcode, 'IN', @qty, 0, @qty, '최초 등록')
                """;
            cmd2.Parameters.AddWithValue("@barcode", product.Barcode);
            cmd2.Parameters.AddWithValue("@qty", initialQuantity);
            await cmd2.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>
    /// 비활성 제품을 재활성화하고 재고를 설정값으로 조정
    /// </summary>
    public async Task ReactivateAsync(Product product, int initialQuantity)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = tx;
            cmd1.CommandText = """
                UPDATE products SET
                    name = @name, cost_price = @cost, sale_price = @sale,
                    remark = @remark, is_active = 1,
                    updated_at = datetime('now','localtime')
                WHERE barcode = @barcode
                """;
            cmd1.Parameters.AddWithValue("@barcode", product.Barcode);
            cmd1.Parameters.AddWithValue("@name", product.Name);
            cmd1.Parameters.AddWithValue("@cost", product.CostPrice);
            cmd1.Parameters.AddWithValue("@sale", product.SalePrice);
            cmd1.Parameters.AddWithValue("@remark", product.Remark);
            await cmd1.ExecuteNonQueryAsync();

            // 현재 누적 재고 조회 후 목표 수량으로 조정
            await using var qCmd = conn.CreateCommand();
            qCmd.Transaction = tx;
            qCmd.CommandText = "SELECT COALESCE(SUM(quantity),0) FROM stock_history WHERE barcode = @b";
            qCmd.Parameters.AddWithValue("@b", product.Barcode);
            int currentQty = Convert.ToInt32(await qCmd.ExecuteScalarAsync());
            int delta = initialQuantity - currentQty;

            if (delta != 0)
            {
                await using var cmd2 = conn.CreateCommand();
                cmd2.Transaction = tx;
                cmd2.CommandText = """
                    INSERT INTO stock_history (barcode, type, quantity, quantity_before, quantity_after, remark)
                    VALUES (@barcode, @type, @qty, @before, @after, '재등록 조정')
                    """;
                cmd2.Parameters.AddWithValue("@barcode", product.Barcode);
                cmd2.Parameters.AddWithValue("@type", delta > 0 ? "IN" : "ADJUST");
                cmd2.Parameters.AddWithValue("@qty", delta);
                cmd2.Parameters.AddWithValue("@before", currentQty);
                cmd2.Parameters.AddWithValue("@after", initialQuantity);
                await cmd2.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task UpdateAsync(Product product)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE products SET
                name = @name, cost_price = @cost, sale_price = @sale,
                remark = @remark, updated_at = datetime('now','localtime')
            WHERE barcode = @barcode
            """;
        cmd.Parameters.AddWithValue("@barcode", product.Barcode);
        cmd.Parameters.AddWithValue("@name", product.Name);
        cmd.Parameters.AddWithValue("@cost", product.CostPrice);
        cmd.Parameters.AddWithValue("@sale", product.SalePrice);
        cmd.Parameters.AddWithValue("@remark", product.Remark);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SoftDeleteAsync(string barcode)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE products SET is_active = 0 WHERE barcode = @b";
        cmd.Parameters.AddWithValue("@b", barcode);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasSalesHistoryAsync(string barcode)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sale_details WHERE barcode = @b";
        cmd.Parameters.AddWithValue("@b", barcode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> GetCurrentQuantityAsync(string barcode)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(quantity),0) FROM stock_history WHERE barcode = @b";
        cmd.Parameters.AddWithValue("@b", barcode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static Product MapProduct(SqliteDataReader r) => new()
    {
        Barcode = r.GetString(0),
        Name = r.GetString(1),
        CostPrice = r.GetDecimal(2),
        SalePrice = r.GetDecimal(3),
        IsActive = r.GetInt32(4) == 1,
        Remark = r.IsDBNull(5) ? "" : r.GetString(5),
        CurrentQuantity = r.GetInt32(6)
    };
}
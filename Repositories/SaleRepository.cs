using InventoryManager.Data;
using InventoryManager.Models;

namespace InventoryManager.Repositories;

public interface ISaleRepository
{
    Task<long> ProcessSaleAsync(List<SaleDetail> cart);
    Task<List<SaleMaster>> GetDailySalesAsync(DateTime from, DateTime to);
    Task<List<(string YearMonth, decimal Total)>> GetMonthlySalesAsync(int year);
    Task<List<(string Barcode, string Name, decimal Total, int Qty)>> GetProductSalesAsync(DateTime from, DateTime to);
}

public class SaleRepository : ISaleRepository
{
    private readonly DatabaseService _db;
    private readonly IStockRepository _stockRepo;

    public SaleRepository(DatabaseService db, IStockRepository stockRepo)
    {
        _db = db;
        _stockRepo = stockRepo;
    }

    /// <summary>
    /// 판매 처리: Master + Detail + 재고 출고를 단일 트랜잭션으로 처리 (Atomic)
    /// </summary>
    public async Task<long> ProcessSaleAsync(List<SaleDetail> cart)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            // 1) 재고 부족 사전 검증
            foreach (var item in cart)
            {
                await using var stockCmd = conn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText =
                    "SELECT COALESCE(SUM(quantity),0) FROM stock_history WHERE barcode = @b";
                stockCmd.Parameters.AddWithValue("@b", item.Barcode);
                int currentQty = Convert.ToInt32(await stockCmd.ExecuteScalarAsync());

                if (currentQty < item.Quantity)
                    throw new InvalidOperationException(
                        $"재고 부족: [{item.ProductName}] 현재 재고 {currentQty}개, 판매 요청 {item.Quantity}개");
            }

            // 2) 판매 Master 생성
            decimal totalAmount = cart.Sum(d => d.LineTotal);
            await using var masterCmd = conn.CreateCommand();
            masterCmd.Transaction = tx;
            masterCmd.CommandText = """
                INSERT INTO sale_masters (total_amount) VALUES (@total);
                SELECT last_insert_rowid();
                """;
            masterCmd.Parameters.AddWithValue("@total", totalAmount);
            long saleId = Convert.ToInt64(await masterCmd.ExecuteScalarAsync());

            var txWrapper = new SqliteTransactionWrapper { Connection = conn, Transaction = tx };

            // 3) 판매 Detail 및 재고 출고 처리
            foreach (var item in cart)
            {
                // Detail 저장
                await using var detailCmd = conn.CreateCommand();
                detailCmd.Transaction = tx;
                detailCmd.CommandText = """
                    INSERT INTO sale_details (sale_id, barcode, product_name, quantity, price)
                    VALUES (@sid, @barcode, @name, @qty, @price)
                    """;
                detailCmd.Parameters.AddWithValue("@sid", saleId);
                detailCmd.Parameters.AddWithValue("@barcode", item.Barcode);
                detailCmd.Parameters.AddWithValue("@name", item.ProductName);
                detailCmd.Parameters.AddWithValue("@qty", item.Quantity);
                detailCmd.Parameters.AddWithValue("@price", item.Price);
                await detailCmd.ExecuteNonQueryAsync();

                // 재고 출고 이력 기록
                await _stockRepo.RecordOutboundAsync(item.Barcode, item.Quantity, saleId, txWrapper);
            }

            tx.Commit();
            return saleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<List<SaleMaster>> GetDailySalesAsync(DateTime from, DateTime to)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DATE(created_at) AS sale_date,
                   COUNT(*) AS count,
                   SUM(total_amount) AS total
            FROM sale_masters
            WHERE created_at >= @from AND created_at <= @to
            GROUP BY DATE(created_at)
            ORDER BY sale_date
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        var list = new List<SaleMaster>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SaleMaster
            {
                CreatedAt = DateTime.Parse(reader.GetString(0)),
                TotalAmount = reader.GetDecimal(2)
            });
        }
        return list;
    }

    public async Task<List<(string YearMonth, decimal Total)>> GetMonthlySalesAsync(int year)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT strftime('%Y-%m', created_at) AS ym, SUM(total_amount)
            FROM sale_masters
            WHERE strftime('%Y', created_at) = @year
            GROUP BY ym ORDER BY ym
            """;
        cmd.Parameters.AddWithValue("@year", year.ToString());

        var list = new List<(string, decimal)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetDecimal(1)));
        return list;
    }

    public async Task<List<(string Barcode, string Name, decimal Total, int Qty)>> GetProductSalesAsync(
        DateTime from, DateTime to)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.barcode, d.product_name,
                   SUM(d.quantity * d.price) AS total,
                   SUM(d.quantity) AS qty
            FROM sale_details d
            JOIN sale_masters m ON m.id = d.sale_id
            WHERE m.created_at >= @from AND m.created_at <= @to
            GROUP BY d.barcode
            ORDER BY total DESC
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        var list = new List<(string, string, decimal, int)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetInt32(3)));
        return list;
    }
}

using InventoryManager.Data;
using InventoryManager.Models;

namespace InventoryManager.Repositories;

public interface ISaleRepository
{
    Task<long> ProcessSaleAsync(List<SaleDetail> cart);
    Task UpdateSaleAsync(long saleId, List<SaleDetail> cart);
    Task DeleteSaleAsync(long saleId);
    Task<List<SaleMaster>> GetDailySalesAsync(DateTime from, DateTime to);
    Task<List<(string YearMonth, decimal Total)>> GetMonthlySalesAsync(int year);
    Task<List<(string Barcode, string Name, decimal Total, int Qty)>> GetProductSalesAsync(DateTime from, DateTime to);
    Task<List<SaleMaster>> GetIndividualSalesAsync(DateTime from, DateTime to);
    Task<List<SaleDetail>> GetSaleDetailsAsync(long saleId);
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

    public async Task<long> ProcessSaleAsync(List<SaleDetail> cart)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
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
            foreach (var item in cart)
            {
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

                await _stockRepo.RecordOutboundAsync(item.Barcode, item.Quantity, saleId, txWrapper);
            }

            tx.Commit();
            return saleId;
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>
    /// 기존 판매 수정: 구재고 복원 → 신규 재고 검증 → details 교체 → master 금액 갱신
    /// </summary>
    public async Task UpdateSaleAsync(long saleId, List<SaleDetail> cart)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            // 1) 기존 details 조회 (재고 복원을 위해)
            await using var oldCmd = conn.CreateCommand();
            oldCmd.Transaction = tx;
            oldCmd.CommandText = "SELECT barcode, quantity FROM sale_details WHERE sale_id = @sid";
            oldCmd.Parameters.AddWithValue("@sid", saleId);

            var oldItems = new List<(string Barcode, int Qty)>();
            await using (var r = await oldCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    oldItems.Add((r.GetString(0), r.GetInt32(1)));
            }

            // 2) 기존 OUT 재고 복원 (IN으로 상쇄)
            foreach (var (barcode, qty) in oldItems)
            {
                await using var restoreCmd = conn.CreateCommand();
                restoreCmd.Transaction = tx;
                restoreCmd.CommandText = """
                    INSERT INTO stock_history (barcode, type, quantity, remark)
                    VALUES (@barcode, 'IN', @qty, @remark)
                    """;
                restoreCmd.Parameters.AddWithValue("@barcode", barcode);
                restoreCmd.Parameters.AddWithValue("@qty", qty);  // 양수 — OUT 취소
                restoreCmd.Parameters.AddWithValue("@remark", $"판매수정 복원 #{saleId}");
                await restoreCmd.ExecuteNonQueryAsync();
            }

            // 3) 신규 항목 재고 검증
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

            // 4) 기존 details 삭제
            await using var delCmd = conn.CreateCommand();
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM sale_details WHERE sale_id = @sid";
            delCmd.Parameters.AddWithValue("@sid", saleId);
            await delCmd.ExecuteNonQueryAsync();

            // 5) 신규 details 삽입 + 재고 OUT
            var txWrapper = new SqliteTransactionWrapper { Connection = conn, Transaction = tx };
            foreach (var item in cart)
            {
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

                await _stockRepo.RecordOutboundAsync(item.Barcode, item.Quantity, saleId, txWrapper);
            }

            // 6) master 금액 갱신
            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText =
                "UPDATE sale_masters SET total_amount = @total WHERE id = @sid";
            updateCmd.Parameters.AddWithValue("@total", cart.Sum(d => d.LineTotal));
            updateCmd.Parameters.AddWithValue("@sid", saleId);
            await updateCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    /// <summary>
    /// 판매 삭제: 재고 복원 → details 삭제 → master 삭제
    /// </summary>
    public async Task DeleteSaleAsync(long saleId)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            // 1) 기존 details 조회
            await using var oldCmd = conn.CreateCommand();
            oldCmd.Transaction = tx;
            oldCmd.CommandText = "SELECT barcode, quantity FROM sale_details WHERE sale_id = @sid";
            oldCmd.Parameters.AddWithValue("@sid", saleId);

            var oldItems = new List<(string Barcode, int Qty)>();
            await using (var r = await oldCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                    oldItems.Add((r.GetString(0), r.GetInt32(1)));
            }

            // 2) 재고 복원
            foreach (var (barcode, qty) in oldItems)
            {
                await using var restoreCmd = conn.CreateCommand();
                restoreCmd.Transaction = tx;
                restoreCmd.CommandText = """
                    INSERT INTO stock_history (barcode, type, quantity, remark)
                    VALUES (@barcode, 'IN', @qty, @remark)
                    """;
                restoreCmd.Parameters.AddWithValue("@barcode", barcode);
                restoreCmd.Parameters.AddWithValue("@qty", qty);
                restoreCmd.Parameters.AddWithValue("@remark", $"판매 삭제 복원 #{saleId}");
                await restoreCmd.ExecuteNonQueryAsync();
            }

            // 3) details → master 순으로 삭제 (FK 제약 순서)
            await using var delDetailCmd = conn.CreateCommand();
            delDetailCmd.Transaction = tx;
            delDetailCmd.CommandText = "DELETE FROM sale_details WHERE sale_id = @sid";
            delDetailCmd.Parameters.AddWithValue("@sid", saleId);
            await delDetailCmd.ExecuteNonQueryAsync();

            await using var delMasterCmd = conn.CreateCommand();
            delMasterCmd.Transaction = tx;
            delMasterCmd.CommandText = "DELETE FROM sale_masters WHERE id = @sid";
            delMasterCmd.Parameters.AddWithValue("@sid", saleId);
            await delMasterCmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<List<SaleMaster>> GetDailySalesAsync(DateTime from, DateTime to)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DATE(created_at), COUNT(*), SUM(total_amount)
            FROM sale_masters
            WHERE created_at >= @from AND created_at <= @to
            GROUP BY DATE(created_at)
            ORDER BY DATE(created_at)
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        var list = new List<SaleMaster>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SaleMaster
            {
                CreatedAt = DateTime.Parse(r.GetString(0)),
                TotalAmount = r.GetDecimal(2)
            });
        return list;
    }

    public async Task<List<(string YearMonth, decimal Total)>> GetMonthlySalesAsync(int year)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT strftime('%Y-%m', created_at), SUM(total_amount)
            FROM sale_masters
            WHERE strftime('%Y', created_at) = @year
            GROUP BY strftime('%Y-%m', created_at)
            ORDER BY strftime('%Y-%m', created_at)
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
                   SUM(d.quantity * d.price), SUM(d.quantity)
            FROM sale_details d
            JOIN sale_masters m ON m.id = d.sale_id
            WHERE m.created_at >= @from AND m.created_at <= @to
            GROUP BY d.barcode
            ORDER BY SUM(d.quantity * d.price) DESC
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        var list = new List<(string, string, decimal, int)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetInt32(3)));
        return list;
    }

    public async Task<List<SaleMaster>> GetIndividualSalesAsync(DateTime from, DateTime to)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, total_amount, created_at
            FROM sale_masters
            WHERE created_at >= @from AND created_at <= @to
            ORDER BY created_at DESC
            LIMIT 200
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        var list = new List<SaleMaster>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SaleMaster
            {
                Id = r.GetInt64(0),
                TotalAmount = r.GetDecimal(1),
                CreatedAt = DateTime.Parse(r.GetString(2))
            });
        return list;
    }

    public async Task<List<SaleDetail>> GetSaleDetailsAsync(long saleId)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, sale_id, barcode, product_name, quantity, price
            FROM sale_details WHERE sale_id = @sid
            """;
        cmd.Parameters.AddWithValue("@sid", saleId);

        var list = new List<SaleDetail>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SaleDetail
            {
                Id = r.GetInt64(0),
                SaleId = r.GetInt64(1),
                Barcode = r.GetString(2),
                ProductName = r.GetString(3),
                Quantity = r.GetInt32(4),
                Price = r.GetDecimal(5)
            });
        return list;
    }
}
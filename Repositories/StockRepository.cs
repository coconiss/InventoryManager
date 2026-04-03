using InventoryManager.Data;
using InventoryManager.Models;

namespace InventoryManager.Repositories;

public interface IStockRepository
{
    Task<List<StockHistory>> GetHistoryAsync(string? barcode = null, DateTime? from = null, DateTime? to = null);
    Task AdjustStockAsync(string barcode, int adjustQty, string remark, string createdBy = "system");
    Task RecordInboundAsync(string barcode, int quantity, string remark = "입고");
    Task RecordOutboundAsync(string barcode, int quantity, long saleId, SqliteTransactionWrapper tx);
}

/// <summary>
/// SqliteTransaction을 Repository 계층에서 주고받기 위한 래퍼
/// </summary>
public class SqliteTransactionWrapper
{
    public Microsoft.Data.Sqlite.SqliteConnection Connection { get; init; } = null!;
    public Microsoft.Data.Sqlite.SqliteTransaction Transaction { get; init; } = null!;
}

public class StockRepository : IStockRepository
{
    private readonly DatabaseService _db;
    public StockRepository(DatabaseService db) => _db = db;

    public async Task<List<StockHistory>> GetHistoryAsync(
        string? barcode = null, DateTime? from = null, DateTime? to = null)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, barcode, type, quantity, quantity_before, quantity_after,
                   remark, created_by, created_at
            FROM stock_history
            WHERE (@barcode IS NULL OR barcode = @barcode)
              AND (@from IS NULL OR created_at >= @from)
              AND (@to   IS NULL OR created_at <= @to)
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("@barcode", barcode ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@from", from?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@to", to?.ToString("yyyy-MM-dd 23:59:59") ?? (object)DBNull.Value);

        var list = new List<StockHistory>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new StockHistory
            {
                Id = reader.GetInt64(0),
                Barcode = reader.GetString(1),
                Type = Enum.Parse<StockType>(reader.GetString(2)),
                Quantity = reader.GetInt32(3),
                QuantityBefore = reader.GetInt32(4),
                QuantityAfter = reader.GetInt32(5),
                Remark = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CreatedBy = reader.IsDBNull(7) ? "system" : reader.GetString(7),
                CreatedAt = DateTime.Parse(reader.GetString(8))
            });
        }
        return list;
    }

    /// <summary>
    /// 재고 조정 - 항상 이력 기반, 직접 수량 수정 금지
    /// </summary>
    public async Task AdjustStockAsync(string barcode, int adjustQty, string remark, string createdBy = "system")
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            // 현재 수량 조회 (잠금)
            await using var qCmd = conn.CreateCommand();
            qCmd.Transaction = tx;
            qCmd.CommandText = "SELECT COALESCE(SUM(quantity),0) FROM stock_history WHERE barcode = @b";
            qCmd.Parameters.AddWithValue("@b", barcode);
            int before = Convert.ToInt32(await qCmd.ExecuteScalarAsync());
            int after = before + adjustQty;

            if (after < 0)
                throw new InvalidOperationException("조정 후 재고가 0 미만이 될 수 없습니다.");

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO stock_history
                    (barcode, type, quantity, quantity_before, quantity_after, remark, created_by)
                VALUES (@barcode, 'ADJUST', @qty, @before, @after, @remark, @by)
                """;
            cmd.Parameters.AddWithValue("@barcode", barcode);
            cmd.Parameters.AddWithValue("@qty", adjustQty);
            cmd.Parameters.AddWithValue("@before", before);
            cmd.Parameters.AddWithValue("@after", after);
            cmd.Parameters.AddWithValue("@remark", remark);
            cmd.Parameters.AddWithValue("@by", createdBy);
            await cmd.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task RecordInboundAsync(string barcode, int quantity, string remark = "입고")
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO stock_history (barcode, type, quantity, remark)
            VALUES (@barcode, 'IN', @qty, @remark)
            """;
        cmd.Parameters.AddWithValue("@barcode", barcode);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@remark", remark);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 판매 시 출고 처리 - 외부 트랜잭션 내에서 호출 (판매 트랜잭션과 묶음)
    /// </summary>
    public async Task RecordOutboundAsync(string barcode, int quantity, long saleId, SqliteTransactionWrapper tx)
    {
        await using var cmd = tx.Connection.CreateCommand();
        cmd.Transaction = tx.Transaction;
        cmd.CommandText = """
            INSERT INTO stock_history (barcode, type, quantity, remark)
            VALUES (@barcode, 'OUT', @qty, @remark)
            """;
        cmd.Parameters.AddWithValue("@barcode", barcode);
        cmd.Parameters.AddWithValue("@qty", -quantity);   // 출고는 음수
        cmd.Parameters.AddWithValue("@remark", $"판매 #{saleId}");
        await cmd.ExecuteNonQueryAsync();
    }
}

using InventoryManager.Data;
using InventoryManager.Models;

namespace InventoryManager.Repositories;

public interface IHoldRepository
{
    Task<long> SaveCartAsync(string label, List<SaleDetail> items);
    Task<List<HeldCart>> GetAllAsync();
    Task<List<SaleDetail>> GetItemsAsync(long cartId);
    Task DeleteAsync(long cartId);
}

public class HoldRepository : IHoldRepository
{
    private readonly DatabaseService _db;
    public HoldRepository(DatabaseService db) => _db = db;

    public async Task<long> SaveCartAsync(string label, List<SaleDetail> items)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var masterCmd = conn.CreateCommand();
            masterCmd.Transaction = tx;
            masterCmd.CommandText = """
                INSERT INTO held_carts (label) VALUES (@label);
                SELECT last_insert_rowid();
                """;
            masterCmd.Parameters.AddWithValue("@label", label);
            long cartId = Convert.ToInt64(await masterCmd.ExecuteScalarAsync());

            foreach (var item in items)
            {
                await using var itemCmd = conn.CreateCommand();
                itemCmd.Transaction = tx;
                itemCmd.CommandText = """
                    INSERT INTO held_cart_items (cart_id, barcode, product_name, quantity, price)
                    VALUES (@cid, @barcode, @name, @qty, @price)
                    """;
                itemCmd.Parameters.AddWithValue("@cid", cartId);
                itemCmd.Parameters.AddWithValue("@barcode", item.Barcode);
                itemCmd.Parameters.AddWithValue("@name", item.ProductName);
                itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                itemCmd.Parameters.AddWithValue("@price", item.Price);
                await itemCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return cartId;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<List<HeldCart>> GetAllAsync()
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT h.id, h.label, h.created_at,
                   COALESCE(SUM(i.quantity * i.price), 0) AS total,
                   COUNT(i.id) AS item_count
            FROM held_carts h
            LEFT JOIN held_cart_items i ON i.cart_id = h.id
            GROUP BY h.id
            ORDER BY h.created_at DESC
            """;

        var list = new List<HeldCart>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new HeldCart
            {
                Id = r.GetInt64(0),
                Label = r.GetString(1),
                CreatedAt = DateTime.Parse(r.GetString(2)),
                TotalAmount = r.GetDecimal(3),
                ItemCount = r.GetInt32(4)
            });
        }
        return list;
    }

    public async Task<List<SaleDetail>> GetItemsAsync(long cartId)
    {
        await using var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT barcode, product_name, quantity, price
            FROM held_cart_items WHERE cart_id = @cid
            """;
        cmd.Parameters.AddWithValue("@cid", cartId);

        var list = new List<SaleDetail>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SaleDetail
            {
                Barcode = r.GetString(0),
                ProductName = r.GetString(1),
                Quantity = r.GetInt32(2),
                Price = r.GetDecimal(3)
            });
        }
        return list;
    }

    public async Task DeleteAsync(long cartId)
    {
        await using var conn = _db.GetConnection();
        await using var tx = conn.BeginTransaction();
        try
        {
            await using var delItems = conn.CreateCommand();
            delItems.Transaction = tx;
            delItems.CommandText = "DELETE FROM held_cart_items WHERE cart_id = @cid";
            delItems.Parameters.AddWithValue("@cid", cartId);
            await delItems.ExecuteNonQueryAsync();

            await using var delCart = conn.CreateCommand();
            delCart.Transaction = tx;
            delCart.CommandText = "DELETE FROM held_carts WHERE id = @cid";
            delCart.Parameters.AddWithValue("@cid", cartId);
            await delCart.ExecuteNonQueryAsync();

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }
}
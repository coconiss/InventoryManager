using Microsoft.Data.Sqlite;

namespace InventoryManager.Data;

/// <summary>
/// SQLite 연결 및 스키마 초기화 담당
/// 앱 전역에서 싱글턴으로 사용
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        // 실행 파일 옆에 inventory.db 생성 (오프라인 환경 대응)
        var dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Data",
            "inventory.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath};";

        InitializeSchema();
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // WAL 모드: 동시 읽기/쓰기 성능 향상
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        return conn;
    }

    private void InitializeSchema()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = GetInitSql();
        cmd.ExecuteNonQuery();
    }

    private static string GetInitSql() => """
        -- 제품 테이블
        CREATE TABLE IF NOT EXISTS products (
            barcode     TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            cost_price  REAL NOT NULL DEFAULT 0,
            sale_price  REAL NOT NULL DEFAULT 0,
            is_active   INTEGER NOT NULL DEFAULT 1,
            remark      TEXT DEFAULT '',
            created_at  TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            updated_at  TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );

        -- 재고 이력 테이블 (모든 재고 변동의 원천)
        CREATE TABLE IF NOT EXISTS stock_history (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            barcode         TEXT NOT NULL REFERENCES products(barcode),
            type            TEXT NOT NULL CHECK(type IN ('IN','OUT','ADJUST')),
            quantity        INTEGER NOT NULL,
            quantity_before INTEGER NOT NULL DEFAULT 0,
            quantity_after  INTEGER NOT NULL DEFAULT 0,
            remark          TEXT DEFAULT '',
            created_by      TEXT DEFAULT 'system',
            created_at      TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );

        -- 판매 Master
        CREATE TABLE IF NOT EXISTS sale_masters (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            total_amount REAL NOT NULL DEFAULT 0,
            created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );

        -- 판매 Detail
        CREATE TABLE IF NOT EXISTS sale_details (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            sale_id      INTEGER NOT NULL REFERENCES sale_masters(id),
            barcode      TEXT NOT NULL,
            product_name TEXT NOT NULL,
            quantity     INTEGER NOT NULL,
            price        REAL NOT NULL
        );

        -- 앱 설정
        CREATE TABLE IF NOT EXISTS app_config (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        -- 기본 설정값 삽입 (없을 때만)
        INSERT OR IGNORE INTO app_config (key, value) VALUES
            ('low_stock_warning', '5'),
            ('barcode_port', 'AUTO'),
            ('barcode_baud_rate', '9600'),
            ('auto_backup', 'true'),
            ('backup_path', '');

        -- 인덱스
        CREATE INDEX IF NOT EXISTS idx_stock_barcode ON stock_history(barcode);
        CREATE INDEX IF NOT EXISTS idx_stock_created ON stock_history(created_at);
        CREATE INDEX IF NOT EXISTS idx_sale_detail_sale ON sale_details(sale_id);
        CREATE INDEX IF NOT EXISTS idx_sale_master_created ON sale_masters(created_at);
        """;

    public string GetDbPath() =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "inventory.db");
}

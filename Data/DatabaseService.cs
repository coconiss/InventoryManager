using Microsoft.Data.Sqlite;
using System.IO;
using System;

namespace InventoryManager.Data;

/// <summary>
/// SQLite 데이터베이스 연결 및 초기 스키마 생성을 담당하는 서비스입니다.
/// 애플리케이션 전역에서 싱글턴으로 관리되어야 합니다.
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    /// <summary>
    /// DatabaseService 생성자. AppData 폴더에 DB 경로를 설정하고 스키마를 초기화합니다.
    /// </summary>
    public DatabaseService()
    {
        // 튜닝: Program Files 권한 문제를 피하기 위해 AppData/Local 폴더 사용
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dbPath = Path.Combine(appDataPath, "InventoryManager", "Data", "inventory.db");

        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _connectionString = $"Data Source={_dbPath};";

        InitializeSchema();
    }

    /// <summary>
    /// SQLite 데이터베이스 연결 객체를 반환합니다.
    /// 트랜잭션 및 쿼리 실행 시 이 메서드를 통해 Connection을 얻어 사용합니다.
    /// </summary>
    /// <returns>열려 있는 SqliteConnection 객체</returns>
    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // 튜닝: WAL 모드를 켜서 동시 읽기/쓰기 성능 향상 및 외래키 제약조건 활성화
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        return conn;
    }

    /// <summary>
    /// 데이터베이스 스키마(테이블, 인덱스, 기본 데이터)를 초기화합니다.
    /// 앱 실행 시 테이블이 없으면 생성합니다.
    /// </summary>
    private void InitializeSchema()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = GetInitSql();
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 현재 연결된 데이터베이스 파일의 절대 경로를 반환합니다.
    /// 백업 서비스 등에서 원본 파일 위치를 찾을 때 사용됩니다.
    /// </summary>
    public string GetDbPath() => _dbPath;

    // 테이블 생성 SQL (기존과 동일하므로 생략 없이 유지)
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

        -- 재고 이력 테이블
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

        -- 보류 카트 Master
        CREATE TABLE IF NOT EXISTS held_carts (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            label      TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );

        -- 보류 카트 Detail
        CREATE TABLE IF NOT EXISTS held_cart_items (
            id           INTEGER PRIMARY KEY AUTOINCREMENT,
            cart_id      INTEGER NOT NULL REFERENCES held_carts(id),
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

        -- 기본 설정값 삽입
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
        CREATE INDEX IF NOT EXISTS idx_held_cart_items_cart ON held_cart_items(cart_id);
        """;
}
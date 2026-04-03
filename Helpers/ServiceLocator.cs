using InventoryManager.Data;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels;

namespace InventoryManager.Helpers
{
    public static class ServiceLocator
    {
        private static DatabaseService? _db;
        private static BarcodeService? _barcodeService;
        private static LogService? _log;
        private static BackupService? _backup;
        private static ConfigService? _configService;

        public static void Initialize()
        {
            _db = new DatabaseService();
            _configService = new ConfigService();
            var cfg = _configService.Load();

            _barcodeService = new BarcodeService();
            _log = new LogService();
            _backup = new BackupService(_db);

            // 자동 연결: 설정이 Serial(COM)일 때 포트 자동 연결 시도
            try
            {
                if (cfg.ScannerMode?.Contains("Serial", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (!string.IsNullOrWhiteSpace(cfg.BarcodePort) && cfg.BarcodePort != "AUTO")
                    {
                        _barcodeService.ConnectSerial(cfg.BarcodePort, cfg.BarcodeBaudRate);
                    }
                }
            }
            catch { /* 무시 */ }
        }

        public static ProductViewModel GetProductViewModel()
        {
            var repo = new ProductRepository(_db!);
            return new ProductViewModel(repo, _log!);
        }

        public static StockViewModel GetStockViewModel()
        {
            var productRepo = new ProductRepository(_db!);
            var stockRepo = new StockRepository(_db!);
            return new StockViewModel(productRepo, stockRepo, _log!);
        }

        public static SaleViewModel GetSaleViewModel()
        {
            var productRepo = new ProductRepository(_db!);
            var stockRepo = new StockRepository(_db!);
            var saleRepo = new SaleRepository(_db!, stockRepo);
            return new SaleViewModel(productRepo, saleRepo, _barcodeService!, _log!);
        }

        public static RevenueViewModel GetRevenueViewModel()
        {
            var stockRepo = new StockRepository(_db!);
            var saleRepo = new SaleRepository(_db!, stockRepo);
            return new RevenueViewModel(saleRepo);
        }

        public static ISaleRepository GetSaleRepository()
        {
            var stockRepo = new StockRepository(_db!);
            return new SaleRepository(_db!, stockRepo);
        }

        public static BarcodeService GetBarcodeService() => _barcodeService!;
        public static BackupService GetBackupService() => _backup!;
        public static LogService GetLogService() => _log!;
        public static ConfigService GetConfigService() => _configService!;
    }
}
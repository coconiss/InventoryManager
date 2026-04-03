using InventoryManager.Data;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels;

namespace InventoryManager.Helpers
{
    /// <summary>
    /// 간단한 Service Locator (DI 컨테이너 대신 사용)
    /// 실제 프로젝트에서는 Microsoft.Extensions.DependencyInjection 권장
    /// </summary>
    public static class ServiceLocator
    {
        private static DatabaseService? _db;
        private static BarcodeService? _barcodeService;
        private static LogService? _log;
        private static BackupService? _backup;

        public static void Initialize()
        {
            _db = new DatabaseService();
            _barcodeService = new BarcodeService();
            _log = new LogService();
            _backup = new BackupService(_db);
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

        public static BarcodeService GetBarcodeService() => _barcodeService!;
        public static BackupService GetBackupService() => _backup!;
        public static LogService GetLogService() => _log!;
    }
}

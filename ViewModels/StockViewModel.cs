using System.Collections.ObjectModel;
using InventoryManager.Models;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;

namespace InventoryManager.ViewModels;

// ─── 재고 관리 ─────────────────────────────────────────────────────────────

public class StockViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepo;
    private readonly IStockRepository _stockRepo;
    private readonly LogService _log;

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<StockHistory> History { get; } = [];

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetProperty(ref _selectedProduct, value);
            if (value != null) _ = LoadHistoryAsync(value.Barcode);
        }
    }

    private int _adjustQuantity;
    public int AdjustQuantity
    {
        get => _adjustQuantity;
        set => SetProperty(ref _adjustQuantity, value);
    }

    private string _adjustRemark = string.Empty;
    public string AdjustRemark
    {
        get => _adjustRemark;
        set => SetProperty(ref _adjustRemark, value);
    }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand AdjustCommand { get; }

    public StockViewModel(IProductRepository productRepo, IStockRepository stockRepo, LogService log)
    {
        _productRepo = productRepo;
        _stockRepo = stockRepo;
        _log = log;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AdjustCommand = new AsyncRelayCommand(AdjustAsync,
            () => SelectedProduct != null && AdjustQuantity != 0);
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        var items = await _productRepo.GetAllAsync();
        Products.Clear();
        foreach (var p in items) Products.Add(p);
        IsBusy = false;
    }

    private async Task LoadHistoryAsync(string barcode)
    {
        var items = await _stockRepo.GetHistoryAsync(barcode);
        History.Clear();
        foreach (var h in items) History.Add(h);
    }

    private async Task AdjustAsync()
    {
        if (SelectedProduct == null) return;
        // SelectedProduct may change during async operation; capture barcode early
        var barcode = SelectedProduct.Barcode;
        try
        {
            // Treat AdjustQuantity as the absolute target quantity.
            var before = await _productRepo.GetCurrentQuantityAsync(barcode);
            var delta = AdjustQuantity - before; // e.g. before=100, AdjustQuantity=10 -> delta=-90

            await _stockRepo.AdjustStockAsync(barcode, delta, AdjustRemark);
            await _log.InfoAsync("Stock",
                $"재고 조정: {barcode} {delta:+#;-#} [{AdjustRemark}] (설정:{AdjustQuantity} 이전:{before})");

            StatusMessage = $"재고 조정 완료: 현재 수량이 {AdjustQuantity}로 설정되었습니다.";
            AdjustQuantity = 0;
            AdjustRemark = string.Empty;
            await LoadAsync();
            await LoadHistoryAsync(barcode);
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
        }
    }
}

// ─── 매출 관리 ─────────────────────────────────────────────────────────────

public class RevenueViewModel : ViewModelBase
{
    private readonly ISaleRepository _saleRepo;

    public ObservableCollection<SaleMaster> DailySales { get; } = [];
    public ObservableCollection<(string YM, decimal Total)> MonthlySales { get; } = [];

    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }

    private DateTime _toDate = DateTime.Today;
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    private int _selectedYear = DateTime.Today.Year;
    public int SelectedYear { get => _selectedYear; set => SetProperty(ref _selectedYear, value); }

    private decimal _totalRevenue;
    public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

    public AsyncRelayCommand LoadDailyCommand { get; }
    public AsyncRelayCommand LoadMonthlyCommand { get; }

    public RevenueViewModel(ISaleRepository saleRepo)
    {
        _saleRepo = saleRepo;
        LoadDailyCommand = new AsyncRelayCommand(LoadDailyAsync);
        LoadMonthlyCommand = new AsyncRelayCommand(LoadMonthlyAsync);
    }

    private async Task LoadDailyAsync()
    {
        IsBusy = true;
        var items = await _saleRepo.GetDailySalesAsync(FromDate, ToDate);
        DailySales.Clear();
        foreach (var s in items) DailySales.Add(s);
        TotalRevenue = DailySales.Sum(s => s.TotalAmount);
        IsBusy = false;
    }

    private async Task LoadMonthlyAsync()
    {
        IsBusy = true;
        var items = await _saleRepo.GetMonthlySalesAsync(SelectedYear);
        MonthlySales.Clear();
        foreach (var s in items) MonthlySales.Add(s);
        IsBusy = false;
    }
}

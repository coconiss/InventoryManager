using System.Collections.ObjectModel;
using InventoryManager.Models;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace InventoryManager.ViewModels;

// ─── 제품별 매출 행 ─────────────────────────────────────────────────────────
public class ProductSaleRow
{
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Qty { get; set; }
}

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
        var barcode = SelectedProduct.Barcode;
        try
        {
            var before = await _productRepo.GetCurrentQuantityAsync(barcode);
            var delta = AdjustQuantity - before;

            await _stockRepo.AdjustStockAsync(barcode, delta, AdjustRemark);
            await _log.InfoAsync("Stock",
                $"재고 조정: {barcode} {delta:+#;-#} [{AdjustRemark}] (설정:{AdjustQuantity} 이전:{before})");

            StatusMessage = $"재고 조정 완료: 현재 수량이 {AdjustQuantity}로 설정되었습니다.";
            AdjustQuantity = 0;
            AdjustRemark = string.Empty;
            await LoadAsync();
            await LoadHistoryAsync(barcode);
        }
        catch (Exception ex) { StatusMessage = $"오류: {ex.Message}"; }
    }
}

// ─── 매출 관리 ─────────────────────────────────────────────────────────────

public class RevenueViewModel : ViewModelBase
{
    private readonly ISaleRepository _saleRepo;

    public ObservableCollection<SaleMaster> DailySales { get; } = [];
    public ObservableCollection<(string YM, decimal Total)> MonthlySales { get; } = [];
    public ObservableCollection<ProductSaleRow> ProductSales { get; } = [];

    // ── 차트 데이터 ──────────────────────────────────────────────
    private ISeries[] _dailySeries = [];
    public ISeries[] DailySeries
    {
        get => _dailySeries;
        private set => SetProperty(ref _dailySeries, value);
    }

    private Axis[] _dailyXAxes = [];
    public Axis[] DailyXAxes
    {
        get => _dailyXAxes;
        private set => SetProperty(ref _dailyXAxes, value);
    }

    private ISeries[] _monthlySeries = [];
    public ISeries[] MonthlySeries
    {
        get => _monthlySeries;
        private set => SetProperty(ref _monthlySeries, value);
    }

    private Axis[] _monthlyXAxes = [];
    public Axis[] MonthlyXAxes
    {
        get => _monthlyXAxes;
        private set => SetProperty(ref _monthlyXAxes, value);
    }

    private ISeries[] _productSeries = [];
    public ISeries[] ProductSeries
    {
        get => _productSeries;
        private set => SetProperty(ref _productSeries, value);
    }

    // ── 날짜 조건 ────────────────────────────────────────────────
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
    public AsyncRelayCommand LoadProductSalesCommand { get; }

    public RevenueViewModel(ISaleRepository saleRepo)
    {
        _saleRepo = saleRepo;
        LoadDailyCommand = new AsyncRelayCommand(LoadDailyAsync);
        LoadMonthlyCommand = new AsyncRelayCommand(LoadMonthlyAsync);
        LoadProductSalesCommand = new AsyncRelayCommand(LoadProductSalesAsync);

        // 통합 조회 버튼용
        LoadCommand = new AsyncRelayCommand(async () =>
        {
            // 탭 인덱스에 따라 적절한 로드 실행
            if (SelectedTabIndex == 0) await LoadDailyAsync();
            else if (SelectedTabIndex == 1) await LoadMonthlyAsync();
            else await LoadProductSalesAsync();
        });
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }

    public AsyncRelayCommand LoadCommand { get; }

    private async Task LoadDailyAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _saleRepo.GetDailySalesAsync(FromDate, ToDate);
            DailySales.Clear();
            foreach (var s in items) DailySales.Add(s);
            TotalRevenue = DailySales.Sum(s => s.TotalAmount);

            // 차트 업데이트
            var values = DailySales.Select(s => (double)s.TotalAmount).ToArray();
            var labels = DailySales.Select(s => s.CreatedAt.ToString("MM/dd")).ToArray();

            DailySeries =
            [
                new ColumnSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(new SKColor(52, 152, 219)),
                    Name = ""
                }
            ];
            DailyXAxes =
            [
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = -45,
                    TextSize = 11
                }
            ];

            // 제품별도 같은 기간으로 갱신
            await LoadProductSalesAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task LoadMonthlyAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _saleRepo.GetMonthlySalesAsync(SelectedYear);
            MonthlySales.Clear();
            foreach (var s in items) MonthlySales.Add(s);

            var values = MonthlySales.Select(s => (double)s.Total).ToArray();
            var labels = MonthlySales.Select(s => s.YM).ToArray();

            MonthlySeries =
            [
                new ColumnSeries<double>
                {
                    Values = values,
                    Fill = new SolidColorPaint(new SKColor(39, 174, 96)),
                    Name = ""
                }
            ];
            MonthlyXAxes =
            [
                new Axis { Labels = labels, TextSize = 11 }
            ];
        }
        finally { IsBusy = false; }
    }

    private async Task LoadProductSalesAsync()
    {
        var items = await _saleRepo.GetProductSalesAsync(FromDate, ToDate);
        ProductSales.Clear();
        foreach (var (barcode, name, total, qty) in items)
        {
            ProductSales.Add(new ProductSaleRow
            {
                Barcode = barcode,
                Name = name,
                Total = total,
                Qty = qty
            });
        }

        // 상위 10개만 차트로 표시
        var top = ProductSales.Take(10).ToList();
        ProductSeries =
        [
            new ColumnSeries<double>
            {
                Values = top.Select(p => (double)p.Total).ToArray(),
                Fill = new SolidColorPaint(new SKColor(231, 76, 60)),
                Name = ""
            }
        ];

        // ProductSeries용 X축은 별도 프로퍼티로 관리 (뷰에서 static 레이블 사용)
        OnPropertyChanged(nameof(ProductSeries));
        var productXAxes = new Axis[]
        {
            new Axis
            {
                Labels = top.Select(p => p.Name.Length > 8 ? p.Name[..8] + "…" : p.Name).ToArray(),
                LabelsRotation = -30,
                TextSize = 10
            }
        };
        ProductXAxes = productXAxes;
    }

    private Axis[] _productXAxes = [];
    public Axis[] ProductXAxes
    {
        get => _productXAxes;
        private set => SetProperty(ref _productXAxes, value);
    }
}
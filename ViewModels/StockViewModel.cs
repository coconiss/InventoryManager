using System.Collections.ObjectModel;
using InventoryManager.Models;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;
using LiveChartsCore;
using LiveChartsCore.Measure;
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

    // SkiaSharp 한글 폰트 — Windows 맑은 고딕 우선, 없으면 기본 폰트
    private static readonly SKTypeface KoreanTypeface = LoadKoreanTypeface();

    private static SKTypeface LoadKoreanTypeface()
    {
        var candidates = new[]
        {
            @"C:\Windows\Fonts\malgun.ttf",
            @"C:\Windows\Fonts\malgunbd.ttf",
            @"C:\Windows\Fonts\NanumGothic.ttf",
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return SKTypeface.FromFile(path);
        }
        return SKTypeface.FromFamilyName("Malgun Gothic")
            ?? SKTypeface.FromFamilyName("나눔고딕")
            ?? SKTypeface.Default;
    }

    /// <summary>
    /// 한글 폰트가 적용된 SolidColorPaint 생성.
    /// TextSize를 명시적으로 설정해야 툴팁 등에서도 폰트가 적용됨.
    /// </summary>
    private static SolidColorPaint MakeKoreanPaint(SKColor color, float size = 11f) =>
        new SolidColorPaint(color)
        {
            SKTypeface = KoreanTypeface,
            SKFontStyle = new SKFontStyle(
                SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright)
        };

    private static SolidColorPaint LabelPaint(float size = 11f) =>
        MakeKoreanPaint(new SKColor(99, 110, 114), size);

    private static SolidColorPaint TooltipPaint(float size = 12f) =>
        MakeKoreanPaint(new SKColor(45, 52, 54), size);

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

    private Axis[] _dailyYAxes = [];
    public Axis[] DailyYAxes
    {
        get => _dailyYAxes;
        private set => SetProperty(ref _dailyYAxes, value);
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

    private Axis[] _monthlyYAxes = [];
    public Axis[] MonthlyYAxes
    {
        get => _monthlyYAxes;
        private set => SetProperty(ref _monthlyYAxes, value);
    }

    private ISeries[] _productSeries = [];
    public ISeries[] ProductSeries
    {
        get => _productSeries;
        private set => SetProperty(ref _productSeries, value);
    }

    private Axis[] _productXAxes = [];
    public Axis[] ProductXAxes
    {
        get => _productXAxes;
        private set => SetProperty(ref _productXAxes, value);
    }

    private Axis[] _productYAxes = [];
    public Axis[] ProductYAxes
    {
        get => _productYAxes;
        private set => SetProperty(ref _productYAxes, value);
    }

    // ── 날짜 조건 ────────────────────────────────────────────────
    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }

    private DateTime _toDate = DateTime.Today;
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    private decimal _totalRevenue;
    public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

    private int _selectedTabIndex;
    public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }

    public AsyncRelayCommand LoadDailyCommand { get; }
    public AsyncRelayCommand LoadMonthlyCommand { get; }
    public AsyncRelayCommand LoadProductSalesCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }

    public RevenueViewModel(ISaleRepository saleRepo)
    {
        _saleRepo = saleRepo;
        LoadDailyCommand = new AsyncRelayCommand(LoadDailyAsync);
        LoadMonthlyCommand = new AsyncRelayCommand(LoadMonthlyAsync);
        LoadProductSalesCommand = new AsyncRelayCommand(LoadProductSalesAsync);

        LoadCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true; // 조회 시작 시 로딩 인디케이터 켬
            try
            {
                await LoadDailyAsync();
                await LoadMonthlyAsync();
                await LoadProductSalesAsync();
            }
            finally
            {
                IsBusy = false; // 조회 완료 후 로딩 인디케이터 끔
            }
        });
    }

    /// <summary>
    /// Y축 레이블: 1만 미만은 원 단위 그대로, 이상은 소수점 1자리 만원 단위 표시.
    /// ex) 5000 → "5,000"  /  33200 → "3.3만"  /  100000 → "10.0만"
    /// </summary>
    private static string FormatYLabel(double v)
    {
        if (v >= 10000)
        {
            double man = v / 10000.0;
            // 소수점이 없으면 정수로, 있으면 1자리까지
            return man == Math.Floor(man)
                ? $"{man:N0}만"
                : $"{man:N1}만";
        }
        return $"{v:N0}";
    }

    private Axis[] MakeYAxis() =>
    [
        new Axis
        {
            Labeler = FormatYLabel,
            LabelsPaint = LabelPaint(),
            TextSize = 11
        }
    ];

    private async Task LoadDailyAsync()
    {
        // try/finally(IsBusy) 제거 및 중복 호출 제거
        var items = await _saleRepo.GetDailySalesAsync(FromDate, ToDate);
        DailySales.Clear();
        foreach (var s in items) DailySales.Add(s);
        TotalRevenue = DailySales.Sum(s => s.TotalAmount);

        var values = DailySales.Select(s => (double)s.TotalAmount).ToArray();
        var labels = DailySales.Select(s => s.CreatedAt.ToString("MM/dd")).ToArray();

        DailySeries =
        [
            new ColumnSeries<double>
        {
            Values = values,
            Fill = new SolidColorPaint(new SKColor(52, 152, 219)),
            Name = "일별 매출",
            TooltipLabelFormatter = p =>
            {
                int idx = (int)p.Context.Entity.MetaData!.EntityIndex;
                string label = idx < labels.Length ? labels[idx] : "";
                return $"{label} : {p.PrimaryValue:N0}원";
            },
            DataLabelsPaint = LabelPaint()
        }
        ];
        DailyXAxes =
        [
            new Axis
        {
            Labels = labels,
            LabelsRotation = -45,
            LabelsPaint = LabelPaint(),
            TextSize = 11
        }
        ];
        DailyYAxes = MakeYAxis();
    }

    private async Task LoadMonthlyAsync()
    {
        // try/finally(IsBusy) 제거
        int year = FromDate.Year;
        var items = await _saleRepo.GetMonthlySalesAsync(year);
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
            Name = "월별 매출",
            TooltipLabelFormatter = p =>
            {
                int idx = (int)p.Context.Entity.MetaData!.EntityIndex;
                string label = idx < labels.Length ? labels[idx] : "";
                return $"{label} : {p.PrimaryValue:N0}원";
            },
            DataLabelsPaint = LabelPaint()
        }
        ];
        MonthlyXAxes =
        [
            new Axis
        {
            Labels = labels,
            LabelsPaint = LabelPaint(),
            TextSize = 11
        }
        ];
        MonthlyYAxes = MakeYAxis();
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

        var top = ProductSales.Take(10).ToList();
        var productLabels = top.Select(p => p.Name.Length > 8 ? p.Name[..8] + "…" : p.Name).ToArray();

        ProductSeries =
        [
            new ColumnSeries<double>
            {
                Values = top.Select(p => (double)p.Total).ToArray(),
                Fill = new SolidColorPaint(new SKColor(231, 76, 60)),
                Name = "제품별 매출",
                TooltipLabelFormatter = p =>
                {
                    int idx = (int)p.Context.Entity.MetaData!.EntityIndex;
                    string label = idx < productLabels.Length ? productLabels[idx] : "";
                    return $"{label} : {p.PrimaryValue:N0}원";
                },
                DataLabelsPaint = LabelPaint()
            }
        ];

        ProductXAxes =
        [
            new Axis
            {
                Labels = productLabels,
                LabelsRotation = -30,
                LabelsPaint = LabelPaint(),
                TextSize = 10
            }
        ];
        ProductYAxes = MakeYAxis();

        OnPropertyChanged(nameof(ProductSeries));
    }
}
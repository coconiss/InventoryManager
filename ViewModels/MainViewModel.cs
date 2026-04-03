using InventoryManager.ViewModels.Base;
using InventoryManager.Views;
using System.Windows.Controls;

namespace InventoryManager.ViewModels;

/// <summary>
/// MainWindow의 ViewModel - 메뉴 네비게이션 및 CurrentView 관리
/// </summary>
public class MainViewModel : ViewModelBase
{
    private System.Windows.Controls.UserControl _currentView = new();
    public System.Windows.Controls.UserControl CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private string _activeMenu = "product";
    public string ActiveMenu
    {
        get => _activeMenu;
        set => SetProperty(ref _activeMenu, value);
    }

    // 뷰 캐시 (재생성 방지)
    private readonly Dictionary<string, System.Windows.Controls.UserControl> _viewCache = [];

    // Commands
    public RelayCommand NavigateProductCommand { get; }
    public RelayCommand NavigateStockCommand { get; }
    public RelayCommand NavigateSaleCommand { get; }
    public RelayCommand NavigateRevenueCommand { get; }
    public RelayCommand NavigateSettingsCommand { get; }

    public string AppVersion => "v1.0.0";
    public string ContactInfo => "문의: lsd9901@gmail.com";

    public MainViewModel()
    {
        NavigateProductCommand = new RelayCommand(() => Navigate("product"));
        NavigateStockCommand = new RelayCommand(() => Navigate("stock"));
        NavigateSaleCommand = new RelayCommand(() => Navigate("sale"));
        NavigateRevenueCommand = new RelayCommand(() => Navigate("revenue"));
        NavigateSettingsCommand = new RelayCommand(() => Navigate("settings"));

        // 시작화면
        Navigate("product");
    }

    private void Navigate(string key)
    {
        ActiveMenu = key;

        if (!_viewCache.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "product" => new ProductView(),
                "stock" => new StockView(),
                "sale" => new SaleView(),
                "revenue" => new RevenueView(),
                "settings" => new SettingsView(),
                _ => new ProductView()
            };
            _viewCache[key] = view;
        }

        CurrentView = view;
    }
}

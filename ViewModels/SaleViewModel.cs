using System.Collections.ObjectModel;
using InventoryManager.Models;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;

namespace InventoryManager.ViewModels;

public class CartItem : InventoryManager.ViewModels.Base.ViewModelBase
{
    private int _quantity;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity
    {
        get => _quantity;
        set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); }
    }
    public decimal LineTotal => Price * Quantity;
}

public class SaleViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepo;
    private readonly ISaleRepository _saleRepo;
    private readonly BarcodeService _barcodeService;
    private readonly LogService _log;

    public ObservableCollection<CartItem> CartItems { get; } = [];

    public int CartItemCount => CartItems.Count;

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value);
    }

    private CartItem? _selectedCartItem;
    public CartItem? SelectedCartItem
    {
        get => _selectedCartItem;
        set => SetProperty(ref _selectedCartItem, value);
    }

    private string _lastScanResult = string.Empty;
    public string LastScanResult
    {
        get => _lastScanResult;
        set => SetProperty(ref _lastScanResult, value);
    }

    // Commands
    public AsyncRelayCommand CheckoutCommand { get; }
    public RelayCommand IncreaseQtyCommand { get; }
    public RelayCommand DecreaseQtyCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand ClearCartCommand { get; }

    public SaleViewModel(
        IProductRepository productRepo,
        ISaleRepository saleRepo,
        BarcodeService barcodeService,
        LogService log)
    {
        _productRepo = productRepo;
        _saleRepo = saleRepo;
        _barcodeService = barcodeService;
        _log = log;

        // 바코드 스캔 이벤트 구독
        _barcodeService.BarcodeScanned += OnBarcodeScanned;

        CheckoutCommand = new AsyncRelayCommand(CheckoutAsync, () => CartItems.Count > 0);
        // Commands accept optional parameter (CartItem). If null, operate on SelectedCartItem.
        IncreaseQtyCommand = new RelayCommand(p => IncreaseQty(p as CartItem ?? SelectedCartItem));
        DecreaseQtyCommand = new RelayCommand(p => DecreaseQty(p as CartItem ?? SelectedCartItem));
        RemoveItemCommand = new RelayCommand(p => RemoveItem(p as CartItem ?? SelectedCartItem));
        ClearCartCommand = new RelayCommand(ClearCart);

        // Update CartItemCount when collection changes
        CartItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CartItemCount));
    }

    private async void OnBarcodeScanned(object? sender, string barcode)
    {
        await AddToCartAsync(barcode);
    }

    /// <summary>
    /// 바코드로 제품 조회 후 카트에 추가 또는 수량 증가
    /// </summary>
    public async Task AddToCartAsync(string barcode)
    {
        try
        {
            // 이미 카트에 있으면 수량 증가
            var existing = CartItems.FirstOrDefault(c => c.Barcode == barcode);
            if (existing != null)
            {
                // 재고 확인
                int stock = await _productRepo.GetCurrentQuantityAsync(barcode);
                if (existing.Quantity >= stock)
                {
                    LastScanResult = $"⚠️ 재고 부족: {existing.Name} (재고: {stock}개)";
                    return;
                }
                existing.Quantity++;
                RefreshCart();
                LastScanResult = $"✅ {existing.Name} 수량 증가 → {existing.Quantity}";
                return;
            }

            // 신규 추가
            var product = await _productRepo.GetByBarcodeAsync(barcode);
            if (product == null)
            {
                LastScanResult = $"❌ 등록되지 않은 바코드: {barcode}";
                return;
            }
            if (product.CurrentQuantity <= 0)
            {
                LastScanResult = $"⚠️ 재고 없음: {product.Name}";
                return;
            }

            CartItems.Add(new CartItem
            {
                Barcode = product.Barcode,
                Name = product.Name,
                Price = product.SalePrice,
                Quantity = 1
            });
            RefreshCart();
            LastScanResult = $"✅ [{product.Name}] 추가됨";
        }
        catch (Exception ex)
        {
            LastScanResult = $"오류: {ex.Message}";
        }
    }

    private async Task CheckoutAsync()
    {
        IsBusy = true;
        try
        {
            var details = CartItems.Select(c => new SaleDetail
            {
                Barcode = c.Barcode,
                ProductName = c.Name,
                Quantity = c.Quantity,
                Price = c.Price
            }).ToList();

            long saleId = await _saleRepo.ProcessSaleAsync(details);
            await _log.InfoAsync("Sale", $"판매 완료 #{saleId} 금액:{TotalAmount:N0}원");

            StatusMessage = $"✅ 결제 완료 #{saleId} | 합계: {TotalAmount:N0}원";
            ClearCart();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = $"⚠️ {ex.Message}";
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync("Sale", "결제 처리 실패", ex);
            StatusMessage = $"오류: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void IncreaseQty(CartItem? item)
    {
        if (item == null) return;
        item.Quantity++;
        RefreshCart();
    }

    private void DecreaseQty(CartItem? item)
    {
        if (item == null) return;
        if (item.Quantity <= 1)
            CartItems.Remove(item);
        else
            item.Quantity--;
        RefreshCart();
    }

    private void RemoveItem(CartItem? item)
    {
        if (item != null)
            CartItems.Remove(item);
        RefreshCart();
    }

    private void ClearCart()
    {
        CartItems.Clear();
        RefreshCart();
    }

    private void RefreshCart()
    {
        TotalAmount = CartItems.Sum(c => c.LineTotal);
        // ObservableCollection은 아이템 속성 변경을 감지 못하므로 강제 갱신
        var temp = CartItems.ToList();
        CartItems.Clear();
        foreach (var item in temp) CartItems.Add(item);
        TotalAmount = CartItems.Sum(c => c.LineTotal);
    }
}

using System.Collections.ObjectModel;
using InventoryManager.Models;
using InventoryManager.Repositories;
using InventoryManager.Services;
using InventoryManager.ViewModels.Base;

namespace InventoryManager.ViewModels;

public class ProductViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepo;
    private readonly LogService _log;

    // ── 목록 ────────────────────────────────────────────────────
    public ObservableCollection<Product> Products { get; } = [];

    private string _searchKeyword = string.Empty;
    public string SearchKeyword
    {
        get => _searchKeyword;
        set { SetProperty(ref _searchKeyword, value); }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            SetProperty(ref _selectedProduct, value);
            if (value != null) LoadEditForm(value);
        }
    }

    // ── 등록/수정 폼 ─────────────────────────────────────────────
    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    private string _formBarcode = string.Empty;
    public string FormBarcode
    {
        get => _formBarcode;
        set => SetProperty(ref _formBarcode, value);
    }

    private string _formName = string.Empty;
    public string FormName
    {
        get => _formName;
        set => SetProperty(ref _formName, value);
    }

    private decimal _formCostPrice;
    public decimal FormCostPrice
    {
        get => _formCostPrice;
        set => SetProperty(ref _formCostPrice, value);
    }

    private decimal _formSalePrice;
    public decimal FormSalePrice
    {
        get => _formSalePrice;
        set => SetProperty(ref _formSalePrice, value);
    }

    private int _formQuantity;
    public int FormQuantity
    {
        get => _formQuantity;
        set => SetProperty(ref _formQuantity, value);
    }

    private string _formRemark = string.Empty;
    public string FormRemark
    {
        get => _formRemark;
        set => SetProperty(ref _formRemark, value);
    }

    // ── Commands ─────────────────────────────────────────────────
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteSelectedCommand { get; }
    public RelayCommand NewFormCommand { get; }
    public RelayCommand CancelCommand { get; }

    public ProductViewModel(IProductRepository productRepo, LogService log)
    {
        _productRepo = productRepo;
        _log = log;

        SearchCommand = new AsyncRelayCommand(LoadProductsAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        NewFormCommand = new RelayCommand(ClearForm);
        CancelCommand = new RelayCommand(ClearForm);
    }

    public async Task LoadProductsAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _productRepo.GetAllAsync(
                string.IsNullOrWhiteSpace(SearchKeyword) ? null : SearchKeyword);
            Products.Clear();
            foreach (var p in items) Products.Add(p);
            StatusMessage = $"총 {Products.Count}개 제품";
        }
        finally { IsBusy = false; }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormBarcode) || string.IsNullOrWhiteSpace(FormName))
        {
            StatusMessage = "바코드와 제품명은 필수입니다.";
            return;
        }

        IsBusy = true;
        try
        {
            var product = new Product
            {
                Barcode = FormBarcode.Trim(),
                Name = FormName.Trim(),
                CostPrice = FormCostPrice,
                SalePrice = FormSalePrice,
                Remark = FormRemark
            };

            if (IsEditMode)
            {
                await _productRepo.UpdateAsync(product);
                await _log.InfoAsync("Product", $"제품 수정: {product.Barcode} [{product.Name}]");
                StatusMessage = "제품이 수정되었습니다.";
            }
            else
            {
                if (FormQuantity < 0)
                {
                    StatusMessage = "입고수량은 0 이상이어야 합니다.";
                    return;
                }
                if (await _productRepo.ExistsAsync(product.Barcode))
                {
                    StatusMessage = "이미 존재하는 바코드입니다.";
                    return;
                }
                await _productRepo.AddAsync(product, FormQuantity);
                await _log.InfoAsync("Product", $"제품 등록: {product.Barcode} [{product.Name}] 수량:{FormQuantity}");
                StatusMessage = "제품이 등록되었습니다.";
            }

            ClearForm();
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync("Product", "저장 실패", ex);
            StatusMessage = $"오류: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteSelectedAsync()
    {
        var targets = Products.Where(p => p.IsActive).ToList(); // 실제로는 체크박스 선택 목록
        if (SelectedProduct == null) return;

        bool hasSales = await _productRepo.HasSalesHistoryAsync(SelectedProduct.Barcode);
        if (hasSales)
        {
            // 판매 이력 있으면 Soft Delete
            await _productRepo.SoftDeleteAsync(SelectedProduct.Barcode);
            await _log.WarnAsync("Product", $"제품 비활성화(판매이력): {SelectedProduct.Barcode}");
            StatusMessage = "판매 이력이 있어 비활성화 처리되었습니다.";
        }
        else
        {
            await _productRepo.SoftDeleteAsync(SelectedProduct.Barcode);
            await _log.InfoAsync("Product", $"제품 삭제: {SelectedProduct.Barcode}");
            StatusMessage = "삭제되었습니다.";
        }
        await LoadProductsAsync();
    }

    private void LoadEditForm(Product p)
    {
        IsEditMode = true;
        FormBarcode = p.Barcode;
        FormName = p.Name;
        FormCostPrice = p.CostPrice;
        FormSalePrice = p.SalePrice;
        FormQuantity = p.CurrentQuantity;
        FormRemark = p.Remark;
    }

    private void ClearForm()
    {
        IsEditMode = false;
        SelectedProduct = null;
        FormBarcode = FormName = FormRemark = string.Empty;
        FormCostPrice = FormSalePrice = 0;
        FormQuantity = 0;
    }
}

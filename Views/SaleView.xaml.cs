using InventoryManager.Helpers;
using InventoryManager.Services;
using InventoryManager.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace InventoryManager.Views;

public partial class SaleView : System.Windows.Controls.UserControl
{
    private readonly BarcodeService _barcodeService;
    private readonly SaleViewModel _vm;

    public SaleView()
    {
        InitializeComponent();
        _barcodeService = ServiceLocator.GetBarcodeService();
        _vm = ServiceLocator.GetSaleViewModel();
        DataContext = _vm;
    }

    /// <summary>
    /// USB HID 바코드 스캐너 입력 → BarcodeService로 전달
    /// </summary>
    private void Grid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        _barcodeService.OnTextInput(e.Text);
        e.Handled = true; // TextBox 기본 입력 차단
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _barcodeService.OnEnterKey();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}

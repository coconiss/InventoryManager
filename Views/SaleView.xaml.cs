using InventoryManager.Helpers;
using InventoryManager.Services;
using InventoryManager.ViewModels;
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

    private void Grid_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        _barcodeService.OnTextInput(e.Text);
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _barcodeService.OnEnterKey();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    private void LoadHistory_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new SaleHistoryDialog(ServiceLocator.GetSaleRepository())
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true
            && dialog.SelectedSaleId.HasValue
            && dialog.SelectedDetails != null)
        {
            _vm.LoadFromHistory(dialog.SelectedSaleId.Value, dialog.SelectedDetails);
        }
    }
}
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

        // 뷰 로드 후 수동입력 박스에 기본 포커스
        Loaded += (_, _) => ManualBarcodeBox.Focus();
    }

    private void Grid_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // 수동 입력 박스에 포커스가 있을 때는 HID 버퍼로 보내지 않음
        if (ManualBarcodeBox.IsFocused) return;
        _barcodeService.OnTextInput(e.Text);
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !ManualBarcodeBox.IsFocused)
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

    // 보류 버튼의 Click 이벤트 — Command(HoldCartCommand)는 ViewModel에서 처리,
    // 여기서는 추가 UI 작업 없이 단순 pass-through
    private void HoldCart_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // HoldCartCommand가 실행된 후 수동입력 박스로 포커스 복원
        Dispatcher.InvokeAsync(() => ManualBarcodeBox.Focus());
    }

    private void LoadHold_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new HoldCartDialog(ServiceLocator.GetHoldRepository())
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true
            && dialog.SelectedCartId.HasValue
            && dialog.SelectedItems != null)
        {
            _vm.LoadFromHold(dialog.SelectedCartId.Value, dialog.SelectedItems);
        }

        ManualBarcodeBox.Focus();
    }
}
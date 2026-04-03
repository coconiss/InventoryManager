using InventoryManager.Models;
using InventoryManager.Repositories;
using System.Windows;

namespace InventoryManager.Views;

public partial class SaleHistoryDialog : Window
{
    private readonly ISaleRepository _saleRepo;

    public List<SaleDetail>? SelectedDetails { get; private set; }
    public long? SelectedSaleId { get; private set; }

    public SaleHistoryDialog(ISaleRepository saleRepo)
    {
        InitializeComponent();
        _saleRepo = saleRepo;

        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
        ToDatePicker.SelectedDate = DateTime.Today;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-7);
        var to = ToDatePicker.SelectedDate ?? DateTime.Today;

        var sales = await _saleRepo.GetIndividualSalesAsync(from, to);
        SaleGrid.ItemsSource = sales;
        DetailGrid.ItemsSource = null;
        LoadButton.IsEnabled = false;
        DeleteSaleButton.IsEnabled = false;
    }

    private async void SaleGrid_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SaleGrid.SelectedItem is SaleMaster selected)
        {
            var details = await _saleRepo.GetSaleDetailsAsync(selected.Id);
            DetailGrid.ItemsSource = details;
            LoadButton.IsEnabled = details.Count > 0;
            DeleteSaleButton.IsEnabled = true;
        }
        else
        {
            DetailGrid.ItemsSource = null;
            LoadButton.IsEnabled = false;
            DeleteSaleButton.IsEnabled = false;
        }
    }

    private async void DeleteSale_Click(object sender, RoutedEventArgs e)
    {
        if (SaleGrid.SelectedItem is not SaleMaster selected) return;

        var result = System.Windows.MessageBox.Show(
            $"주문 #{selected.Id} ({selected.CreatedAt:yyyy-MM-dd HH:mm}) 을 삭제하시겠습니까?\n재고가 복원됩니다.",
            "주문 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _saleRepo.DeleteSaleAsync(selected.Id);

            // 목록 새로고침
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-7);
            var to = ToDatePicker.SelectedDate ?? DateTime.Today;
            SaleGrid.ItemsSource = await _saleRepo.GetIndividualSalesAsync(from, to);
            DetailGrid.ItemsSource = null;
            LoadButton.IsEnabled = false;
            DeleteSaleButton.IsEnabled = false;

            System.Windows.MessageBox.Show($"주문 #{selected.Id}이 삭제되었습니다.", "삭제 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"삭제 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        if (SaleGrid.SelectedItem is not SaleMaster selected) return;
        if (DetailGrid.ItemsSource is not List<SaleDetail> details) return;

        SelectedSaleId = selected.Id;
        SelectedDetails = details;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
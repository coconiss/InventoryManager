using InventoryManager.Models;
using InventoryManager.Repositories;
using System.Windows;

namespace InventoryManager.Views;

public partial class HoldCartDialog : Window
{
    private readonly IHoldRepository _holdRepo;

    public List<SaleDetail>? SelectedItems { get; private set; }
    public long? SelectedCartId { get; private set; }

    public HoldCartDialog(IHoldRepository holdRepo)
    {
        InitializeComponent();
        _holdRepo = holdRepo;
        Loaded += async (_, _) => await RefreshListAsync();
    }

    private async System.Threading.Tasks.Task RefreshListAsync()
    {
        HoldGrid.ItemsSource = await _holdRepo.GetAllAsync();
        DetailGrid.ItemsSource = null;
        LoadButton.IsEnabled = false;
        DeleteHoldButton.IsEnabled = false;
    }

    private async void HoldGrid_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (HoldGrid.SelectedItem is HeldCart selected)
        {
            var items = await _holdRepo.GetItemsAsync(selected.Id);
            DetailGrid.ItemsSource = items;
            LoadButton.IsEnabled = items.Count > 0;
            DeleteHoldButton.IsEnabled = true;
        }
        else
        {
            DetailGrid.ItemsSource = null;
            LoadButton.IsEnabled = false;
            DeleteHoldButton.IsEnabled = false;
        }
    }

    private async void DeleteHold_Click(object sender, RoutedEventArgs e)
    {
        if (HoldGrid.SelectedItem is not HeldCart selected) return;

        var result = System.Windows.MessageBox.Show(
            $"보류 카트 #{selected.Id}를 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _holdRepo.DeleteAsync(selected.Id);
            await RefreshListAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"삭제 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        if (HoldGrid.SelectedItem is not HeldCart selected) return;

        var items = await _holdRepo.GetItemsAsync(selected.Id);
        SelectedCartId = selected.Id;
        SelectedItems = items;

        // 불러온 카트는 보류 목록에서 삭제
        await _holdRepo.DeleteAsync(selected.Id);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
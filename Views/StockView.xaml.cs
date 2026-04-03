using InventoryManager.Helpers;
using InventoryManager.ViewModels;
using System.Windows.Controls;

namespace InventoryManager.Views;

public partial class StockView : System.Windows.Controls.UserControl
{
    public StockView()
    {
        InitializeComponent();
        var vm = ServiceLocator.GetStockViewModel();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }
}

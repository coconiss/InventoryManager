using InventoryManager.Helpers;
using InventoryManager.ViewModels;
using System.Windows.Controls;

namespace InventoryManager.Views;

public partial class ProductView : System.Windows.Controls.UserControl
{
    public ProductView()
    {
        InitializeComponent();
        var vm = ServiceLocator.GetProductViewModel();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadProductsAsync();
    }
}

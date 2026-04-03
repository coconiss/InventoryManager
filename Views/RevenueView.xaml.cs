using InventoryManager.Helpers;
using System.Windows.Controls;

namespace InventoryManager.Views;

public partial class RevenueView : System.Windows.Controls.UserControl
{
    public RevenueView()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRevenueViewModel();
    }
}

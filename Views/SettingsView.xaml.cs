using InventoryManager.Helpers;
using InventoryManager.Services;
using InventoryManager.ViewModels;
using InventoryManager.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Forms;

namespace InventoryManager.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(
            ServiceLocator.GetBarcodeService(),
            ServiceLocator.GetBackupService());
    }
}

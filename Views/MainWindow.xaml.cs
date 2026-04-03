using InventoryManager.Helpers;
using InventoryManager.ViewModels;
using System.Windows;

namespace InventoryManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}

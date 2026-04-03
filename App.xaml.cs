using InventoryManager.Helpers;
using System.Windows;

namespace InventoryManager;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 전역 예외 처리
        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(
                $"예기치 않은 오류가 발생했습니다.\n\n{ex.Exception.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // DB 초기화 + 서비스 싱글턴 생성
        ServiceLocator.Initialize();
    }
}

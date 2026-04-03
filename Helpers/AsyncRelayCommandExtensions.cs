using InventoryManager.ViewModels.Base;

namespace InventoryManager.Helpers;

/// <summary>
/// AsyncRelayCommand를 Loaded 이벤트 등에서 await 가능하도록 확장
/// </summary>
public static class AsyncRelayCommandExtensions
{
    public static Task ExecuteAsync(this AsyncRelayCommand cmd, object? param)
    {
        cmd.Execute(param);
        return Task.CompletedTask;
    }
}

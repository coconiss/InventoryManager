using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InventoryManager.Helpers;

/// <summary>
/// IsEditMode=True → IsEnabled=False (바코드 필드 수정 불가)
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

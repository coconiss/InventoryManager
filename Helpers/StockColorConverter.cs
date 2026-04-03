using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace InventoryManager.Helpers;

/// <summary>
/// 재고 수량에 따라 색을 반환하는 컨버터
/// 설정에서 LowStockThreshold를 변경하면 전역으로 반영됨
/// </summary>
public class StockColorConverter : IValueConverter
{
    // 전역 임계값
    public static int LowStockThreshold { get; set; } = 5;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int qty)
        {
            return qty <= LowStockThreshold
                ? System.Windows.Media.Brushes.OrangeRed
                : System.Windows.Media.Brushes.Transparent;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

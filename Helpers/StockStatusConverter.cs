using System;
using System.Globalization;
using System.Windows.Data;

namespace InventoryManager.Helpers;

public class StockStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int qty)
        {
            if (qty <= 0) return "품절";
            if (qty <= StockColorConverter.LowStockThreshold) return "주의";
            return "정상";
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

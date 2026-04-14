using InventoryManager.Helpers;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Windows;

namespace InventoryManager.Views;

public partial class RevenueView : System.Windows.Controls.UserControl
{
    public RevenueView()
    {
        InitializeComponent();
        DataContext = ServiceLocator.GetRevenueViewModel();

        Loaded += (_, _) => ApplyKoreanTooltipFont();
    }

    private void ApplyKoreanTooltipFont()
    {
        var typeface = LoadKoreanTypeface();
        var paint = new SolidColorPaint(new SKColor(45, 52, 54)) { SKTypeface = typeface };

        // 이름으로 차트 컨트롤을 찾아 툴팁 폰트 일괄 적용
        foreach (var chart in FindVisualChildren<CartesianChart>(this))
        {
            chart.TooltipTextPaint = paint;
        }
    }

    private static SKTypeface LoadKoreanTypeface()
    {
        var candidates = new[]
        {
            @"C:\Windows\Fonts\malgun.ttf",
            @"C:\Windows\Fonts\malgunbd.ttf",
            @"C:\Windows\Fonts\NanumGothic.ttf",
        };
        foreach (var path in candidates)
        {
            if (System.IO.File.Exists(path))
                return SKTypeface.FromFile(path);
        }
        return SKTypeface.FromFamilyName("Malgun Gothic")
            ?? SKTypeface.Default;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var grandchild in FindVisualChildren<T>(child))
                yield return grandchild;
        }
    }
}
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AnalyzerHelper.Converters
{
    /// <summary>True -> Visible, False -> Collapsed.</summary>
    public sealed class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>True -> Collapsed, False -> Visible.</summary>
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v != Visibility.Visible;
    }

    /// <summary>True -> "✔ Fixed", False -> "Pending".</summary>
    public sealed class BoolToFixedTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? "✔ Fixed" : "Pending";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string s && s.Contains("Fixed");
    }

    /// <summary>True -> SemiBold (folder names), False -> Normal (file names).</summary>
    public sealed class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? FontWeights.SemiBold : FontWeights.Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is FontWeight fw && fw == FontWeights.SemiBold;
    }
}

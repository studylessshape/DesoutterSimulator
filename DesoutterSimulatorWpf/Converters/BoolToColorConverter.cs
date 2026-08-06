using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DesoutterSimulatorWpf.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Colors.Green);
            return new SolidColorBrush(Colors.Red);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool b) return "";
            // ConverterParameter: "connect"=未连接/已连接，其他=未启动/已启动
            return (parameter as string) == "connect"
                ? (b ? "已连接" : "未连接")
                : (b ? "已启动" : "未启动");
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
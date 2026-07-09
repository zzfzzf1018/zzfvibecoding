
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EtfTool.Wpf.Converters
{
    public class ChangeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal change)
            {
                return change >= 0 ? Brushes.Red : Brushes.Green;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

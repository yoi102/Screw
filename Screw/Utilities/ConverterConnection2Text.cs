using System;
using System.Globalization;
using System.Windows.Data;

namespace Screw.Utilities
{
    public class ConverterConnection2Text : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string txt;
            bool val = (bool)value;
            if (val == true)
            {
                txt = "已就绪";
            }
            else
            {
                txt = "未就绪";
            }

            return txt;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterConnection2Text] Cannot Convert Back.");
        }
    }
}

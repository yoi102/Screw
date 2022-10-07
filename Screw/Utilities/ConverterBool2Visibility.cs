using System;
using System.Globalization;
using System.Windows.Data;

namespace Screw.Utilities
{
    public class ConverterBool2Visibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string visibility;
            bool val = (bool)value;
            if (!val)
            {
                visibility = "Visible";

            }
            else
            {
                visibility = "Hidden";
            }

            return visibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterBool2VisibilityInv] Cannot Convert Back.");
        }
    }
}

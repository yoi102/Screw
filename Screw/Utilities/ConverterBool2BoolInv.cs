using System;
using System.Globalization;
using System.Windows.Data;

namespace Screw.Utilities
{
    public class ConverterBool2BoolInv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invbool;
            bool val = (bool)value;
            if (val)
            {
                invbool = false;

            }
            else
            {
                invbool = true;
            }

            return invbool;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterBool2VisibilityInv] Cannot Convert Back.");
        }
    }
}

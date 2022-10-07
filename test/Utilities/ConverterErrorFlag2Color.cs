using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace test.Utilities
{
    public class ConverterErrorFlag2Color : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string color;
            bool val = (bool)value;
            if (val)
            {
                color = "Red";
            }
            else
            {
                color = "White";
                //color = "Silver";
                //color = "Gray";
            }

            return color;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterErrorFlag2Color] Cannot Convert Back.");
        }
    }
}

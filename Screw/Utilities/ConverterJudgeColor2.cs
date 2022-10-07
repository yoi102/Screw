using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Screw.Utilities
{
    class ConverterJudgeColor2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string color;
            string val = (string)value;
            switch (val)
            {
                case "OK":
                    color = "Blue";
                    break;
                case "NG":
                    color = "Red";
                    break;
                case "NA":
                    color = "Silver";
                    break;
                default:
                    color = "Yellow";
                    break;
            }

            return color;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterConnection2Color] Cannot Convert Back.");
        }

    }
}

using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Screw.Utilities
{
    class ConverterRunStatus2Color : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string color;
            StatusType val = (StatusType)value;
            switch (val)
            {
                case StatusType.wait:
                    color = "Black";
                    break;
                case StatusType.running:
                    color = "GreenYellow";
                    break;
                case StatusType.end:
                    color = "Blue";
                    break;
                case StatusType.error:
                    color = "Red";
                    break;
                default: color = "Gray";break;
            }

            return color;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterConnection2Color] Cannot Convert Back.");
        }
    }



}

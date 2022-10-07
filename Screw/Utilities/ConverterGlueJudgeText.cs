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
    class ConverterGreaseJudgeText : IValueConverter
    {


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string color;
            JudgeType val = (JudgeType)value;
            switch (val)
            {
                case JudgeType.OK:
                    color = "OK";
                    break;
                case JudgeType.NG:
                    color = "少";
                    break;
                case JudgeType.NG2:
                    color = "多";
                    break;
                case JudgeType.NA:
                    color = "NA";
                    break;
                case JudgeType.ERROR:
                    color = "ERROR";
                    break;
                default:
                    color = "White";
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

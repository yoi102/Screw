using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Screw.Utilities
{
    class ConverterJudgeText2 : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string txt;
            string val = (string)value;

            switch (val)
            {
                case "OK":
                    txt = "压轴_OK";
                    break;
                case "NG":
                    txt = "压轴_NG";
                    break;
                case "NA":
                    txt = "==";
                    break;
                default:
                    txt = "!";
                    break;
            }

            return txt;
        }



        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("[ConverterConnection2Color] Cannot Convert Back.");
        }
    }
}


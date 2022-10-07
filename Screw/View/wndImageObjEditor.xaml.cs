using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Screw.View
{
    /// <summary>
    /// wndImageObjEditor.xaml 的交互逻辑
    /// </summary>
    public partial class wndImageObjEditor : Window
    {
        private static wndImageObjEditor staticInstance = null;

        public wndImageObjEditor()
        {
            InitializeComponent();
        }

        public static wndImageObjEditor GetInstance()
        {
            if (staticInstance == null)
            {
                staticInstance = new wndImageObjEditor();
            }

            return staticInstance;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            staticInstance = null;
            
        }
    }
}

using Screw.Model;
using Screw.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// wndAITag.xaml 的交互逻辑
    /// </summary>
    public partial class wndAITag : Window
    {
        //AITag AiTag;
        //public wndAITag()
        //{
        //    InitializeComponent();
        //    AiTag = new AITag(this);//需要这个
        //    this.DataContext = AiTag;


        //    //AiTag.FocusLastItem += AutoScroll;


        //}

        protected override void OnClosing(CancelEventArgs e)//防止关闭后，再启动时报错
        {
            e.Cancel = true;  // cancels the window close  撤销关闭
            this.Hide();      // Programmatically hides the window  隐藏当前窗口
        }




        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)//关闭时
        //{
        //    // hold windows closing
        //    e.Cancel = true;
        //    // do some works
        //    viewModel.TerminatePredictors();

        //    //resume exitting the application  
        //    Application.Current.Shutdown();//关闭所有窗口了
        //}




        //private void AutoScroll()
        //{
        //    StatusList.ScrollIntoView(StatusList.Items[StatusList.Items.Count - 1]);
        //}










    }
}

using SaGlue.ViewModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SaGlue
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModelMain viewModel;

        public MainWindow()
        {
            InitializeComponent();
            // set datacontext
            viewModel = new ViewModelMain(this);
            this.DataContext = viewModel;

        }
        /// <summary>
        /// 关闭方法一
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("确认要关闭？\n(っ °Д °;)っ\n_〆(´Д｀ )", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
            {
                e.Cancel = true;
                //Application.Current.Shutdown();      //关闭所有的窗体并退出程序
                //viewModel.AiTag.Close();
                //Application.Current.Shutdown();
                //MessageBox.Show("cc");
            }
            else
            {
                Application.Current.Shutdown();//关闭app

            }


        }
        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    // hold windows closing
        //    e.Cancel = true;
        //    // do some works
        //    viewModel.TerminatePredictors();

        //    //resume exitting the application  
        //    Application.Current.Shutdown();
        //}

        ///关闭方法二
        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    MessageBoxResult result = MessageBox.Show("确定退出程序？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        //    if (result == MessageBoxResult.No)
        //    {
        //        e.Cancel = true;
        //    }
        //}
        ///方法三
        //private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        //{
        //    MessageBoxResult result = MessageBox.Show("你要退出本程序？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        //    if (result == MessageBoxResult.No)
        //    {
        //        e.Cancel = true;
        //    }

        //}



    }




}


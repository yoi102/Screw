using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Screw
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    
        public partial class App : Application
        {
            /// <summary>
            /// 设置程序单例运行
            /// </summary>
            private static Mutex mutex;
            public App()
            {
                this.Startup += new StartupEventHandler(App_Startup);
            }

            void App_Startup(object sender, StartupEventArgs e)
            {
                mutex = new Mutex(true, "Screw", out bool ret);//Screw程序名字==》这里的<Application x:Class="Screw.App"
            if (!ret)
                {
                    MessageBox.Show("程序已经打开");
                    Environment.Exit(0);
                }
            }
     }
    
}

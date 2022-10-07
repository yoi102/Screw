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
using test.ViewModel;

namespace test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            // set datacontext
            viewModel = new MainViewModel(this);

            this.DataContext = viewModel;

        }

      
        private void Button_Click(object sender, RoutedEventArgs e)
        {

            Point p2 = new Point(100, 50);//
            double x;
            double y;


            Point p1 = new Point(7.98, 240.56);//原点
            Point p3 = new Point(8.21, 222.31);//1号的2点
            double angle = Math.Atan2( p3.Y - p1.Y,p3.X - p1.X);  //这个不用判断第几象限反过来，且x相反
            //double angle = Math.Atan2(-1,1);  //这个不用判断第几象限反过来，且x相反
            //0.0126020725625517弧度电缸夹角
            double angle2 = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);  //这个不用判断第几象限
            double theta = angle * (180 / Math.PI);   //角度 -36.86989764584402，即360 - 36.86989764584402 = 323.13010235415598
            double m = Math.PI / 2;//90°
            double mm = Math.Sqrt(Math.Pow(p1.X - p3.X, 2) + Math.Pow(p1.Y - p3.Y, 2));


            ushort qqq = 4 | 0b1111111111;
            //Console.WriteLine("Angle: " + angle + "\n" + (angle2 - angle) + "\nTheta: " + theta);
            //qwe.Text = qqq.ToString();
            ////qwe.Text = angle.ToString();
            //asd.Text = theta.ToString();


            MessageBox.Show("Angle: " + angle + "\n" + (angle2 - angle) + "\nTheta: " + theta);
            double n = 0;//治具两个圆的角度

            x = ((p2.X - p1.X) * Math.Cos(angle + n - m)) + ((p2.Y - p1.Y) * Math.Sin(angle + n - m));//减去90°，没错就是加上负的90°。
            y = ((p2.Y - p1.Y) * Math.Cos(angle + n - m)) - ((p2.X - p1.X) * Math.Sin(angle + n - m));//

            double XR = 0;//X轴比例
            double YR = 0;//Y轴比例
            double X = x * XR;//最后的X要乘以比例
            double Y = y * YR;//最后的Y要乘以比例


            //MessageBox.Show("x: " + x + "\ny: " + y + "\npi/2: " + (Math.PI / 2));
            //MessageBox.Show("Angle: " + angle + "\n" + (angle2 - angle) + "\nTheta: " + theta);


            //double X = ((p2.X - p1.X) * Math.Sin(angle)) - ((p2.Y - p1.Y) * Math.Cos(angle));//这里反过来的
            //double Y = ((p2.Y - p1.Y) * Math.Sin(angle)) + ((p2.X - p1.X) * Math.Cos(angle));


            //MessageBox.Show("X: " + X + "\nY: " + Y);

            //治具1
            //        X: 529.393751022411 Y: 1436.77413474796
            //        1X: 1756.06867394845 Y: 1439.54424164032
            //夹角 0.00225822027646608            XR = （2438 - 1436.33981094325 ）/（14.82*100）     

            //治具2
            //        X: 522.065361002824 Y: 1444.90209855505
            //        1X: 1749.13332696167 Y: 1452.13110484565
            //夹角 0.00589121618358112
            //相差 0.00363299590711504


            //治具3
            //        X: 513.612158962336 Y: 1434.78572511694
            //        1X: 1740.51682516897 Y: 1444.62483402117
            //夹角 0.00801928483943207
            //相差 0.00576106456296599

            //治具4
            //        X: 516.20144954687 Y: 1436.18544364565
            //        1X: 1743.05393503798 Y: 1447.10875799426
            //夹角 0.00890329183904573
            //相差 0.00664507156257965

            //治具5
            //        X: 520.307627039844 Y: 1441.82334139198
            //        1X: 1746.95898297811 Y: 1449.11375339723
            //夹角 0.00594327486061151
            //相差 0.00368505458414543

            //治具6
            //        X: 519.354427137901 Y: 1442.16299969733
            //        1X: 1746.27693702074 Y: 1450.473864978
            //夹角 0.00677364552871626
            //相差 0.00451542525225018
        }
    }
}

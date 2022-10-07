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

namespace Screw.View
{
    /// <summary>
    /// uclImage.xaml 的交互逻辑
    /// </summary>
    public partial class uclImage : UserControl
    {
        public uclImage()
        {
            InitializeComponent();
            this.SizeChanged += new System.Windows.SizeChangedEventHandler(PageCameraResized);

        }












        public ImageSource ImageSource //自定义属性
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty;//依赖项属性
        static uclImage()
        {
            var metadata = new FrameworkPropertyMetadata((ImageSource)null);
            ImageSourceProperty = DependencyProperty.RegisterAttached("ImageSource", typeof(ImageSource), typeof(uclImage), metadata);
        }
        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            double x = e.GetPosition((IInputElement)e.Source).X;
            double y = e.GetPosition((IInputElement)e.Source).Y;
            Image imageControl = (Image)(IInputElement)e.Source;
            double xRatio = ImageSource.Width / imageControl.ActualWidth;
            double yRatio = ImageSource.Height / imageControl.ActualHeight;
            Path_X.Text = ((x * xRatio)).ToString();
            Path_Y.Text = ((y * yRatio)).ToString();

            //Path_X.Text = ((int)e.GetPosition((IInputElement)e.Source).X).ToString();
            //Path_Y.Text = e.GetPosition((IInputElement)e.Source).Y.ToString();

        }



        /* -----------窗口大小变化响应------------ */
        private void PageCameraResized(object sender, System.EventArgs e)
        {
            var group = workspace.FindResource("Imageview") as TransformGroup;
            var scale = group.Children[0] as ScaleTransform;
            var move = group.Children[1] as TranslateTransform;
            move.X = 0;
            move.Y = 0;
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
        /* -------------图像缩放处理--------------- */
        // img为可视区域，image为图像
        private bool mouseDown;
        private Point position;
        private void ImgMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BackFrame.Cursor = Cursors.ScrollAll;
            var img = sender as ContentControl;
            if (img == null) { return; }
            img.CaptureMouse();
            mouseDown = true;
            position = e.GetPosition(img);
        }
        private void ImgMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            BackFrame.Cursor = Cursors.Arrow;
            var img = sender as ContentControl;
            if (img == null) { return; }
            img.ReleaseMouseCapture();
            mouseDown = false;
            var group = workspace.FindResource("Imageview") as TransformGroup;
            var move = group.Children[1] as TranslateTransform;
        }
        private void ImgMouseMove(object sender, MouseEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null) { return; }
            if (mouseDown)
            {
                DoMouseMove(img, e);
            }
        }
        private void DoMouseMove(ContentControl img, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { return; }
            var group = workspace.FindResource("Imageview") as TransformGroup;
            var scale = group.Children[0] as ScaleTransform;
            var move = group.Children[1] as TranslateTransform;
            var mouseXY = e.GetPosition(img);
            move.X += mouseXY.X - position.X;
            move.Y += mouseXY.Y - position.Y;
            position = mouseXY;
            // W+w > 2*move_x > -((2*scale-1)*w + W)  水平平移限制条件
            // H+h > 2*move_y > -((2*scale-1)*h + H)  垂直平移限制条件
            if (move.X * 2 > image.ActualWidth + img.ActualWidth - 20)
                move.X = (image.ActualWidth + img.ActualWidth - 20) / 2;
            if (-move.X * 2 > (2 * scale.ScaleX - 1) * image.ActualWidth + img.ActualWidth - 20)
                move.X = -((scale.ScaleX - 0.5) * image.ActualWidth + img.ActualWidth / 2 - 10);
            if (move.Y * 2 > image.ActualHeight + img.ActualHeight - 20)
                move.Y = (image.ActualHeight + img.ActualHeight - 20) / 2;
            if (-move.Y * 2 > (2 * scale.ScaleY - 1) * image.ActualHeight + img.ActualHeight - 20)
                move.Y = -((scale.ScaleY - 0.5) * image.ActualHeight + img.ActualHeight / 2 - 10);
        }
        private void ImgMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var img = sender as ContentControl;
            if (img == null) { return; }
            var point = e.GetPosition(image);
            var group = workspace.FindResource("Imageview") as TransformGroup;
            var delta = e.Delta * 0.002;
            DoWheelZoom(group, point, delta);
        }
        private void DoWheelZoom(TransformGroup group, Point point, double delta)
        {
            var transform = group.Children[0] as ScaleTransform;
            if (transform.ScaleX + delta < 0.1) return;
            transform.ScaleX += delta;
            transform.ScaleY += delta;
            var transform1 = group.Children[1] as TranslateTransform;
            transform1.X -= point.X * delta;
            transform1.Y -= point.Y * delta;
        }













    }
}

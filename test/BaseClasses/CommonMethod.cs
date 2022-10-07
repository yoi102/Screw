using NLog;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace test.BaseClasses
{
    class CommonMethod : NotifyPropertyChangedBase
    {
        NLog.Logger logger = LogManager.GetCurrentClassLogger();

        #region 压缩和截图

        /// <summary>
        /// 手动压缩图用
        /// </summary>
        /// <param name="InputFolder"></param>
        /// <param name="OutputFolder"></param>
        void CompressImage(string InputFolder, string OutputFolder)
        {
            try
            {
                //Filter = "图像文件|*.jpg;*.png;*.jpeg;*.bmp;*.gif|所有文件|*.*"//限定文件类型？

                Directory.CreateDirectory(OutputFolder);
                if (Directory.Exists(InputFolder))//文件夹 Directory ；文件 File.Exists。
                {
                    DirectoryInfo theFolder = new DirectoryInfo(InputFolder);
                    FileInfo[] fileInfo = theFolder.GetFiles("*.*");//全部文件类型，限定
                    MessageBox.Show("开始了，等着吧！！！\n搞完会弹窗哒！！！\n懒得做进度条\n( *︾▽︾)\t( *︾▽︾)\t( *︾▽︾)", "图片压缩");

                    foreach (FileInfo NextFile in fileInfo) //遍历文件夹里的文件
                    {
                        GetThumImage(NextFile.FullName, 18, 3, OutputFolder + "\\" + NextFile.Name.Remove(NextFile.Name.Length - 4) + ".jpg");
                        //using (Bitmap curBitmap = (Bitmap)Image.FromFile(NextFile.FullName))
                        //{
                        //    CanvasImage = curBitmap.ToBitmapSource();
                        //}

                    }
                    MessageBox.Show("压缩搞完啦！！！\n(ヘ･_･)ヘ┳━┳\n(╯°□°）╯︵ ┻━┻", "图片压缩");

                }
            }
            catch (Exception ex)
            {
                logger.Error("ImagesCut|  " + ex.Message);
                MessageBox.Show("出错啦！\n不会就不要搞啦！\n╮(╯▽╰)╭╮(╯▽╰)╭\n(￣_,￣ )", "图片压缩");
            }
        }
        /// <summary>
        /// 压缩图像
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="quality"></param>
        /// <param name="multiple"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        public bool getThumImage(Mat sourceFile, long quality, int multiple, string outputFile)
        {
            try
            {
                long imageQuality = quality;
                Bitmap sourceImage = sourceFile.ToBitmap();
                ImageCodecInfo myImageCodecInfo = GetEncoderInfo("image/jpeg");
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, imageQuality);
                myEncoderParameters.Param[0] = myEncoderParameter;
                float xWidth = sourceImage.Width;
                float yWidth = sourceImage.Height;
                Bitmap newImage = new Bitmap((int)(xWidth / multiple), (int)(yWidth / multiple));
                Graphics g = Graphics.FromImage(newImage);

                g.DrawImage(sourceImage, 0, 0, xWidth / multiple, yWidth / multiple);
                g.Dispose();
                newImage.Save(outputFile, myImageCodecInfo, myEncoderParameters);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("getThumImage|  " + ex.Message);
                return false;

            }
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="sourceFile">原始图片文件</param>
        /// <param name="quality">质量压缩比</param>
        /// <param name="multiple">收缩倍数</param>
        /// <param name="outputFile">输出文件名</param>
        /// <returns>成功返回true,失败则返回false</returns>
        public bool GetThumImage(string sourceFile, long quality, int multiple, string outputFile)
        {
            try
            {

                long imageQuality = quality;
                Bitmap sourceImage = new Bitmap(sourceFile);
                ImageCodecInfo myImageCodecInfo = GetEncoderInfo("image/jpeg");
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, imageQuality);
                myEncoderParameters.Param[0] = myEncoderParameter;
                float xWidth = sourceImage.Width;
                float yWidth = sourceImage.Height;
                Bitmap newImage = new Bitmap((int)(xWidth / multiple), (int)(yWidth / multiple));
                Graphics g = Graphics.FromImage(newImage);

                g.DrawImage(sourceImage, 0, 0, xWidth / multiple, yWidth / multiple);
                g.Dispose();
                newImage.Save(outputFile, myImageCodecInfo, myEncoderParameters);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("getThumImage|  " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取图片编码信息
        /// </summary>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }


        #endregion

        #region 清理文件

        /// <summary>
        /// Delete File
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        public bool CleanFile(string Path, ushort Day)//Day需要填正数。
        {
            try
            {
                //文件夹路径
                DirectoryInfo dyInfo = new DirectoryInfo(Path);

                //获取文件夹下所有的文件

                foreach (FileInfo feInfo in dyInfo.GetFiles())
                {
                    //判断文件日期是否小于今天，是则删除
                    if (feInfo.CreationTime < DateTime.Now.AddDays(-Day))//三天前
                    {
                        feInfo.Delete();//删除文件

                    }
                }
                foreach (DirectoryInfo dyInfoDelete in dyInfo.GetDirectories())//删除文件夹
                {
                    //判断文件日期是否小于今天，是则删除
                    if (dyInfoDelete.CreationTime < DateTime.Now.AddDays(-Day))//三天前
                    {
                        dyInfoDelete.Delete(true);//删除文件夹

                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error("CleanFile| " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 平均灰度，方差
        //计算图片平均灰度，也可标准差
        public double GetGrayAvg(Mat mat)
        {
            try
            {
                Mat img = new Mat();
                if (mat.Channels() == 3)
                {
                    Cv2.CvtColor(mat, img, ColorConversionCodes.BGR2GRAY);
                }
                else
                {
                    img = mat;
                }
                //Cv2.Mean(mat);
                Mat mean = new Mat();
                Mat stdDev = new Mat();
                Cv2.MeanStdDev(img, mean, stdDev);
                Scalar AvgBot = mean.Mean();
                Scalar StdBot = stdDev.Mean();
                logger.Info("Avg: " + AvgBot + "Std: " + StdBot);
                return AvgBot.Val0;

            }
            catch (Exception ex)
            {
                return 0;
                logger.Error("GetGrayAvg Run Fail!" + ex.Message);
            }

        }

        #endregion

        #region 选择图片
        /// <summary>
        /// 选择图片
        /// </summary>
        private BitmapSource FileADD( string FilePath ,BitmapSource bitmapSource)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog openfiledialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.png;*.jpeg;*.bmp;*.gif|所有文件|*.*"//限定文件类型？
                };

                if ((bool)openfiledialog.ShowDialog())//如果点击确定
                {
                    FilePath = openfiledialog.FileName;//选择的文件名给属性。。

                    using (Mat Image = new Mat(FilePath))
                    {
                        //   LocalImage = Image.ToBitmapSource();
                        bitmapSource = Image.ToBitmapSource();
                        return bitmapSource;

                    }
                }
                else
                {
                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                logger.Error("FileADD| " + ex.Message);
                return bitmapSource;

            }
        }

#endregion


    }
}

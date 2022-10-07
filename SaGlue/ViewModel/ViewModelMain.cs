using NLog;
using OpenCvSharp;
using SaGlue.BaseClasses;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;
using Rect = OpenCvSharp.Rect;
using SpinnakerNET;
using SaGlue.Model;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using SaGlue.View;
using System.Drawing.Imaging;

namespace SaGlue.ViewModel
{
    class ViewModelMain : NotifyPropertyChangedBase
    {

        private MainWindow mainWindow;
        NLog.Logger logger = LogManager.GetCurrentClassLogger();


        public ViewModelMain(MainWindow mainWindow)
        {

            this.mainWindow = mainWindow;

        }
        #region Field



        private static string CreateJsonFolder = "Json";
        private static string MorphologicalParametersFile = CreateJsonFolder + "\\MorphologicalParametersSetting.json";





        private bool LoadMorParaJsonData()
        {
            try
            {
                if (File.Exists(MorphologicalParametersFile))
                {
                    MorPara = JsonConvert.DeserializeObject<MorphologicalParameters>(File.ReadAllText(MorphologicalParametersFile), new JsonSerializerSettings//修改parameters为自己需要存储的文件就OK？
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                        DateParseHandling = DateParseHandling.DateTime
                    });
                }

                else
                {
                    Directory.CreateDirectory(CreateJsonFolder);
                    MorPara = new MorphologicalParameters();
                  
                    MorPara.PositiveGlueBinValue = 0;
                    MorPara.PositiveGlueWidth = 0;
                    MorPara.PositiveGlueWidthLimit = 0;
                    MorPara.PositiveGlueHeight = 0;
                    MorPara.PositiveGlueHeightLimit = 0;
                    MorPara.PositiveGlueArea = 0;
                    MorPara.PositiveGlueAreaLimit = 0;

                    MorPara.NegativeGlueBinValue = 0;
                    MorPara.NegativeGlueWidth = 0;
                    MorPara.NegativeGlueWidthLimit = 0;
                    MorPara.NegativeGlueHeight = 0;
                    MorPara.NegativeGlueHeightLimit = 0;
                    MorPara.NegativeGlueArea = 0;
                    MorPara.NegativeGlueAreaLimit = 0;

                    MorPara.DatumPointBinValue = 0;
                    MorPara.DatumPointWidth = 0;
                    MorPara.DatumPointWidthLimit = 0;
                    MorPara.DatumPointHeight = 0;
                    MorPara.DatumPointHeightLimit = 0;
                    MorPara.DatumPointArea = 0;
                    MorPara.DatumPointAreaLimit = 0;


                    JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.TypeNameHandling = TypeNameHandling.Auto;
                    serializer.Formatting = Formatting.Indented;
                    serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                    serializer.DateParseHandling = DateParseHandling.DateTime;

                    using (StreamWriter sw = new StreamWriter(MorphologicalParametersFile))
                    {
                        using (JsonWriter writer = new JsonTextWriter(sw))
                        {
                            serializer.Serialize(writer, MorPara, typeof(MorphologicalParameters));//修改parameters为自己需要存储的类的属性和命令
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                logger.Error("LoadMorphologicalParametersJsonData|  " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save MorphologicalParameters setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveMorParaToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(MorphologicalParametersFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, MorPara, typeof(MorphologicalParameters));//修改parameters为自己需要存储的类的属性和命令
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("SaveMorphologicalParametersToJsonData| " + ex.Message);

                return false;
            }
            return true;
        }



















        #endregion

        #region Constructor















        #endregion

        #region Property

        private double _DatumPoint_X;

        public double DatumPoint_X
        {
            get { return _DatumPoint_X; }
            set { if (_DatumPoint_X != value) { _DatumPoint_X = value; RaisePropertyChanged("DatumPoint_X"); } }
        }
        private double _DatumPoint_Y;
        public double DatumPoint_Y
        {
            get { return _DatumPoint_Y; }
            set { if (_DatumPoint_Y != value) { _DatumPoint_Y = value; RaisePropertyChanged("DatumPoint_Y"); } }
        }






        private Visibility _VisibilityAll;
        public Visibility VisibilityAll
        {
            get { return _VisibilityAll; }
            set { if (_VisibilityAll != value) { _VisibilityAll = value; RaisePropertyChanged("VisibilityAll"); } }
        }

        private BitmapSource _OriginalPositiveImage;
        public BitmapSource OriginalPositiveImage
        {
            get { return _OriginalPositiveImage; }
            set
            {
                if (value != _OriginalPositiveImage)
                {
                    _OriginalPositiveImage = value;
                    _OriginalPositiveImage.Freeze();
                    RaisePropertyChanged("OriginalPositiveImage");
                }
            }
        }
        private BitmapSource _OriginalNegativeImage;
        public BitmapSource OriginalNegativeImage
        {
            get { return _OriginalNegativeImage; }
            set
            {
                if (value != _OriginalNegativeImage)
                {
                    _OriginalNegativeImage = value;
                    _OriginalNegativeImage.Freeze();
                    RaisePropertyChanged("OriginalNegativeImage");
                }
            }
        }

        private BitmapSource _MinorPositiveImage;
        public BitmapSource MinorPositiveImage
        {
            get { return _MinorPositiveImage; }
            set
            {
                if (value != _MinorPositiveImage)
                {
                    _MinorPositiveImage = value;
                    _MinorPositiveImage.Freeze();
                    RaisePropertyChanged("MinorPositiveImage");
                }
            }
        }


        private BitmapSource _MinorNegativeImage;
        public BitmapSource MinorNegativeImage
        {
            get { return _MinorNegativeImage; }
            set
            {
                if (value != _MinorNegativeImage)
                {
                    _MinorNegativeImage = value;
                    _MinorNegativeImage.Freeze();
                    RaisePropertyChanged("MinorNegativeImage");
                }
            }
        }




        private BitmapSource _ClosePosiiveImage;
        public BitmapSource ClosePosiiveImage
        {
            get { return _ClosePosiiveImage; }
            set
            {
                if (value != _ClosePosiiveImage)
                {
                    _ClosePosiiveImage = value;
                    _ClosePosiiveImage.Freeze();
                    RaisePropertyChanged("ClosePosiiveImage");
                }
            }
        }
        private BitmapSource _CloseNegativeImage;
        public BitmapSource CloseNegativeImage
        {
            get { return _CloseNegativeImage; }
            set
            {
                if (value != _CloseNegativeImage)
                {
                    _CloseNegativeImage = value;
                    _CloseNegativeImage.Freeze();
                    RaisePropertyChanged("CloseNegativeImage");
                }
            }
        }



        private MorphologicalParameters _MorPara = new MorphologicalParameters();
        public MorphologicalParameters MorPara
        {
            get { return _MorPara; }
            set { if (_MorPara != value) { _MorPara = value; RaisePropertyChanged("MorPara"); } }
        }





        #endregion

        #region Method



        /// <summary>
        /// 找定位位置
        /// </summary>
        private void DatumPointCapture(BitmapSource DisplayMat)
        {
            try
            {
                List<ConnectedComponents.Blob> DatumPointBlobs = new List<ConnectedComponents.Blob>();
                using (Mat Image = DisplayMat.ToMat())
                using (Mat ImageMedian = new Mat())
                using (Mat Gray = new Mat())
                using (Mat Bin = new Mat())
                using (Mat ClosingImage = new Mat())
                using (Mat OpeningImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(Image, ImageMedian, 5);
                    Cv2.CvtColor(ImageMedian, Gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.EqualizeHist(Gray, Gray);
                    Cv2.Threshold(Gray, Bin, MorPara.DatumPointBinValue, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(Bin, OpeningImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(OpeningImage, ClosingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);

                    var rr = Cv2.ConnectedComponentsEx(ClosingImage);
                    foreach (var Recblob in rr.Blobs.Skip(1))
                    {
                        if (Math.Abs(Recblob.Width - MorPara.DatumPointWidth) <= MorPara.DatumPointWidthLimit && 
                            Math.Abs(Recblob.Height - MorPara.DatumPointHeight) <= MorPara.DatumPointHeightLimit &&
                            Math.Abs(Recblob.Area - MorPara.DatumPointArea) <= MorPara.DatumPointAreaLimit) //方定位
                        {
                            DatumPointBlobs.Add(Recblob);
                            logger.Debug("DatumPointCapture: " + "W:" + DatumPointBlobs[0].Width + "H: " + DatumPointBlobs[0].Height + "A" + DatumPointBlobs[0].Area);

                        }
                    }
                    if (DatumPointBlobs.Count != 1)
                    {

                        for (int i = 0; i < DatumPointBlobs.Count; i++)
                        {
                            //注意XY，后续可能要改
                            logger.Warn("DatumPointCapture: " + "W:" + DatumPointBlobs[i].Width + "H: " + DatumPointBlobs[i].Height + "A" + DatumPointBlobs[i].Area);
                            logger.Warn("DatumPointCapture: " + "X:" + DatumPointBlobs[i].Left + "Y: " + DatumPointBlobs[i].Top);
                            
                        }
                        logger.Warn("DatumPointBlobs.Count!=1： " + DatumPointBlobs.Count);


                    }
                    else if (DatumPointBlobs.Count == 1)
                    {
                        //左上角还是右上角好？
                        DatumPoint_X = DatumPointBlobs[0].Left;
                        DatumPoint_Y = DatumPointBlobs[0].Top;

                        logger.Info("DatumPointCapture: " + "W:" + DatumPointBlobs[0].Width + "H: " + DatumPointBlobs[0].Height + "A" + DatumPointBlobs[0].Area);
                        logger.Info("DatumPointCapture: " + "X:" + DatumPoint_X + "Y: " + DatumPoint_Y);
                    }
                }


            }
            catch (Exception ex)
            {
                logger.Error("DatumPointCapture|  " + ex.Message);
                
            }
        }

        private void PositiveGlue(BitmapSource DisplayMat)
        {

            try
            {
                List<ConnectedComponents.Blob> PositiveGlueBlobs = new List<ConnectedComponents.Blob>();
                using (Mat Image = DisplayMat.ToMat())
                using (Mat ImageMedian = new Mat())
                using (Mat Gray = new Mat())
                using (Mat Bin = new Mat())
                using (Mat ClosingImage = new Mat())
                using (Mat OpeningImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(Image, ImageMedian, 5);
                    Cv2.CvtColor(ImageMedian, Gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.EqualizeHist(Gray, Gray);
                    Cv2.Threshold(Gray, Bin, MorPara.PositiveGlueBinValue, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(Bin, OpeningImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(OpeningImage, ClosingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);

                    var rr = Cv2.ConnectedComponentsEx(ClosingImage);
                    foreach (var Recblob in rr.Blobs.Skip(1))
                    {
                        if (Math.Abs(Recblob.Width - MorPara.PositiveGlueWidth) <= MorPara.PositiveGlueWidthLimit &&
                            Math.Abs(Recblob.Height - MorPara.PositiveGlueHeight) <= MorPara.PositiveGlueHeightLimit &&
                            Math.Abs(Recblob.Area - MorPara.PositiveGlueArea) <= MorPara.PositiveGlueAreaLimit) //方定位
                        {
                            PositiveGlueBlobs.Add(Recblob);
                            logger.Debug("PositiveGlueBlobs: " + "W:" + PositiveGlueBlobs[0].Width + "H: " + PositiveGlueBlobs[0].Height + "A" + PositiveGlueBlobs[0].Area);

                        }
                    }
                    if (PositiveGlueBlobs.Count != 1)
                    {

                        for (int i = 0; i < PositiveGlueBlobs.Count; i++)
                        {
                            //注意XY，后续可能要改
                            logger.Warn("PositiveGlueBlobs: " + "W:" + PositiveGlueBlobs[i].Width + "H: " + PositiveGlueBlobs[i].Height + "A" + PositiveGlueBlobs[i].Area);
                            logger.Warn("PositiveGlueBlobs: " + "X:" + PositiveGlueBlobs[i].Left + "Y: " + PositiveGlueBlobs[i].Top);

                        }
                        logger.Warn("PositiveGlueBlobs.Count!=1： " + PositiveGlueBlobs.Count);


                    }
                    else if (PositiveGlueBlobs.Count == 1)
                    {
                        //左上角还是右上角好？
                        DatumPoint_X = PositiveGlueBlobs[0].Left;
                        DatumPoint_Y = PositiveGlueBlobs[0].Top;

                        logger.Info("DatumPointCapture: " + "W:" + PositiveGlueBlobs[0].Width + "H: " + PositiveGlueBlobs[0].Height + "A" + PositiveGlueBlobs[0].Area);
                        logger.Info("DatumPointCapture: " + "X:" + DatumPoint_X + "Y: " + DatumPoint_Y);
                    }
                }


            }
            catch (Exception ex)
            {
                logger.Error("PositiveGlue|  " + ex.Message);

            }




        }

        private void NegativeGlue( BitmapSource InputMat)
        {

            try
            {
                List<ConnectedComponents.Blob> NegativeGlueBlobs = new List<ConnectedComponents.Blob>();
                using (Mat Image = InputMat.ToMat())
                using (Mat ImageMedian = new Mat())
                using (Mat Gray = new Mat())
                using (Mat Bin = new Mat())
                using (Mat ClosingImage = new Mat())
                using (Mat OpeningImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(Image, ImageMedian, 5);
                    Cv2.CvtColor(ImageMedian, Gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.EqualizeHist(Gray, Gray);
                    Cv2.Threshold(Gray, Bin, MorPara.NegativeGlueBinValue, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(Bin, OpeningImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(OpeningImage, ClosingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);

                    var rr = Cv2.ConnectedComponentsEx(ClosingImage);
                    foreach (var Recblob in rr.Blobs.Skip(1))
                    {
                        if (Math.Abs(Recblob.Width - MorPara.NegativeGlueWidth) <= MorPara.NegativeGlueWidthLimit &&
                            Math.Abs(Recblob.Height - MorPara.NegativeGlueHeight) <= MorPara.NegativeGlueHeightLimit &&
                            Math.Abs(Recblob.Area - MorPara.NegativeGlueArea) <= MorPara.NegativeGlueAreaLimit) //方定位
                        {
                            NegativeGlueBlobs.Add(Recblob);
                            logger.Debug("NegativeGlueBlobs: " + "W:" + NegativeGlueBlobs[0].Width + "H: " + NegativeGlueBlobs[0].Height + "A" + NegativeGlueBlobs[0].Area);

                        }
                    }
                    if (NegativeGlueBlobs.Count != 1)
                    {

                        for (int i = 0; i < NegativeGlueBlobs.Count; i++)
                        {
                            //注意XY，后续可能要改
                            logger.Warn("NegativeGlueBlobs: " + "W:" + NegativeGlueBlobs[i].Width + "H: " + NegativeGlueBlobs[i].Height + "A" + NegativeGlueBlobs[i].Area);
                            logger.Warn("NegativeGlueBlobs: " + "X:" + NegativeGlueBlobs[i].Left + "Y: " + NegativeGlueBlobs[i].Top);

                        }
                        logger.Warn("NegativeGlueBlobs.Count!=1： " + NegativeGlueBlobs.Count);


                    }
                    else if (NegativeGlueBlobs.Count == 1)
                    {
                        //左上角还是右上角好？
                        DatumPoint_X = NegativeGlueBlobs[0].Left;
                        DatumPoint_Y = NegativeGlueBlobs[0].Top;

                        logger.Info("DatumPointCapture: " + "W:" + NegativeGlueBlobs[0].Width + "H: " + NegativeGlueBlobs[0].Height + "A" + NegativeGlueBlobs[0].Area);
                        logger.Info("DatumPointCapture: " + "X:" + DatumPoint_X + "Y: " + DatumPoint_Y);
                    }
                }


            }
            catch (Exception ex)
            {
                logger.Error("NegativeGlue|  " + ex.Message);

            }




        }





















        #endregion

        #region Command


        #endregion

        #region


        #endregion

        #region


        #endregion




    }
}

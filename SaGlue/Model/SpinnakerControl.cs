using Newtonsoft.Json;
using NLog;
using SaGlue.BaseClasses;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SaGlue.Model
{
    class SpinnakerControl : NotifyPropertyChangedBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
   
        #region Field
        private IList<IManagedCamera> camList;//图片，现在用的时camList[1]，另外的相机端口呢，怎么选用哪个相机？
        private ManagedSystem system = new ManagedSystem();
        [JsonIgnore]
        public int CamNum = 0;
        #endregion

        #region Operations
        /// <summary>
        /// 相机初始化
        /// </summary>
        /// <param name="cam"></param>
        /// <returns></returns>
        public int InitCamera(IManagedCamera cam)
        {
            int result = 0;
            try
            {
                // Retrieve TL device nodemap and print device information
                INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();
                //result = PrintDeviceInfo(nodeMapTLDevice);
                // Initialize camera
                cam.Init();

                // Retrieve GenICam nodemap
                INodeMap nodeMap = cam.GetNodeMap();
                // Acquire images
                result |= InitCamereMode(cam, nodeMap, nodeMapTLDevice);
                // Deinitialize camera
                //cam.DeInit();
                //cam.BeginAcquisition();
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }
            return result;
        }

        /// <summary>
        ///   获取摄像头设备信息
        /// </summary>
        private int InitCamereMode(IManagedCamera cam, INodeMap nodeMap, INodeMap nodeMapTLDevice)
        {
            int result = 0;
            try
            {
                // Retrieve enumeration node from nodemap
                IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
                if (iAcquisitionMode == null || !iAcquisitionMode.IsWritable)
                {
                    return -1;
                }
                // Retrieve entry node from enumeration node
                IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
                if (iAcquisitionModeContinuous == null || !iAcquisitionMode.IsReadable)
                {
                    return -1;
                }
                // Set symbolic from entry node as new value for enumeration node
                iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = 0;
            }
            return result;
        }


        /// <summary>
        /// 相机获取画像数据
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="returnImage"></param>
        /// <returns></returns>
        public int AcquireImages_Simple(IManagedCamera cam, out BitmapSource returnImage)
        {
            //DateTime dtstart = DateTime.Now;
            int result = 0;
            ManagedImage convertedImage = new ManagedImage();

            // Retrieve, convert
            try
            {
                cam.BeginAcquisition();
                using (IManagedImage rawImage = cam.GetNextImage())
                {
                    if (rawImage.IsIncomplete)
                    {
                        Console.WriteLine("Image incomplete with image status {0}...", rawImage.ImageStatus);
                        result = -1;
                    }
                    else
                    {
                        PixelFormatEnums pixelFormat = (PixelFormatEnums)Enum.Parse(typeof(PixelFormatEnums), cam.PixelFormat.Value, true);
                      //  pixelFormat = PixelFormatEnums.Mono8;
                        pixelFormat = PixelFormatEnums.BGR8;
                        
                        //if (pixelFormat != PixelFormatEnums.Mono8)
                        //{
                        //    pixelFormat = PixelFormatEnums.BGR8;
                        //}
                        rawImage.ConvertToBitmapSource(pixelFormat, convertedImage, ColorProcessingAlgorithm.HQ_LINEAR);

                        //attributeObj.timeDecay = (DateTime.Now - dtstart).TotalMilliseconds;
                        result = 0;
                    }
                    // End acquisition
                    cam.EndAcquisition();
                }

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            returnImage = convertedImage.bitmapsource.Clone();
            // 不丢弃的话，内存会溢出
            convertedImage.Dispose();
            return result;

        }

        public int AcquireImages_Simple_test(IManagedCamera cam, out BitmapSource returnImage)
        {
            //DateTime dtstart = DateTime.Now;
            int result = 0;
            ManagedImage convertedImage = new ManagedImage();

            // Retrieve, convert
            try
            {
                cam.BeginAcquisition();
                using (IManagedImage rawImage = cam.GetNextImage())
                {
                    if (rawImage.IsIncomplete)
                    {
                        Console.WriteLine("Image incomplete with image status {0}...", rawImage.ImageStatus);
                        result = -1;
                    }
                    else
                    {
                        PixelFormatEnums pixelFormat = (PixelFormatEnums)Enum.Parse(typeof(PixelFormatEnums), cam.PixelFormat.Value, true);
                        pixelFormat = PixelFormatEnums.Mono8;

                        //if (pixelFormat != PixelFormatEnums.Mono8)
                        //{
                        //    pixelFormat = PixelFormatEnums.BGR8;
                        //}
                        rawImage.ConvertToBitmapSource(pixelFormat, convertedImage, ColorProcessingAlgorithm.HQ_LINEAR);

                        //attributeObj.timeDecay = (DateTime.Now - dtstart).TotalMilliseconds;
                        result = 0;
                    }
                    // End acquisition
                    cam.EndAcquisition();
                }

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                result = -1;
            }

            returnImage = convertedImage.bitmapsource.Clone();
            // 不丢弃的话，内存会溢出
            convertedImage.Dispose();
            return result;

        }
        /// <summary>
        /// 配置相机的曝光
        /// </summary>
        /// <param name="nodeMap"></param>
        /// <returns></returns>
        public int ConfigureExposure(INodeMap nodeMap, double exposureTimeToSet, ref string Message)
        {
            int result = 0;


            //Console.WriteLine("\n\n*** CONFIGURING EXPOSURE ***\n");

            try
            {
                //
                // Turn off automatic exposure mode
                //
                // *** NOTES ***
                // Automatic exposure prevents the manual configuration of 
                // exposure time and needs to be turned off.
                //
                // *** LATER ***
                // Exposure time can be set automatically or manually as needed. 
                // This example turns automatic exposure off to set it manually 
                // and back on in order to return the camera to its default
                // state.
                //
                IEnum iExposureAuto = nodeMap.GetNode<IEnum>("ExposureAuto");
                if (iExposureAuto == null || !iExposureAuto.IsWritable)
                {
                    //Console.WriteLine("Unable to disable automatic exposure (enum retrieval). Aborting...\n");
                    return -1;
                }

                IEnumEntry iExposureAutoOff = iExposureAuto.GetEntryByName("Off");
                if (iExposureAutoOff == null || !iExposureAutoOff.IsReadable)
                {
                    //Console.WriteLine("Unable to disable automatic exposure (entry retrieval). Aborting...\n");
                    return -1;
                }

                iExposureAuto.Value = iExposureAutoOff.Value;

                //Console.WriteLine("Automatic exposure disabled...");

                //
                // Set exposure time manually; exposure time recorded in microseconds
                //
                // *** NOTES ***
                // The node is checked for availability and writability prior 
                // to the setting of the node. Further, it is ensured that the
                // desired exposure time does not exceed the maximum. Exposure 
                // time is counted in microseconds. This information can be 
                // found out either by retrieving the unit with the GetUnit() 
                // method or by checking SpinView.
                // 
                //const double exposureTimeToSet = 2294.36;
                //double exposureTimeToSet = myPara.Camera_exposure;

                IFloat iExposureTime = nodeMap.GetNode<IFloat>("ExposureTime");
                if (iExposureTime == null || !iExposureTime.IsWritable)
                {
                    //Console.WriteLine("Unable to set exposure time. Aborting...\n");
                    return -1;
                }

                // Ensure desired exposure time does not exceed the maximum
                iExposureTime.Value = (exposureTimeToSet > iExposureTime.Max ? iExposureTime.Max : exposureTimeToSet);

                //Console.WriteLine("Exposure time set to {0} us...\n", iExposureTime.Value);
                Message = "Exposure time set to " + iExposureTime.Value + " us...";
                //richControl.INFO_message(nodeMap.ToString() + "Exposure time set to" + iExposureTime.Value + "us...");

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                // _logger.Error(nodeMap.ToString() + "Exception_Error:" + ex.Message);
                result = -1;
            }

            return result;
        }

        /// <summary>
        /// 重置相机曝光度    自动曝光
        /// </summary>
        /// <param name="nodeMap"></param>
        /// <returns></returns>
        public int ResetExposure(INodeMap nodeMap)
        {
            int result = 0;

            try
            {
                // 
                // Turn automatic exposure back on
                //
                // *** NOTES ***
                // It is recommended to have automatic exposure enabled 
                // whenever manual exposure settings are not required.
                //
                IEnum iExposureAuto = nodeMap.GetNode<IEnum>("ExposureAuto");
                if (iExposureAuto == null || !iExposureAuto.IsWritable)
                {
                    Console.WriteLine("Unable to enable automatic exposure (enum retrieval). Aborting...\n");
                    //_logger.Debug("Unable to enable automatic exposure (enum retrieval). Aborting...");
                    return -1;
                }

                IEnumEntry iExposureAutoContinuous = iExposureAuto.GetEntryByName("Continuous");
                if (iExposureAutoContinuous == null || !iExposureAutoContinuous.IsReadable)
                {
                    Console.WriteLine("Unable to enable automatic exposure (entry retrieval). Aborting...\n");
                    //_logger.Debug("Unable to enable automatic exposure (entry retrieval). Aborting...");
                    return -1;
                }

               iExposureAuto.Value = iExposureAutoContinuous.Value;
                ExposeTime = iExposureAutoContinuous.Value;

                //Console.WriteLine("Automatic exposure enabled...\n");
                //richControl.Debug_message("Automatic exposure enabled...");
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                //_logger.Error(nodeMap.ToString() + "Exception_Error:" + ex.Message);
                result = -1;
            }

            return result;
        }

        /// <summary>
        ///  配置相机的增益
        /// </summary>
        /// <param name="nodeMap"></param>
        public void ConfigureGain(INodeMap nodeMap, double gain_num, ref string Message)
        {

            try
            {
                // Set gain and exposure time value on camera
                // Remove from automatic mode
                IEnum iGainAuto = nodeMap.GetNode<IEnum>("GainAuto");
                if (iGainAuto == null || !iGainAuto.IsWritable)
                {
                    // _logger.Error("GainAuto node not available...");
                    //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                }

                IEnumEntry iGainAutoOff = iGainAuto.GetEntryByName("Off");
                if (iGainAutoOff == null || !iGainAutoOff.IsReadable)
                {
                    // _logger.Error("GainAuto off not available...");
                    //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                }
                iGainAuto.Value = iGainAutoOff.Value;

                IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
                if (iGain == null || !iGain.IsWritable)
                {
                    //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                }
                // Set gain
                //iGain.Value = myPara.Camera_gain;
                //double gain_num = myPara.Camera_gain;
                //_logger.Debug("Gain set to {0}", iGain.Value);
                Message = "Gain set to" + iGain.Value;
                //richControl.Debug_message(nodeMap.ToString() +"Gain set to" + iGain.Value);

                iGain.Value = (gain_num > iGain.Max ? iGain.Max : gain_num);

            }
            catch (Exception ex)
            {
                logger.Error("Error ConfigureGain:" + ex.Message);
            }

        }


        /// <summary>
        /// 重置相机增益           自动增益把
        /// </summary>
        /// <param name="nodeMap"></param>
        public void ResetGain(INodeMap nodeMap)
        {
            try
            {
                IEnum iGainAuto = nodeMap.GetNode<IEnum>("GainAuto");
                if (iGainAuto == null || !iGainAuto.IsWritable)
                {
                    // _logger.Error("GainAuto node not available...");
                    //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                }

                IEnumEntry iGainAutoContinuous = iGainAuto.GetEntryByName("Continuous");
                if (iGainAutoContinuous == null || !iGainAutoContinuous.IsReadable)
                {

                }

               
                iGainAuto.Value = iGainAutoContinuous.Value;
                Gain = iGainAutoContinuous.Value;

                //richControl.Debug_message("Automatic Gain enabled...");

            }
            catch (Exception ex)
            {
                logger.Error(nodeMap.ToString() + "Error ResetGain:" + ex.Message);
            }


        }
        #endregion

        #region Method
        /// <summary>
        /// 相机初始化
        /// </summary>
        public void CameraInit()
        {
            camList = system.GetCameras();//初始化就打开摄像头
            CamNum = camList.Count;
            if (CamNum > 0)
            {
                InitCamera(camList[1]);//初始化相机0；有两个就可以camlist[1]?
                CamIsConnected = true;
                logger.Trace("检测到相机");

            }
            else
            {
                CamIsConnected = false;

                logger.Error("未检测到相机");
            }
        }

        /// <summary>
        /// 相机捕获
        /// </summary>
        public void CameraCapture()
        {
            if (CamNum > 0)
            {
                AcquireImages_Simple(camList[1], out BitmapSource bitmapSource);
                CameraImage = bitmapSource;
                logger.Trace("相机打开");
            }
            else
            {
                logger.Error("相机未初始化");
            }

            GC.Collect();
        }
        public void CameraCapture_test()
        {
            if (CamNum > 0)
            {
                system.UpdateCameras();
                BitmapSource bitmapSource = null;
                AcquireImages_Simple_test(camList[1], out bitmapSource);
                CameraImage = bitmapSource;
                logger.Trace("相机打开");
                camList.Clear();
                camList = null;
            }
            else
            {
                logger.Error("相机未初始化");
            }

            GC.Collect();
        }
        public void ConfigureGain()
        {
            string GainMessage = null;
            ConfigureGain(camList[1].GetNodeMap(), Gain, ref GainMessage);
        }
        public void ConfigureExposure()
        {
            string ExposureMessage = null;
            ConfigureExposure(camList[1].GetNodeMap(), ExposeTime, ref ExposureMessage);
        }
        public void ResetExposure()
        {
            ResetExposure(camList[1].GetNodeMap());
        }
        public void ResetGain()
        {
            ResetGain(camList[1].GetNodeMap());

        }

        #endregion

        #region Property
 
        private ushort _CleanDay;
        public ushort CleanDay
        {
            get { return _CleanDay; }
            set { if (_CleanDay != value) { _CleanDay = value; RaisePropertyChanged("CleanDay"); } }
        }

        private double _ExposeTime;
        public double ExposeTime
        {
            get { return _ExposeTime; }
            set { if (_ExposeTime != value) { _ExposeTime = value; RaisePropertyChanged("ExposeTime"); } }
        }

        private double _Gain;
        public double Gain
        {
            get { return _Gain; }
            set { if (_Gain != value) { _Gain = value; RaisePropertyChanged("Gain"); } }
        }

        private BitmapSource _CameraImage;
        [JsonIgnore]
        public BitmapSource CameraImage            //用来绑定image控件    网上有用其他类型的，自定义的MatToBitmapImage。
        {
            get { return _CameraImage; }
            set
            {
                if (value != _CameraImage)
                {
                    _CameraImage = value;
                    _CameraImage.Freeze();
                    RaisePropertyChanged("CameraImage");
                }
            }
        }
        private bool _CamIsConnected;//相机是否连接
        [JsonIgnore]
        public bool CamIsConnected
        {
            get { return _CamIsConnected; }
            set { if (_CamIsConnected != value) { _CamIsConnected = value; RaisePropertyChanged("CamIsConnected"); } }
        }




        #endregion



       









    }


}
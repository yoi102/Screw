using Newtonsoft.Json;
using NLog;
using Screw.BaseClasses;
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

namespace Screw.Model
{

    public class SpinnakerControl : NotifyPropertyChangedBase
    {
        // initialize logger
        Logger logger = LogManager.GetCurrentClassLogger();
        private const string camSettingConfigFile = @"Config\cameraSetting.config";

        // Retrieve singleton reference to system object
        private ManagedSystem system = new ManagedSystem();

        #region Property

        /// <summary>
        /// Collection to camera objects
        /// </summary>
        private ObservableCollection<CameraInformation> _cameras = new ObservableCollection<CameraInformation>();
        public ObservableCollection<CameraInformation> cameras
        {
            get { return _cameras; }
            set { if (_cameras != value) { _cameras = value; RaisePropertyChanged("cameras"); } }
        }

        /// <summary>
        /// Is Camera Available
        /// </summary>
        private bool _IsCamAvailable;
        public bool IsCamAvailable
        {
            get { return _IsCamAvailable; }
            set { if (_IsCamAvailable != value) { _IsCamAvailable = value; RaisePropertyChanged("IsCamAvailable"); } }
        }

        #endregion

        public SpinnakerControl()
        {
            PrintBuildInfo();
            // load camera settings
            LoadCameraSettings();
            // init camera
            //InitCameras(); // call from parant object
        }

        ~SpinnakerControl()
        {
            StopCameras();
        }

        private void LoadCameraSettings()
        {
            // read cam mapping
            if (File.Exists(camSettingConfigFile))
            {
                cameras = JsonConvert.DeserializeObject<ObservableCollection<CameraInformation>>(File.ReadAllText(camSettingConfigFile), new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                    DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                    DateParseHandling = DateParseHandling.DateTime
                });
            }
        }

        public void SaveCameraSettings()
        {
            // write sample cam setting file
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            serializer.Formatting = Formatting.Indented;
            serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
            serializer.DateParseHandling = DateParseHandling.DateTime;

            using (StreamWriter sw = new StreamWriter(camSettingConfigFile))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, cameras, typeof(ObservableCollection<CameraInformation>));
                }
            }
        }

        /// <summary>
        /// Initialize cameras
        /// </summary>
        /// <returns></returns>
        public int InitCameras()
        {
            // Retrieve list of cameras from the system
            IList<IManagedCamera> cameraList = system.GetCameras();
            Console.WriteLine("Number of cameras detected: {0}\n\n", cameraList.Count);

            // Finish if there are no cameras
            if (cameraList.Count == 0)
            {
                // Clear camera list before releasing system
                // cameraList.Clear();

                // Release system
                //system.Dispose();//如果启用的话，二次连接就会闪退。

                logger.Error("No camera found!");


                return -1;
            }

            int result = 0;
            logger.Debug("\n*** DEVICE INFORMATION ***\n");
            // initiliaze cameras array according to camera map 
            if (cameras.Count > 0)
            {
                ///////////////////// CAMERA MAPPING ////////////////////////////
                // Matching cameras
                for (int c = 0; c < cameraList.Count; c++)
                {
                    // Initialize camera
                    cameraList[c].Init();

                    // Retrieve TL device nodemap
                    INodeMap nodeMapTLDevice = cameraList[c].GetTLDeviceNodeMap();
                    // Print device information
                    result = result | PrintDeviceInfo(nodeMapTLDevice, c);

                    IString iDeviceSerialNumber = cameraList[c].GetTLDeviceNodeMap().GetNode<IString>("DeviceSerialNumber");

                    if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
                    {
                        if (cameras.Count > 0)
                        {
                            for (int i = 0; i < cameras.Count; i++)
                            {
                                if (cameras[i].DeviceSerialNumber == iDeviceSerialNumber.Value)
                                {
                                    cameras[i].CamManagedCamera = cameraList[c];
                                }
                            }
                        }
                    }
                }

                bool allCamsMatched = true;
                // Prepare each camera to acquire images
                for (int i = 0; i < cameras.Count; i++)
                {
                    if (cameras[i].CamManagedCamera != null)
                    {
                        // is connected flag
                        cameras[i].IsConnected = true;

                        try
                        {
                            // Set cameras' properties
                            cameras[i].SetCamProperty(cameras[i].Gain, cameras[i].ExposureTime);
                            // get again
                            cameras[i].GetCamProperty();
                        }
                        catch (System.Exception ex)
                        {
                            cameras[i].IsConnected = false;

                            logger.Error("Error configuring camera : {0}", ex.Message);
                            return -1;
                        }
                        //try
                        //{
                        //    // Begin acquiring images
                        //    //cameras[i].CamManagedCamera.BeginAcquisition();
                        //}
                        //catch (System.Exception ex)
                        //{
                        //    logger.Error("Error starting camera : {0}", ex.Message);
                        //    return -1;
                        //}
                    }
                    else
                    {
                        allCamsMatched = false;
                    }
                }

                // save to local file
                //SaveCameraSettings();///这个不应该初始化后保存，否则初始失败，会导致修改配置文件了。

                if (allCamsMatched)
                {
                    IsCamAvailable = true;
                    return 0;
                }
                else
                {
                    IsCamAvailable = false;
                    return -1;
                }
            }
            else
            {
                int camCnt = 0;
                foreach (IManagedCamera cam in cameraList)
                {
                    // Initialize camera
                    cam.Init();
                    // Retrieve TL device nodemap
                    INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();
                    // Print device information
                    result = result | PrintDeviceInfo(nodeMapTLDevice, camCnt++);
                    // Get serial number
                    IString iDeviceSerialNumber = cam.GetTLDeviceNodeMap().GetNode<IString>("DeviceSerialNumber");

                    if (iDeviceSerialNumber != null && iDeviceSerialNumber.IsReadable)
                    {
                        CameraInformation newCamInfo = new CameraInformation() { DeviceSerialNumber = iDeviceSerialNumber.Value, CamManagedCamera = cam, IsConnected = true };
                        // get gain and exposuretime
                        //newCamInfo.SetCamProperty(cameras[i].Gain, cameras[i].ExposureTime);

                        newCamInfo.GetCamProperty();
                        // add to camera list
                        cameras.Add(newCamInfo);
                    }
                }
                // save to local file
                SaveCameraSettings();

                return -1;
            }
        }

        /// <summary>
        /// Retreive bitmap image from camera
        /// </summary>
        /// <param name="camIndex"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public BitmapSource AcquisitionBitmapFromCam_(int camIndex)
        {
            ManagedImage m_converted = new ManagedImage();
            try
            {
                logger.Debug("Acquisition Start");
                cameras[camIndex].CamManagedCamera.BeginAcquisition();

                // Retrieve an image and ensure image completion
                using (IManagedImage rawImage = cameras[camIndex].CamManagedCamera.GetNextImage())
                {
                    if (rawImage.IsIncomplete)
                    {
                        logger.Error("Image incomplete with image status {0}...", rawImage.ImageStatus);
                    }
                    else
                    {
                        // Print image information
                        logger.Debug(string.Format("Camera {0} grabbed image, width = {1}, height = {2}", camIndex, rawImage.Width, rawImage.Height));

                        // convert to bitmap and show
                        //rawImage.ConvertToBitmapSource(PixelFormatEnums.Mono8, m_converted, ColorProcessingAlgorithm.HQ_LINEAR);
                        PixelFormatEnums pixelFormat = (PixelFormatEnums)Enum.Parse(typeof(PixelFormatEnums), cameras[camIndex].PixelFormat.Value, true);
                        if (pixelFormat != PixelFormatEnums.Mono8)
                        {
                            pixelFormat = PixelFormatEnums.BGR8;
                        }
                        rawImage.ConvertToBitmapSource(pixelFormat, m_converted, ColorProcessingAlgorithm.HQ_LINEAR);
                    }
                }

                cameras[camIndex].CamManagedCamera.EndAcquisition();
                logger.Debug("Acquisition End");
            }
            catch (Exception ex)
            {
                logger.Error("Acquisition error|" + ex.Message);
            }

            return m_converted.bitmapsource;
        }

        /// <summary>
        /// Retreive bitmap image from camera
        /// </summary>
        /// <param name="camIndex"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public bool AcquisitionBitmapFromCam(int camIndex, out BitmapSource img)
        {
            bool result = false;

            ManagedImage m_converted = new ManagedImage();

            try
            {
                logger.Debug("Acquisition Start camIndex={0}", camIndex);
                cameras[camIndex].CamManagedCamera.BeginAcquisition();

                // Retrieve an image and ensure image completion
                using (IManagedImage rawImage = cameras[camIndex].CamManagedCamera.GetNextImage())
                {
                    if (rawImage.IsIncomplete)
                    {
                        logger.Error("Image incomplete with image status {0}...", rawImage.ImageStatus);
                        result = false;
                    }
                    else
                    {
                        // Print image information
                        logger.Debug(string.Format("Camera {0} grabbed image, width = {1}, height = {2}", camIndex, rawImage.Width, rawImage.Height));

                        // convert to bitmap and show
                        //rawImage.ConvertToBitmapSource(PixelFormatEnums.Mono8, m_converted, ColorProcessingAlgorithm.HQ_LINEAR);
                        PixelFormatEnums pixelFormat = (PixelFormatEnums)Enum.Parse(typeof(PixelFormatEnums), cameras[camIndex].PixelFormat.Value, true);
                        if (pixelFormat != PixelFormatEnums.Mono8)
                        {
                            pixelFormat = PixelFormatEnums.BGR8;
                        }
                        rawImage.ConvertToBitmapSource(pixelFormat, m_converted, ColorProcessingAlgorithm.HQ_LINEAR);

                        result = true;
                    }

                    rawImage.Dispose();
                }

                cameras[camIndex].CamManagedCamera.EndAcquisition();
                logger.Debug("Acquisition End");
            }
            catch (Exception ex)
            {
                IsCamAvailable = false;
                logger.Error("Acquisition error|" + ex.Message);
                result = false;
                m_converted.Dispose();

            }

            img = m_converted.bitmapsource.Clone();
            m_converted.Dispose();

            return result;
        }


        /// <summary>
        /// Print spinnaker SDK info
        /// </summary>
        private void PrintBuildInfo()
        {
            // Print out current library version
            LibraryVersion spinVersion = system.GetLibraryVersion();
            Console.WriteLine("Spinnaker library version: {0}.{1}.{2}.{3}\n\n",
                              spinVersion.major,
                              spinVersion.minor,
                              spinVersion.type,
                              spinVersion.build);

            StringBuilder newStr = new StringBuilder();
            newStr.AppendFormat(
                "Spinnaker library version: {0}.{1}.{2}.{3}\n\n",
                              spinVersion.major,
                              spinVersion.minor,
                              spinVersion.type,
                              spinVersion.build);

            Console.WriteLine(newStr);
            logger.Debug(newStr);
        }

        // This function prints the device information of the camera from the 
        // transport layer; please see NodeMapInfo_CSharp example for more 
        // in-depth comments on printing device information from the nodemap.
        private int PrintDeviceInfo(INodeMap nodeMap, int camNum)
        {
            int result = 0;
            StringBuilder newStr = new StringBuilder();
            try
            {
                newStr.AppendFormat("Printing device information for camera {0}...\n", camNum);

                ICategory category = nodeMap.GetNode<ICategory>("DeviceInformation");
                if (category != null && category.IsReadable)
                {
                    for (int i = 0; i < category.Children.Length; i++)
                    {
                        newStr.AppendFormat("{0}: {1}\n", category.Children[i].Name, (category.Children[i].IsReadable ? category.Children[i].ToString() : "Node not available"));
                    }
                }
                else
                {
                    newStr.Append("Device control information not available.\n");
                }
            }
            catch (SpinnakerException ex)
            {
                newStr.AppendFormat("Error: {0}\n", ex.Message);
                result = -1;
            }

            logger.Debug(newStr);
            return result;
        }

        /// <summary>
        /// Release cameras
        /// </summary>
        private void StopCameras()
        {
            try
            {
                foreach (IManagedCamera cam in cameras)
                {
                    // Put camera back to continous mode
                    cam.EndAcquisition();
                    cam.DeInit();
                }

                // Release system
                system.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error("Stop Cameras Error|" + ex.Message);
            }
        }

        private System.Windows.Input.ICommand _SaveCameraSettingsCommand;
        [JsonIgnore]
        public System.Windows.Input.ICommand SaveCameraSettingsCommand
        {
            get
            {
                if (_SaveCameraSettingsCommand == null)
                {
                    _SaveCameraSettingsCommand = new RelayCommand(
                        param => this.SaveCameraSettingsExecute(),
                        param => this.CanSaveCameraSettings()
                    );
                }
                return _SaveCameraSettingsCommand;
            }
        }
        private bool CanSaveCameraSettings()
        {
            return true;
        }
        private void SaveCameraSettingsExecute()
        {
            SaveCameraSettings();
            MessageBox.Show("已保存！！！\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "截图");

        }
    }


    #region CameraInformation

    /// <summary>
    /// Class for the camera enumerated 
    /// </summary>
    public class CameraInformation : NotifyPropertyChangedBase
    {
        public enum ErrorCodes
        {
            NO_ERROR = 0,
            INTERNAL_ERROR,
            INTERNAL_ERROR_READING_NODES,
            GAIN_OUT_OF_RANGE,
            EXPOSURE_OUT_OF_RANGE,
            TEMPERATURE_OUT_OF_RANGE,
            THRESHOLD_OUT_OF_RANGE,
            X_COORDINATE_OUT_OF_RANGE,
            Y_COORDINATE_OUT_OF_RANGE,
            PIXEL_SET_ALREADY_EXISTING,
            NO_DEFECTIVE_PIXELS_FOUND,
            LESS_THAN_255_DEFECTIVE_PIXELS_FOUND,
            TOO_MANY_DEFECTIVE_PIXELS
        }

        Logger logger = LogManager.GetCurrentClassLogger();
        // String parameters stored and camera pointer
        //private string _deviceSerialNumber;
        private IManagedCamera managedCamera;
        //private string _deviceModelName;
        //private string _deviceType;
        private IEnum _pixelFormat;

        //public string DeviceSerialNumber { get { return _deviceSerialNumber; } set { _deviceSerialNumber = value; } }
        //public string DeviceModelName { get { return _deviceModelName; } set { _deviceModelName = value; } }
        //public string DeviceType { get { return _deviceType; } set { _deviceType = value; } }
        [JsonIgnore]
        public IEnum PixelFormat { get { return _pixelFormat; } set { _pixelFormat = value; } }
        [JsonIgnore]
        public IManagedCamera CamManagedCamera { get { return managedCamera; } set { managedCamera = value; } }

        #region Property

        private string _deviceSerialNumber;
        public string DeviceSerialNumber
        {
            get
            {
                return _deviceSerialNumber;
            }

            set
            {
                if (value != _deviceSerialNumber)
                {
                    _deviceSerialNumber = value;
                    RaisePropertyChanged("DeviceSerialNumber");
                }
            }
        }

        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }

            set
            {
                if (value != _Name)
                {
                    _Name = value;
                    RaisePropertyChanged("Name");
                }
            }
        }

        private double _gain;
        public double Gain
        {
            get
            {
                return _gain;
            }

            set
            {
                if (value != _gain)
                {
                    _gain = value;
                    RaisePropertyChanged("Gain");
                }
            }
        }

        private double _exposureTime;
        public double ExposureTime
        {
            get
            {
                return _exposureTime;
            }

            set
            {
                if (value != _exposureTime)
                {
                    _exposureTime = value;
                    RaisePropertyChanged("ExposureTime");
                }
            }
        }

        private int _xOffset;
        public int xOffset
        {
            get
            {
                return _xOffset;
            }

            set
            {
                if (value != _xOffset)
                {
                    _xOffset = value;
                    RaisePropertyChanged("xOffset");
                }
            }
        }

        private int _yOffset;
        public int yOffset
        {
            get
            {
                return _yOffset;
            }

            set
            {
                if (value != _yOffset)
                {
                    _yOffset = value;
                    RaisePropertyChanged("yOffset");
                }
            }
        }

        private int _width;
        public int width
        {
            get
            {
                return _width;
            }

            set
            {
                if (value != _width)
                {
                    _width = value;
                    RaisePropertyChanged("width");
                }
            }
        }

        private int _height;
        public int height
        {
            get
            {
                return _height;
            }

            set
            {
                if (value != _height)
                {
                    _height = value;
                    RaisePropertyChanged("height");
                }
            }
        }

        /// <summary>
        /// PLC Connection
        /// </summary>
        private bool _IsConnected;
        [JsonIgnore]
        public bool IsConnected
        {
            get { return _IsConnected; }
            set { if (_IsConnected != value) { _IsConnected = value; RaisePropertyChanged("IsConnected"); } }
        }

        #endregion

        public CameraInformation()
        {
            // if these values are preset to -1
            // xOffset, yOffset will automatically set to be min image value (0, 0)
            // and width, height will automatically set to be maximum image size(e.g. 4000, 3000)
            xOffset = -1;
            yOffset = -1;
            width = -1;
            height = -1;
        }

        ~CameraInformation()
        { }

        #region Methods

        public ErrorCodes GetCamProperty()
        {
            // Retrieve GenICam nodemap
            INodeMap nodeMap = managedCamera.GetNodeMap();

            IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
            if (iGain == null || !iGain.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            Gain = iGain.Value;

            IFloat iExposureTime = nodeMap.GetNode<IFloat>("ExposureTime");
            if (iExposureTime == null || !iExposureTime.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            ExposureTime = iExposureTime.Value;

            // Retrieve the enumeration node from the nodemap
            IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
            if (iPixelFormat == null && !iPixelFormat.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            PixelFormat = iPixelFormat;
            logger.Debug("Pixelformat:" + iPixelFormat.Value);

            IInteger iOffsetX = nodeMap.GetNode<IInteger>("OffsetX");
            if (iOffsetX == null && !iOffsetX.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            xOffset = (int)iOffsetX.Value;
            logger.Debug("OffsetX:" + iOffsetX.Value);

            IInteger iOffsetY = nodeMap.GetNode<IInteger>("OffsetY");
            if (iOffsetY == null && !iOffsetX.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            yOffset = (int)iOffsetY.Value;
            logger.Debug("OffsetY:" + iOffsetY.Value);

            IInteger iWidth = nodeMap.GetNode<IInteger>("Width");
            if (iWidth == null && !iWidth.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            width = (int)iWidth.Value;
            logger.Debug("Width:" + iWidth.Value);

            IInteger iHeight = nodeMap.GetNode<IInteger>("Height");
            if (iHeight == null && !iHeight.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            height = (int)iHeight.Value;
            logger.Debug("Height:" + iHeight.Value);

            return ErrorCodes.NO_ERROR;
        }

        /// <summary>
        /// Set import camera properties
        /// </summary>
        /// <param name="gainToSet"></param>
        /// <param name="exposureTimeToSet"></param>
        /// <param name="xOffset">ROI x offset (do nothing if value equals -1)</param>
        /// <param name="yOffset">ROI y offset (do nothing if value equals -1)</param>
        /// <param name="Width">ROI width offset (do nothing if value equals -1)</param>
        /// <param name="Height">ROI height offset (do nothing if value equals -1)</param>
        /// <returns></returns>
        public ErrorCodes SetCamProperty(double gainToSet, double exposureTimeToSet)
        {
            logger.Debug("Setting property of camera {0} SN:{1}", this.Name, this.DeviceSerialNumber);

            // Retrieve GenICam nodemap
            INodeMap nodeMap = managedCamera.GetNodeMap();

            // Set gain and exposure time value on camera
            // Remove from automatic mode
            IEnum iGainAuto = nodeMap.GetNode<IEnum>("GainAuto");
            if (iGainAuto == null || !iGainAuto.IsWritable)
            {
                logger.Error("GainAuto node not available...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            IEnumEntry iGainAutoOff = iGainAuto.GetEntryByName("Off");
            if (iGainAutoOff == null || !iGainAutoOff.IsReadable)
            {
                logger.Error("GainAuto off not available...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            iGainAuto.Value = iGainAutoOff.Value;

            IFloat iGain = nodeMap.GetNode<IFloat>("Gain");
            if (iGain == null || !iGain.IsWritable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            // Set gain
            iGain.Value = gainToSet;
            logger.Debug("Gain set to {0}", iGain.Value);

            IEnum iExposureAuto = nodeMap.GetNode<IEnum>("ExposureAuto");
            if (iExposureAuto == null || !iExposureAuto.IsWritable)
            {
                logger.Error("ExposureAuto node not available...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            IEnumEntry iExposureAutoOff = iExposureAuto.GetEntryByName("Off");
            if (iExposureAutoOff == null || !iExposureAutoOff.IsReadable)
            {
                logger.Error("iExposureAuto not available...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            iExposureAuto.Value = iExposureAutoOff.Value;

            IFloat iExposureTime = nodeMap.GetNode<IFloat>("ExposureTime");
            if (iExposureTime == null || !iExposureTime.IsWritable)
            {
                logger.Error("ExposureTime node not available...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            // Set exposure time
            iExposureTime.Value = exposureTimeToSet;
            logger.Debug("ExposureTime set to {0}", iExposureTime.Value);

            //
            // Ensure trigger mode off
            //
            // *** NOTES ***
            // The trigger must be disabled in order to configure the
            // trigger source.
            //
            IEnum iTriggerMode = nodeMap.GetNode<IEnum>("TriggerMode");
            if (iTriggerMode == null || !iTriggerMode.IsWritable)
            {
                logger.Error("Unable to disable trigger mode (enum retrieval). Aborting...");
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            IEnumEntry iTriggerModeOff = iTriggerMode.GetEntryByName("Off");
            if (iTriggerModeOff == null || !iTriggerModeOff.IsReadable)
            {
                logger.Error("Unable to disable trigger mode (entry retrieval). Aborting...");
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            iTriggerMode.Value = iTriggerModeOff.Value;
            logger.Debug("Trigger mode disabled...");

            // Set acquisition mode
            IEnum iAcquisitionMode = nodeMap.GetNode<IEnum>("AcquisitionMode");
            if (iAcquisitionMode == null || !iAcquisitionMode.IsWritable)
            {
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                logger.Error("iAcquisitionMode not available...");
            }

            IEnumEntry iAcquisitionModeSingleFrame = iAcquisitionMode.GetEntryByName("SingleFrame");
            if (iAcquisitionModeSingleFrame == null || !iAcquisitionModeSingleFrame.IsReadable)
            {
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                logger.Error("iAcquisitionModeContinuous not available...");
            }

            IEnumEntry iAcquisitionModeContinuous = iAcquisitionMode.GetEntryByName("Continuous");
            if (iAcquisitionModeContinuous == null || !iAcquisitionModeContinuous.IsReadable)
            {
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
                logger.Error("iAcquisitionMode SingleFrame not available...");
            }

            //iAcquisitionMode.Value = iAcquisitionModeSingleFrame.Value;
            iAcquisitionMode.Value = iAcquisitionModeContinuous.Symbolic;

            // Retrieve the enumeration node from the nodemap
            IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
            if (iPixelFormat == null && !iPixelFormat.IsReadable)
            {
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            PixelFormat = iPixelFormat;
            logger.Debug("Pixelformat: {0}", iPixelFormat.Value);
            /*
            if (iPixelFormat != null && iPixelFormat.IsWritable)
            {
                // Retrieve the desired entry node from the enumeration node
                IEnumEntry iPixelFormatMono8 = iPixelFormat.GetEntryByName("Mono8");
                if (iPixelFormat != null && iPixelFormat.IsReadable)
                {
                    // Set value of entry node as new value for enumeration node
                    iPixelFormat.Value = iPixelFormatMono8.Value;

                    Console.WriteLine("Pixel format set to {0}...", iPixelFormat.Value.String);
                }
                else
                {
                    Console.WriteLine("Pixel format mono 8 not available...");
                }
            }
            else
            {
                Console.WriteLine("Pixel format not available...");
            }
            */


            // 
            // Apply minimum to offset X
            //
            // *** NOTES ***
            // Numeric nodes have both a minimum and maximum. A minimum is 
            // retrieved with the method GetMin(). Sometimes it can be 
            // important to check minimums to ensure that your desired value 
            // is within range.
            //
            IInteger iOffsetX = nodeMap.GetNode<IInteger>("OffsetX");
            if (iOffsetX != null && iOffsetX.IsWritable)
            {

                iOffsetX.Value = iOffsetX.Min;
                if (xOffset >= 0)
                {
                    if (xOffset <= iOffsetX.Max)
                    {
                        iOffsetX.Value = xOffset;

                    }
                    else
                    {
                        xOffset = (int)iOffsetX.Value;
                        MessageBox.Show("xOffset大于最大值！！！\n请确保ROI范围\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "警告");
                    }
                }
                logger.Debug("Offset X set to {0}", iOffsetX.Value);
            }
            else
            {
                logger.Error("Offset X not available...");
            }

            //
            // Apply minimum to offset Y
            // 
            // *** NOTES ***
            // It is often desirable to check the increment as well. The 
            // increment is a number of which a desired value must be a 
            // multiple. Certain nodes, such as those corresponding to 
            // offsets X and Y, have an increment of 1, which basically 
            // means that any value within range is appropriate. The 
            // increment is retrieved with the method GetInc().
            //
            IInteger iOffsetY = nodeMap.GetNode<IInteger>("OffsetY");
            if (iOffsetY != null && iOffsetY.IsWritable)
            {
                iOffsetY.Value = iOffsetY.Min;

                if (yOffset >= 0)
                {
                    if (yOffset <= iOffsetY.Max)
                    {
                        iOffsetY.Value = yOffset;

                    }
                    else
                    {
                        yOffset = (int)iOffsetY.Value;

                        MessageBox.Show("yOffset大于最大值！！！\n请确保ROI范围\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "警告");

                    }

                }
                logger.Debug("Offset Y set to {0}", iOffsetY.Value);
            }
            else
            {
                logger.Error("Offset Y not available...");
            }

            //
            // Set maximum width
            //
            // *** NOTES ***
            // Other nodes, such as those corresponding to image width and
            // height, might have an increment other than 1. In these cases, 
            // it can be important to check that the desired value is a 
            // multiple of the increment. However, as these values are being 
            // set to the maximum, there is no reason to check against the 
            // increment.
            //
            IInteger iWidth = nodeMap.GetNode<IInteger>("Width");
            if (iWidth != null && iWidth.IsWritable)
            {
                iWidth.Value = iWidth.Max;

                if (width >= 0)
                {
                    if (width <= iWidth.Max)
                    {
                        iWidth.Value = width;
                    }
                    else
                    {
                        width = (int)iWidth.Value;

                        MessageBox.Show("width大于最大值！！！\n请确保ROI范围\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "警告");

                    }
                }
                logger.Debug("Width set to {0}", iWidth.Value);
            }
            else
            {
                logger.Error("Width not available...");
            }

            //
            // Set maximum height
            //
            // *** NOTES ***
            // A maximum is retrieved with the method GetMax(). A node's 
            // minimum and maximum should always be a multiple of its
            // increment.
            //
            IInteger iHeight = nodeMap.GetNode<IInteger>("Height");
            if (iHeight != null && iHeight.IsWritable)
            {
                iHeight.Value = iHeight.Max;

                if (height >= 0)
                {
                    if (height <= iHeight.Max)
                    {
                        iHeight.Value = height;

                    }
                    else
                    {
                        height = (int)iHeight.Value;
                        MessageBox.Show("height大于最大值！！！\n请确保ROI范围\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "警告");

                    }
                }
                logger.Debug("Height set to {0}", iHeight.Max);
            }
            else
            {
                logger.Error("Height not available");
            }


            /////////////////////////// Stream buffer handling mode /////////////////////////
            // *********************************** IMPORTANT ************************************************
            // Default mode is Oldest First, Which will give you the oldest image when using getNextImage()
            // Set NewestFirst or NewestOnly, make sure we always get the lastest image
            // **********************************************************************************************
            // Retrieve Stream Parameters device nodemap
            INodeMap sNodeMap = managedCamera.GetTLStreamNodeMap();

            // Retrieve Buffer Handling Mode Information
            IEnum handlingMode = sNodeMap.GetNode<IEnum>("StreamBufferHandlingMode");
            if (handlingMode == null || !handlingMode.IsWritable)
            {
                logger.Error("Unable to set Buffer Handling mode (node retrieval). Aborting...");
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            IEnumEntry newestOnlyMode = handlingMode.GetEntryByName("NewestOnly");
            if (newestOnlyMode == null || !newestOnlyMode.IsReadable)
            {
                logger.Error("Unable to set Buffer Handling mode to NewestOnly. Aborting...");
                //return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }
            handlingMode.Value = newestOnlyMode.Value;
            logger.Debug("StreamBufferHandingMode set to NewestOnly");

            /*
            int numBuffers = 1;
            // Set stream buffer Count Mode to manual
            IEnum streamBufferCountMode = sNodeMap.GetNode<IEnum>("StreamBufferCountMode");
            if (streamBufferCountMode == null || !streamBufferCountMode.IsWritable)
            {
                Console.WriteLine("Unable to set Buffer Count Mode (node retrieval). Aborting...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            IEnumEntry streamBufferCountModeManual = streamBufferCountMode.GetEntryByName("Manual");
            if (streamBufferCountModeManual == null || !streamBufferCountModeManual.IsReadable)
            {
                Console.WriteLine("Unable to set Buffer Count Mode (entry retrieval). Aborting...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            streamBufferCountMode.Value = streamBufferCountModeManual.Value;

            Console.WriteLine("Stream Buffer Count Mode set to manual...");

            // Retrieve and modify Stream Buffer Count
            IInteger bufferCount = sNodeMap.GetNode<IInteger>("StreamBufferCountManual");
            if (bufferCount == null || !bufferCount.IsWritable)
            {
                Console.WriteLine("Unable to set Buffer Count (Integer node retrieval). Aborting...");
                return ErrorCodes.INTERNAL_ERROR_READING_NODES;
            }

            // Display buffer info
            Console.WriteLine("Default Buffer Count: {0}", bufferCount.Value);
            Console.WriteLine("Maximum Buffer Count: {0}", bufferCount.Max);

            bufferCount.Value = numBuffers;

            Console.WriteLine("Buffer count now set to : {0}", bufferCount.Value);     
            */

            return ErrorCodes.NO_ERROR;
        }

        #endregion

        #region RelayCommands

        /// <summary>
        /// SetCameraProperty command
        /// </summary>
        private System.Windows.Input.ICommand _SetCameraPropertyCommand;
        [JsonIgnore]
        public System.Windows.Input.ICommand SetCameraPropertyCommand
        {
            get
            {
                if (_SetCameraPropertyCommand == null)
                {
                    _SetCameraPropertyCommand = new RelayCommand(
                        param => this.SetCameraPropertyExecute(),
                        param => this.CanSetCameraProperty()
                    );
                }
                return _SetCameraPropertyCommand;
            }
        }
        private bool CanSetCameraProperty()
        {
            return true;
        }
        private void SetCameraPropertyExecute()
        {
            SetCamProperty(this.Gain, this.ExposureTime);
        }

        /// <summary>
        /// GetCameraProperty command
        /// </summary>
        private System.Windows.Input.ICommand _GetCameraPropertyCommand;
        [JsonIgnore]
        public System.Windows.Input.ICommand GetCameraPropertyCommand
        {
            get
            {
                if (_GetCameraPropertyCommand == null)
                {
                    _GetCameraPropertyCommand = new RelayCommand(
                        param => this.GetCameraPropertyExecute(),
                        param => this.CanGetCameraProperty()
                    );
                }
                return _GetCameraPropertyCommand;
            }
        }
        private bool CanGetCameraProperty()
        {
            return true;
        }
        private void GetCameraPropertyExecute()
        {
            GetCamProperty();
        }

        #endregion
    }

    #endregion


















}



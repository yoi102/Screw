using ModbusLib.Protocols;
using Newtonsoft.Json;
using NLog;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using Screw.BaseClasses;
using Screw.Model;
using Screw.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;



/*  PLC数据寄存器
   
D48: 转盘转一下置10，用来移位治具号和opid给油脂保存图片用
D49：ST2画像当前治具号,螺丝位置的治具号
D50：ST2画像扫描请求（0初始化、10开始、11OK、12NG）,螺丝位测量，10开始
D51：X轴0螺接offset_L，给plc用的，计算出的螺丝孔偏移量，L低位，H高位
D52：X轴0螺接offset_H
D53：Y轴1螺接offset_L
D54：Y轴1螺接offset_H
D59：点检触发（0初始化、10开始、11OK、12NG），点检，master两圆孔
D43----第一二滴的油脂检测结果，给plc
D44----第一二滴面的检测开始 10，给11表示处理完
D46----第三滴的油脂检测结果，给plc
D47----第三滴面检测开始 20      ，  给23表示获取图片完成，给21表示处理图片完成
现在没用D530：螺接时间
现在没用D531：螺接治具号
现在没用D532：启动信号（1开始存，0初始化）,原来读取串口用，10读取，读取完发11给plc

 */

namespace Screw.ViewModel
{

    class ViewModelMain : NotifyPropertyChangedBase
    {
        //油脂多量少量异常状态，寄存器
        private const ushort Drop1_TooLittle = 0b00;
        private const ushort Drop1_OK = 0b10;
        private const ushort Drop1_TooMuch = 0b01;
        private const ushort Drop1_Abnromal = 0b01;//函数异常时会判断为异常给plc，现在异常和多量同效果

        private const ushort Drop2_TooLittle = 0b0000;
        private const ushort Drop2_OK = 0b1000;
        private const ushort Drop2_TooMuch = 0b0100;
        private const ushort Drop2_Abnromal = 0b0100;

        private const ushort Drop3_TooLittle = 0b000000;
        private const ushort Drop3_OK = 0b100000;
        private const ushort Drop3_TooMuch = 0b010000;
        private const ushort Drop3_Abnromal = 0b010000;

        private MainWindow mainWindow;
        //计时器
        private DispatcherTimer clean_timer = null;
        //日志
        NLog.Logger logger = LogManager.GetCurrentClassLogger();
        //用于同步线程,读取完OPID之前阻塞线程用
        private AutoResetEvent _waitForResponse = new AutoResetEvent(false);
        public ViewModelMain(MainWindow mainWindow)
        {

            this.mainWindow = mainWindow;
            Device_IsConnected = true;
            LoadGreaseMorParaJsonData();
            LoadImageObjects();
            LoadPLCJsonData();
            LoadScrewMoParameterJsonData();
            LoadGloParaJsonData();
            LoadImageObjects();
            Lobe.SignatureFilePath = @"model_ONNX\signature.json";//训练好的文件地址
            DiskMg.GetDiskSpaceInfo();//更新磁盘信息。
            //初始值
            Drop3OPID = "0";
            Drop3Fixture = 0;
            Drop12OPID = "0";
            Drop12Fixture = 0;
            JudgeColorCircular = "NA";
            JudgeTextCircular = "NA";
            JudgeColor_TabletPredictor = "NA";
            JudgeText_TabletPredictor = "NA";
            //TestOrigin = false;
            Clean_files_flag = false;

            //ActHook.KeyDown += new System.Windows.Forms.KeyEventHandler(DivideKeyDownHandler);///////////EnterKeyDownHandler添加按键的响应//////////////////////////////////
            //ActHook.KeyDown += new System.Windows.Forms.KeyEventHandler(OemplusKeyDownHandler);///////////EnterKeyDownHandler添加按键的响应//////////////////////////////////

            VisibilitySettings = false;
            GloPara.DebugMode = false;
            NegativeJudge = JudgeType.NA;
            PositiveJudge = JudgeType.NA;
            Positive2Judge = JudgeType.NA;
            Dirs_init();
            //油脂
            for (int i = 0; i < 9; i++)
            {
                Run_status.Add(StatusType.wait);
            }
            InitTimer();//初始化计时器
            GreaseContinuousBadInit();
            if (GloPara.AutoStart)
            {
                PlcCtrl.Connect();
                SpinCtrl.InitCameras();
                Task.Run(() => MainThread());
            }
            GreaseContinuousBad = false;
        }

        private void MainThread()
        {
            try
            {
                while (true)
                {
                    if (PlcCtrl.PlcIsConnected && SpinCtrl.IsCamAvailable && !suspendRequested)//当清理文件时suspendRequested为true，暂停操作
                    {
                        if (!Device_IsConnected)
                        {
                            Device_IsConnected = true;
                        }
                        //读取50，50为10是，拍照测量圆孔偏移量等
                        ushort[] read50 = new ushort[1];
                        PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 50, 1, ref read50);
                        if (read50[0] == 10)
                        {
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 0 });//写0，防止再次开线程
                            Task.Run(() => ScrewStart());
                        }

                        //转盘转一下时。，用来读取数据
                        ushort[] read48 = new ushort[1];
                        PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 48, 1, ref read48);

                        if (read48[0] == 10)
                        {
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 48, 1, new ushort[] { 0 });//写0，防止再次开线程
                            Task.Run(() => ReadData());
                        }

                        //读59，10为点检Master
                        ushort[] read59 = new ushort[1];
                        PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 59, 1, ref read59);
                        if (read59[0] == 10)
                        {
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 59, 1, new ushort[] { 0 });//写0，防止再次开线程
                            Task.Run(() => MasterSpotCheckStart());
                        }

                        //开始反面信号，20第3滴面开始检测
                        ushort[] read47 = new ushort[1];
                        read47[0] = 0;
                        PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 47, 1, ref read47);
                        if (read47[0] == 20)
                        {
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 0 });//写0，防止再次开线程
                            Task.Run(() => Grease3Start());
                        }

                        //正面开始检测信号，第12滴面拍照开始检测
                        ushort[] read44 = new ushort[1];
                        read44[0] = 0;
                        PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 44, 1, ref read44);
                        if (read44[0] == 10)
                        {
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 0 });//写0，防止再次开线程
                            Task.Run(() => Grease12Start());
                        }

                    }
                    else//相机等没用连接时。
                    {
                        if (!suspendRequested)
                        {
                            Device_IsConnected = false;
                        }
                        if (!PlcCtrl.PlcIsConnected)
                        {
                            PlcCtrl.Connect();
                        }
                        if (!SpinCtrl.IsCamAvailable)
                        {
                            SpinCtrl.InitCameras();
                        }
                        Thread.Sleep(5000);

                    }

                }
            }
            catch (Exception ex)
            {
               
                logger.Error("MainThread| " + ex.Message);
                Thread.Sleep(5000);
                MainThread();
            }
        }






        ///////螺丝的

        #region Field
        //创建图片文件夹
        const string Folder_Logs_Error_logs = "logs\\Error_logs\\";
        const string Folder_Logs_Warn_logs = "logs\\Warn_logs\\";
        const string Folder_Logs_Info_logs = "logs\\Info_logs\\";
        const string Folder_Logs_Debug_logs = "logs\\Debug_logs\\";

        //文件夹
        private string Screw_error_images_dir { get; set; }
        private string Screw_motor_images_dir { get; set; }
        private string Screw_original_images_dir { get; set; }
        private string Screw_tablet_images_dir { get; set; }
        private string Drop1_images_dir { get; set; }
        private string Drop2_images_dir { get; set; }
        private string Drop3_images_dir { get; set; }
        private string Grease_error_images_dir { get; set; }
        private string Drop12_face_images_dir { get; set; }
        private string Drop3_face_images_dir { get; set; }

        // 创建Json文件
        private static readonly string CreateJsonFolder = "Config";
        private static readonly string PlcControlFile = CreateJsonFolder + "\\PlcSetting.config";
        private static readonly string LobeControlFile = CreateJsonFolder + "\\LobeSetting.config";
        //private static string SerialControlFile = CreateJsonFolder + "\\SerialSetting.config";
        private const string imageObjectsConfigFile = @"Config\ImageObjects.config";

        private static readonly string ScrewMorphologicalParametersFile = CreateJsonFolder + "\\ScrewPartParameters.config";
        private static readonly string GlobalParametersFile = CreateJsonFolder + "\\GlobalParametersSetting.config";
        //private const string OPIDFile = "D:\\OPID\\OPID.txt";




        /// <summary>
        /// Load PLC setting from Json file
        /// </summary>
        /// <returns></returns>
        private bool LoadPLCJsonData()
        {
            try
            {
                if (File.Exists(PlcControlFile))//Json文件影响单例模式，注意
                {
                    PlcControl tempPlcCtrl = JsonConvert.DeserializeObject<PlcControl>(File.ReadAllText(PlcControlFile), new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Formatting.Indented,
                        DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                        DateParseHandling = DateParseHandling.DateTime
                    });
                    PlcCtrl.Baudrate = tempPlcCtrl.Baudrate;
                    PlcCtrl.SlaveID = tempPlcCtrl.SlaveID;
                    PlcCtrl.Port = tempPlcCtrl.Port;
                    PlcCtrl.Add = tempPlcCtrl.Add;
                    PlcCtrl.Val = tempPlcCtrl.Val;
                    PlcCtrl.stopBits = tempPlcCtrl.stopBits;
                    PlcCtrl.dataBits = tempPlcCtrl.dataBits;
                    PlcCtrl.parity = tempPlcCtrl.parity;
                    tempPlcCtrl = null;

                }

                else
                {
                    Create_dir(CreateJsonFolder);
                    PlcCtrl = PlcControl.GetInstance();
                    PlcCtrl.Baudrate = 115200;
                    PlcCtrl.SlaveID = 2;
                    PlcCtrl.stopBits = StopBits.One;
                    PlcCtrl.parity = Parity.None;
                    PlcCtrl.dataBits = 8;
                    PlcCtrl.Port = "COM3";
                    PlcCtrl.Add = 50;
                    PlcCtrl.Val = 10;

                    SavePLCToJsonData();
                }
            }

            catch (Exception ex)
            {
                logger.Error("LoadPLCJsonData|  " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save PLC setting to file
        /// </summary>
        /// <returns></returns>
        private bool SavePLCToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(PlcControlFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, PlcCtrl, typeof(PlcControl));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("SavePLCToJsonData| " + ex.Message);

                return false;
            }
            return true;
        }

        /// <summary>
        /// Load PLC setting from Json file
        /// </summary>
        /// <returns></returns>
        private bool LoadGloParaJsonData()
        {
            try
            {
                if (File.Exists(GlobalParametersFile))
                {
                    GloPara = JsonConvert.DeserializeObject<GlobalParameters>(File.ReadAllText(GlobalParametersFile), new JsonSerializerSettings//修改parameters为自己需要存储的文件就OK？
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
                    Create_dir(CreateJsonFolder);

                    GloPara = new GlobalParameters();//不加这个容易报错

                    GloPara.MotorNG = 0;
                    GloPara.MotorNGRatio = 0;
                    GloPara.TotalMotor = 0;
                    GloPara.TabletNG = 0;
                    GloPara.TabletNGRatio = 0;
                    GloPara.TotalTablet = 0;
                    GloPara.TabletNGRatio360 = 0;

                    GloPara.LimitOriginXY = 2;
                    GloPara.OriginX = 1776.6022167997023;
                    GloPara.OriginY = 1423.7663675089955;
                    GloPara.XRatio = 0;//比例固定 需要改   
                    //GloPara.LatestOPID = "0";
                    //ScOPID = "0";

                    GloPara.Master1Angle = 0.00225822027646608;
                    //GloPara.Master2Angle = 0.00363299590711504;

                    GloPara.MasterDistanceX = 1800;

                    GloPara.CleanDay_short = 996;

                    SaveGloParaToJsonData();
                }
            }

            catch (Exception ex)
            {
                logger.Error("LoadGloParaJsonData|  " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save PLC setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveGloParaToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(GlobalParametersFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, GloPara, typeof(GlobalParameters));//修改parameters为自己需要存储的类的属性和命令
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("SaveGloParaToJsonData| " + ex.Message);

                return false;
            }
            return true;
        }

        ///// <summary>
        ///// Load Lobe Setting from file
        ///// </summary>
        ///// <returns></returns>
        //private bool LoadLobeJsonData()
        //{
        //    try
        //    {
        //        if (File.Exists(LobeControlFile))
        //        {
        //            LobePredictors = JsonConvert.DeserializeObject<ObservableCollection<LobePredictor>>(File.ReadAllText(LobeControlFile), new JsonSerializerSettings
        //            {
        //                NullValueHandling = NullValueHandling.Ignore,
        //                TypeNameHandling = TypeNameHandling.Auto,
        //                Formatting = Formatting.Indented,
        //                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
        //                DateParseHandling = DateParseHandling.DateTime
        //            });
        //        }

        //        else
        //        {
        //            create_dir(CreateJsonFolder);
        //            LobePredictors = new ObservableCollection<LobePredictor>();
        //            LobePredictors.Add(new LobePredictor() { Name = "Tablet_Predict", SignatureFilePath = @"model_ONNX\signature.json" }); //LobePredictors[0]调用？能指定Name？

        //            SaveLobeToJsonData();
        //        }
        //    }

        //    catch (Exception ex)
        //    {
        //        logger.Error("LoadLobeJsonData| " + ex.Message);
        //        return false;
        //    }

        //    return true;
        //}

        ///// <summary>
        ///// Save Lobe setting to file
        ///// </summary>
        ///// <returns></returns>
        //private bool SaveLobeToJsonData()
        //{
        //    try
        //    {
        //        JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
        //        serializer.NullValueHandling = NullValueHandling.Ignore;
        //        serializer.TypeNameHandling = TypeNameHandling.Auto;
        //        serializer.Formatting = Formatting.Indented;
        //        serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
        //        serializer.DateParseHandling = DateParseHandling.DateTime;

        //        using (StreamWriter sw = new StreamWriter(LobeControlFile))
        //        {
        //            using (JsonWriter writer = new JsonTextWriter(sw))
        //            {
        //                serializer.Serialize(writer, LobePredictors, typeof(ObservableCollection<LobePredictor>));//修改parameters为自己需要存储的类的属性和命令
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("SavelobeToJsonData| " + ex.Message);

        //        return false;
        //    }
        //    return true;
        //}



        /// <summary>
        /// Add image objects (this presently mannal)
        /// </summary>
        private void LoadImageObjects()
        {
            // read predict modules
            if (File.Exists(imageObjectsConfigFile))
            {
                ImageObjects = JsonConvert.DeserializeObject<ObservableCollection<ImageObject>>(File.ReadAllText(imageObjectsConfigFile), new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                    DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                    DateParseHandling = DateParseHandling.DateTime
                });
            }
            // write sample predict modules to config file
            if (ImageObjects == null || ImageObjects.Count == 0)
            {
                ImageObjects = new ObservableCollection<ImageObject>();

                // add image objects mannully
                ImageObjects.Add(new ImageObject()
                {
                    Name = "Canvas",
                    MarkSearchMode = ImageObject.SearchMode.ByEdge,
                    MarkCornerType = ImageObject.CornerType.TopLeft,
                    MarkLeft = 0,
                    MarkTop = 0,
                    MarkHeight = 100,
                    MarkWidth = 100
                });



                SaveImageOjbects();
            }

            // this is going to be changed into camera mapping in the future
            if (ImageObjects.Count > 0)
            {
                for (int i = 0; i < ImageObjects.Count; i++)
                {
                    ImageObjects[i].SourceCamIndex = i;
                }
            }

            // create saving folder and update image count
            //foreach (ImageObject imgObj in ImageObjects)
            //{
            //    System.IO.create_dir(imgObj.ImageSavingPath);
            //}
        }

        /// <summary>
        /// Save image objects to file
        /// </summary>
        private void SaveImageOjbects()
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            serializer.Formatting = Formatting.Indented;
            serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
            serializer.DateParseHandling = DateParseHandling.DateTime;

            using (StreamWriter sw = new StreamWriter(imageObjectsConfigFile))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, ImageObjects, typeof(ObservableCollection<ImageObject>));
                }
            }
        }




        /// <summary>
        /// Load Parameter Setting from file
        /// </summary>
        /// <returns></returns>
        private bool LoadScrewMoParameterJsonData()
        {
            try
            {
                if (File.Exists(ScrewMorphologicalParametersFile))
                {
                    ScrewPara = JsonConvert.DeserializeObject<ScrewPartParameters>(File.ReadAllText(ScrewMorphologicalParametersFile), new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Formatting.Indented,
                        DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                        DateParseHandling = DateParseHandling.DateTime
                    });
                }

                else
                {
                    Create_dir(CreateJsonFolder);//这里
                    ScrewPara = new ScrewPartParameters();
                    //op孔的 限制

                    ScrewPara.OpCircular_W = 115;
                    ScrewPara.OpCircularRangeLimit_W = 13;
                    //马达孔的限制

                    ScrewPara.MotorCircularThresh = 165;

                    ScrewPara.MotorCircular_W = 144;
                    ScrewPara.MotorCircularRangLimit_W = 30;
                    //方的限制
                    //ScrewPara.ScrewDatumPointBlob_A = 100477;
                    //ScrewPara.ScrewDatumPointBlobRange_A = 8000;
                    //ScrewPara.ScrewDatumPointBlobThresh = 180;
                    //ScrewPara.ScrewDatumPointBlob_H = 240;
                    //ScrewPara.ScrewDatumPointBlobRange_H = 80;
                    //ScrewPara.ScrewDatumPointBlob_W = 445;
                    //ScrewPara.ScrewDatumPointBlobRange_W = 60;
                    //好像没用了
                    ScrewPara.MasterCircularThresh = 138;
                    //ScrewPara.MasterCircular_H = 337;
                    //ScrewPara.MasterCircularRange_H = 30;
                    ScrewPara.MasterCircular_W = 337;
                    ScrewPara.MasterCircularRange_W = 30;
                    //ScrewPara.MasterCircular_A = 86145;
                    //ScrewPara.MasterCircularRange_A = 5000;

                    //ScrewPara.CirWHLimit = 8;//小圆的WH差的限制
                    ScrewPara.DeltaXYLimit = 13;//两圆心XY的限制

                    SaveScrewMoParameterToJsonData();
                }
            }

            catch (Exception ex)
            {
                logger.Error("LoadScrewMoParameterJsonData| " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save MoParameter setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveScrewMoParameterToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(ScrewMorphologicalParametersFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, ScrewPara, typeof(ScrewPartParameters));//修改parameters为自己需要存储的类的属性和命令
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("SaveScrewMoParameterToJsonData| " + ex.Message);

                return false;
            }
            return true;
        }




        //private bool LoadSerialJsonData()
        //{
        //    try
        //    {
        //        if (File.Exists(SerialControlFile))
        //        {
        //            SerialCtrl = JsonConvert.DeserializeObject<SerialControl>(File.ReadAllText(SerialControlFile), new JsonSerializerSettings
        //            {
        //                NullValueHandling = NullValueHandling.Ignore,
        //                TypeNameHandling = TypeNameHandling.Auto,
        //                Formatting = Formatting.Indented,
        //                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
        //                DateParseHandling = DateParseHandling.DateTime
        //            });
        //        }

        //        else
        //        {
        //            create_dir(CreateJsonFolder);
        //            SerialCtrl = new SerialControl();
        //            SerialCtrl.baudRate = 38400;
        //            SerialCtrl.comPort = "COM7";
        //            SerialCtrl.stopBits = StopBits.One;
        //            SerialCtrl.parity = Parity.None;
        //            SerialCtrl.sendData = "SR,01,519";
        //            SerialCtrl.dataBits = 8;

        //            SaveSerialToJsonData();
        //        }
        //    }

        //    catch (Exception ex)
        //    {
        //        logger.Error("LoadLobeJsonData| " + ex.Message);
        //        return false;
        //    }

        //    return true;
        //}


        //private bool SaveSerialToJsonData()
        //{
        //    try
        //    {
        //        JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
        //        serializer.NullValueHandling = NullValueHandling.Ignore;
        //        serializer.TypeNameHandling = TypeNameHandling.Auto;
        //        serializer.Formatting = Formatting.Indented;
        //        serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
        //        serializer.DateParseHandling = DateParseHandling.DateTime;

        //        using (StreamWriter sw = new StreamWriter(SerialControlFile))
        //        {
        //            using (JsonWriter writer = new JsonTextWriter(sw))
        //            {
        //                serializer.Serialize(writer, SerialCtrl, typeof(SerialControl));//修改parameters为自己需要存储的类的属性和命令
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("SavelobeToJsonData| " + ex.Message);

        //        return false;
        //    }
        //    return true;
        //}



        #endregion

        #region Property
        /// <summary>
        /// 打螺丝位OPID
        /// </summary>
        public string ScOPID { get; set; }
        /// <summary>
        /// 打螺丝位治具号
        /// </summary>
        public ushort ScFixture { get; set; }
        /// <summary>
        /// 打螺丝位X偏移量
        /// </summary>
        public double ShiftXsc { get; set; }
        /// <summary>
        /// 打螺丝位Y偏移量
        /// </summary>
        public double ShiftYsc { get; set; }
        /// <summary>
        /// 暂停线程用
        /// </summary>
        public volatile bool suspendRequested = false;
        /// <summary>
        /// 第三滴保存图片用 OPID
        /// </summary>
        private string Drop3OPID { get; set; }
        /// <summary>
        /// //第三滴用，治具号
        /// </summary>
        private ushort Drop3Fixture { get; set; }
        /// <summary>
        /// //第1，2滴用OPID号
        /// </summary>
        private string Drop12OPID { get; set; }
        /// <summary>
        /// //第1，2滴用，治具号
        /// </summary>
        private ushort Drop12Fixture { get; set; }
        /// <summary>
        /// 使用本地图片用的图片地址
        /// </summary>
        private string ImagePath { get; set; }

        private bool _clean_files_flag;
        /// <summary>
        /// 清理文件时的flag，用来显示弹窗
        /// </summary>
        public bool Clean_files_flag
        {
            get { return _clean_files_flag; }
            set
            {
                if (value != _clean_files_flag)
                {
                    _clean_files_flag = value;
                    RaisePropertyChanged("Clean_files_flag");
                }
            }
        }

        private bool _device_IsConnected;
        /// <summary>
        /// 设备是否连接，用来显示弹窗
        /// </summary>
        public bool Device_IsConnected
        {
            get { return _device_IsConnected; }
            set
            {
                if (value != _device_IsConnected)
                {
                    _device_IsConnected = value;
                    RaisePropertyChanged("Device_IsConnected");
                }
            }
        }

        //private bool _TestOrigin;
        ///// <summary>
        ///// 用来是否测试Master孔（点检Master时用到），
        ///// </summary>
        //public bool TestOrigin
        //{
        //    get { return _TestOrigin; }
        //    set
        //    {
        //        if (value != _TestOrigin)
        //        {
        //            _TestOrigin = value;
        //            RaisePropertyChanged("TestOrigin");
        //        }
        //    }
        //}


        private string _TabletLabel;
        /// <summary>
        /// 预测压杆的OKNG，用来记录表格
        /// </summary>
        public string TabletLabel
        {
            get { return _TabletLabel; }
            set
            {
                if (value != _TabletLabel)
                {
                    _TabletLabel = value;
                    RaisePropertyChanged("TabletLabel");
                }
            }
        }
        private double _TabletConfidence;
        /// <summary>
        /// 预测压杆的OKNG的概率，用来记录表格
        /// </summary>
        public double TabletConfidence
        {
            get { return _TabletConfidence; }
            set
            {
                if (value != _TabletConfidence)
                {
                    _TabletConfidence = value;
                    RaisePropertyChanged("TabletConfidence");
                }
            }
        }

        private bool _RepairMode;
        /// <summary>
        /// 是否开启维修模式， 当维修模式时，将不检测螺丝孔位置
        /// </summary>
        public bool RepairMode
        {
            get { return _RepairMode; }
            set { if (_RepairMode != value) { _RepairMode = value; RaisePropertyChanged("RepairMode"); } }
        }
        //private bool _VisibilityAll;
        //public bool VisibilityAll
        //{
        //    get { return _VisibilityAll; }
        //    set { if (_VisibilityAll != value) { _VisibilityAll = value; RaisePropertyChanged("VisibilityAll"); } }
        //}
        //private bool _VisibilityMotor;
        //public bool VisibilityMotor
        //{
        //    get { return _VisibilityMotor; }
        //    set { if (_VisibilityMotor != value) { _VisibilityMotor = value; RaisePropertyChanged("VisibilityMotor"); } }
        //}
        //private bool _VisibilityTablet;
        //public bool VisibilityTablet
        //{
        //    get { return _VisibilityTablet; }
        //    set { if (_VisibilityTablet != value) { _VisibilityTablet = value; RaisePropertyChanged("VisibilityTablet"); } }
        //}
        //private bool _VisibilityCanvas;
        //public bool VisibilityCanvas
        //{
        //    get { return _VisibilityCanvas; }
        //    set { if (_VisibilityCanvas != value) { _VisibilityCanvas = value; RaisePropertyChanged("VisibilityCanvas"); } }
        //}

        //private bool _VisibilityCircular;
        //public bool VisibilityCircular
        //{
        //    get { return _VisibilityCircular; }
        //    set { if (_VisibilityCircular != value) { _VisibilityCircular = value; RaisePropertyChanged("VisibilityCircular"); } }
        //}
        //private bool _VisibilityCloseCircular;
        //public bool VisibilityCloseCircular
        //{
        //    get { return _VisibilityCloseCircular; }
        //    set { if (_VisibilityCloseCircular != value) { _VisibilityCloseCircular = value; RaisePropertyChanged("VisibilityCloseCircular"); } }
        //}

        //private bool _VisibilityCloseMotorCircular;
        //public bool VisibilityCloseMotorCircular
        //{
        //    get { return _VisibilityCloseMotorCircular; }
        //    set { if (_VisibilityCloseMotorCircular != value) { _VisibilityCloseMotorCircular = value; RaisePropertyChanged("VisibilityCloseMotorCircular"); } }
        //}

        private bool _VisibilitySettings;
        /// <summary>
        /// 是否显示设置ui区域，输入密码后可看到设置面板
        /// </summary>
        public bool VisibilitySettings
        {
            get { return _VisibilitySettings; }
            set { if (_VisibilitySettings != value) { _VisibilitySettings = value; RaisePropertyChanged("VisibilitySettings"); } }
        }



        private int _DatumPoint_X;
        /// <summary>
        /// 螺丝面，定位点的x
        /// </summary>
        public int DatumPoint_X
        {
            get { return _DatumPoint_X; }
            set { if (_DatumPoint_X != value) { _DatumPoint_X = value; RaisePropertyChanged("DatumPoint_X"); } }
        }

        private int _DatumPoint_Y;
        /// <summary>
        /// 螺丝面，定位点的Y
        /// </summary>
        public int DatumPoint_Y
        {
            get { return _DatumPoint_Y; }
            set { if (_DatumPoint_Y != value) { _DatumPoint_Y = value; RaisePropertyChanged("DatumPoint_Y"); } }
        }

        private int _RecW;
        /// <summary>
        /// 螺丝面，定位物体大小，用于记录
        /// </summary>
        public int RecW
        {
            get { return _RecW; }
            set { if (_RecW != value) { _RecW = value; RaisePropertyChanged("RecW"); } }
        }
        private int _RecH;
        /// <summary>
        /// 螺丝面，定位物体大小，用于记录
        /// </summary>
        public int RecH
        {
            get { return _RecH; }
            set { if (_RecH != value) { _RecH = value; RaisePropertyChanged("RecH"); } }
        }
        private int _RecA;
        /// <summary>
        /// 螺丝面，定位物体大小，用于记录
        /// </summary>
        public int RecA
        {
            get { return _RecA; }
            set { if (_RecA != value) { _RecA = value; RaisePropertyChanged("RecA"); } }
        }

        //private int _OpCircularWidth;
        ///// <summary>
        ///// 测量出来的op架小圆孔的大小，用来记录
        ///// </summary>
        //public int OpCircularWidth
        //{
        //    get { return _OpCircularWidth; }
        //    set { if (_OpCircularWidth != value) { _OpCircularWidth = value; RaisePropertyChanged("OpCircularWidth"); } }
        //}
        //private int _OpCircularHeight;
        ///// <summary>
        ///// 测量出来的op架小圆孔的大小，用来记录
        ///// </summary>
        //public int OpCircularHeight
        //{
        //    get { return _OpCircularHeight; }
        //    set { if (_OpCircularHeight != value) { _OpCircularHeight = value; RaisePropertyChanged("OpCircularHeight"); } }
        //}
        //private int _OpCircularArea;
        ///// <summary>
        ///// 测量出来的op架小圆孔的大小，用来记录
        ///// </summary>
        //public int OpCircularArea
        //{
        //    get { return _OpCircularArea; }
        //    set { if (_OpCircularArea != value) { _OpCircularArea = value; RaisePropertyChanged("OpCircularArea"); } }
        //}

        //private int _MotorCirW;//大圆宽高面积
        ///// <summary>
        ///// 测量出来的马达钣金圆孔的大小，用来记录
        ///// </summary>
        //public int MotorCirW
        //{
        //    get { return _MotorCirW; }
        //    set { if (_MotorCirW != value) { _MotorCirW = value; RaisePropertyChanged("MotorCirW"); } }
        //}
        //private int _MotorCirH;
        ///// <summary>
        ///// 测量出来的马达钣金圆孔的大小，用来记录
        ///// </summary>
        //public int MotorCirH
        //{
        //    get { return _MotorCirH; }
        //    set { if (_MotorCirH != value) { _MotorCirH = value; RaisePropertyChanged("MotorCirH"); } }
        //}
        //private int _MotorCirA;
        ///// <summary>
        ///// 测量出来的马达钣金圆孔的大小，用来记录
        ///// </summary>
        //public int MotorCirA
        //{
        //    get { return _MotorCirA; }
        //    set { if (_MotorCirA != value) { _MotorCirA = value; RaisePropertyChanged("MotorCirA"); } }
        //}

        //private int _FilteredCount;

        //public int FilteredCount
        //{
        //    get { return _FilteredCount; }
        //    set { if (_FilteredCount != value) { _FilteredCount = value; RaisePropertyChanged("FilteredCount"); } }
        //}

        private ushort _Read49;
        /// <summary>
        /// 治具号，用来记录
        /// </summary>
        public ushort Read49
        {
            get { return _Read49; }
            set { if (_Read49 != value) { _Read49 = value; RaisePropertyChanged("Read49"); } }
        }
        //private ushort _LatestRead49;
        //public ushort LatestRead49
        //{
        //    get { return _LatestRead49; }
        //    set { if (_LatestRead49 != value) { _LatestRead49 = value; RaisePropertyChanged("LatestRead49"); } }
        //}

        private string _OPID;
        /// <summary>
        /// OPID号，用来记录
        /// </summary>
        public string OPID
        {
            get { return _OPID; }
            set { if (_OPID != value) { _OPID = value; RaisePropertyChanged("OPID"); } }
        }
        private string _LatestOPID;
        /// <summary>
        /// 读取到的OPID号，用来防止推移队列时出错
        /// </summary>
        public string LatestOPID
        {
            get { return _LatestOPID; }
            set { if (_LatestOPID != value) { _LatestOPID = value; RaisePropertyChanged("LatestOPID"); } }
        }



        private string _Password;
        /// <summary>
        /// 输入的密码
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                if (_Password != value)
                {
                    _Password = value;
                    RaisePropertyChanged("Password");
                }
            }
        }

        private string _JudgeColorCircular;
        public string JudgeColorCircular
        {
            get { return _JudgeColorCircular; }
            set { if (_JudgeColorCircular != value) { _JudgeColorCircular = value; RaisePropertyChanged("JudgeColorCircular"); } }
        }
        private string _JudgeColor_TabletPredictor;
        public string JudgeColor_TabletPredictor
        {
            get { return _JudgeColor_TabletPredictor; }
            set { if (_JudgeColor_TabletPredictor != value) { _JudgeColor_TabletPredictor = value; RaisePropertyChanged("JudgeColor_TabletPredictor"); } }
        }
        private string _JudgeTextCircular;
        public string JudgeTextCircular
        {
            get { return _JudgeTextCircular; }
            set { if (_JudgeTextCircular != value) { _JudgeTextCircular = value; RaisePropertyChanged("JudgeTextCircular"); } }
        }
        private string _JudgeText_TabletPredictor;
        public string JudgeText_TabletPredictor
        {
            get { return _JudgeText_TabletPredictor; }
            set { if (_JudgeText_TabletPredictor != value) { _JudgeText_TabletPredictor = value; RaisePropertyChanged("JudgeText_TabletPredictor"); } }
        }
        private double _OpCircularCentroidX;//小圆中心点
        /// <summary>
        /// OP架圆孔的像素质心位置
        /// </summary>
        public double OpCircularCentroidX
        {
            get { return _OpCircularCentroidX; }
            set { if (_OpCircularCentroidX != value) { _OpCircularCentroidX = value; RaisePropertyChanged("OpCircularCentroidX"); } }
        }
        private double _OpCircularCentroidY;
        /// <summary>
        /// OP架圆孔的像素质心位置
        /// </summary>
        public double OpCircularCentroidY
        {
            get { return _OpCircularCentroidY; }
            set { if (_OpCircularCentroidY != value) { _OpCircularCentroidY = value; RaisePropertyChanged("OpCircularCentroidY"); } }
        }
        private double _MotorCircularCentroidX;//大圆中心点
        /// <summary>
        /// 马达钣金圆孔的像素质心位置
        /// </summary>
        public double MotorCircularCentroidX
        {
            get { return _MotorCircularCentroidX; }
            set { if (_MotorCircularCentroidX != value) { _MotorCircularCentroidX = value; RaisePropertyChanged("MotorCircularCentroidX"); } }
        }
        private double _MotorCircularCentroidY;
        /// <summary>
        /// 马达钣金圆孔的像素质心位置
        /// </summary>
        public double MotorCircularCentroidY
        {
            get { return _MotorCircularCentroidY; }
            set { if (_MotorCircularCentroidY != value) { _MotorCircularCentroidY = value; RaisePropertyChanged("MotorCircularCentroidY"); } }
        }


        private double _MotorOpCentroidDeltaX;
        /// <summary>
        /// 用来记录，op和马达钣金圆孔的差值
        /// </summary>
        public double MotorOpCentroidDeltaX
        {
            get { return _MotorOpCentroidDeltaX; }
            set { if (_MotorOpCentroidDeltaX != value) { _MotorOpCentroidDeltaX = value; RaisePropertyChanged("MotorOpCentroidDeltaX"); } }
        }
        private double _MotorOpCentroidDeltaY;
        /// <summary>
        /// 用来记录，op和马达钣金圆孔的差值
        /// </summary>
        public double MotorOpCentroidDeltaY
        {
            get { return _MotorOpCentroidDeltaY; }
            set { if (_MotorOpCentroidDeltaY != value) { _MotorOpCentroidDeltaY = value; RaisePropertyChanged("MotorOpCentroidDeltaY"); } }
        }



        private double _ShiftX;
        /// <summary>
        /// 计算出来的OP架圆孔的偏移量，给PLC用
        /// </summary>
        public double ShiftX
        {
            get { return _ShiftX; }
            set { if (_ShiftX != value) { _ShiftX = value; RaisePropertyChanged("ShiftX"); } }
        }
        private double _ShiftY;
        /// <summary>
        /// 计算出来的OP架圆孔的偏移量，给PLC用
        /// </summary>
        public double ShiftY
        {
            get { return _ShiftY; }
            set { if (_ShiftY != value) { _ShiftY = value; RaisePropertyChanged("ShiftY"); } }
        }

        private double _CorrectOrigin_X;
        /// <summary>
        /// Master的螺丝位置孔的质心
        /// </summary>
        public double CorrectOrigin_X
        {
            get { return _CorrectOrigin_X; }
            set { if (_CorrectOrigin_X != value) { _CorrectOrigin_X = value; RaisePropertyChanged("CorrectOrigin_X"); } }
        }
        private double _CorrectOrigin_Y;
        /// <summary>
        /// Master的螺丝位置孔的质心
        /// </summary>
        public double CorrectOrigin_Y
        {
            get { return _CorrectOrigin_Y; }
            set { if (_CorrectOrigin_Y != value) { _CorrectOrigin_Y = value; RaisePropertyChanged("CorrectOrigin_Y"); } }
        }

        private double _TestHole_X;
        /// <summary>
        /// Master的第二个孔的质心
        /// </summary>
        public double TestHole_X
        {
            get { return _TestHole_X; }
            set { if (_TestHole_X != value) { _TestHole_X = value; RaisePropertyChanged("TestHole_X"); } }
        }
        private double _TestHole_Y;
        /// <summary>
        /// Master的第二个孔的质心
        /// </summary>
        public double TestHole_Y
        {
            get { return _TestHole_Y; }
            set { if (_TestHole_Y != value) { _TestHole_Y = value; RaisePropertyChanged("TestHole_Y"); } }
        }



        private BitmapSource _SourceImage;
        public BitmapSource SourceImage
        {
            get { return _SourceImage; }
            set
            {
                if (value != _SourceImage)
                {
                    _SourceImage = value;
                    _SourceImage.Freeze();
                    RaisePropertyChanged("SourceImage");
                }
            }
        }

        private BitmapSource _DisplayMotorImage;
        public BitmapSource DisplayMotorImage
        {
            get { return _DisplayMotorImage; }
            set
            {
                if (value != _DisplayMotorImage)
                {
                    _DisplayMotorImage = value;
                    _DisplayMotorImage.Freeze();
                    RaisePropertyChanged("DisplayMotorImage");
                }
            }
        }
        private BitmapSource _DisplayTabletImage;
        public BitmapSource DisplayTabletImage
        {
            get { return _DisplayTabletImage; }
            set
            {
                if (value != _DisplayTabletImage)
                {
                    _DisplayTabletImage = value;
                    _DisplayTabletImage.Freeze();
                    RaisePropertyChanged("DisplayTabletImage");
                }
            }
        }

        private BitmapSource _DisplayCircularImage;
        public BitmapSource DisplayCircularImage
        {
            get { return _DisplayCircularImage; }
            set
            {
                if (value != _DisplayCircularImage)
                {
                    _DisplayCircularImage = value;
                    _DisplayCircularImage.Freeze();
                    RaisePropertyChanged("DisplayCircularImage");
                }
            }
        }


        private BitmapSource _DisplayCloseCircularImage;
        public BitmapSource DisplayCloseCircularImage
        {
            get { return _DisplayCloseCircularImage; }
            set
            {
                if (value != _DisplayCloseCircularImage)
                {
                    _DisplayCloseCircularImage = value;
                    _DisplayCloseCircularImage.Freeze();
                    RaisePropertyChanged("DisplayCloseCircularImage");
                }
            }
        }
        private BitmapSource _DisplayCloseMotorCircularImage;
        public BitmapSource DisplayCloseMotorCircularImage
        {
            get { return _DisplayCloseMotorCircularImage; }
            set
            {
                if (value != _DisplayCloseMotorCircularImage)
                {
                    _DisplayCloseMotorCircularImage = value;
                    _DisplayCloseMotorCircularImage.Freeze();
                    RaisePropertyChanged("DisplayCloseMotorCircularImage");
                }
            }
        }

        private BitmapSource _LocalImage;
        public BitmapSource LocalImage
        {
            get { return _LocalImage; }
            set
            {
                if (value != _LocalImage)
                {
                    _LocalImage = value;
                    _LocalImage.Freeze();
                    RaisePropertyChanged("LocalImage");
                }
            }
        }

        private BitmapSource _DisplayRoi_DatumPointRange;
        public BitmapSource DisplayRoi_DatumPointRange
        {
            get { return _DisplayRoi_DatumPointRange; }
            set
            {
                if (value != _DisplayRoi_DatumPointRange)
                {
                    _DisplayRoi_DatumPointRange = value;
                    _DisplayRoi_DatumPointRange.Freeze();
                    RaisePropertyChanged("DisplayRoi_DatumPointRange");
                }
            }
        }

        private PlcControl _PlcCtrl = PlcControl.GetInstance();
        public PlcControl PlcCtrl
        {
            get { return _PlcCtrl; }
            set { if (_PlcCtrl != value) { _PlcCtrl = value; RaisePropertyChanged("PlcCtrl"); } }
        }

        private DiskManage _DiskMg = new DiskManage();
        public DiskManage DiskMg
        {
            get { return _DiskMg; }
            set { if (_DiskMg != value) { _DiskMg = value; RaisePropertyChanged("DiskMg"); } }
        }
        private ObservableCollection<LobePredictor> _LobePredictors = new ObservableCollection<LobePredictor>();
        public ObservableCollection<LobePredictor> LobePredictors
        {
            get { return _LobePredictors; }
            set { if (_LobePredictors != value) { _LobePredictors = value; RaisePropertyChanged("LobePredictors"); } }
        }

        private SpinnakerControl _SpinCtrl = new SpinnakerControl();
        public SpinnakerControl SpinCtrl
        {
            get { return _SpinCtrl; }
            set { if (value != _SpinCtrl) { _SpinCtrl = value; RaisePropertyChanged("SpinCtrl"); } }
        }
        private ScrewPartParameters _ScrewPara = new ScrewPartParameters();
        public ScrewPartParameters ScrewPara
        {
            get { return _ScrewPara; }
            set { if (value != _ScrewPara) { _ScrewPara = value; RaisePropertyChanged("ScrewPara"); } }
        }
        private GlobalParameters _GloPara = new GlobalParameters();
        public GlobalParameters GloPara
        {
            get { return _GloPara; }
            set { if (value != _GloPara) { _GloPara = value; RaisePropertyChanged("GloPara"); } }
        }
        private LobePredictor _Lobe = new LobePredictor();
        public LobePredictor Lobe
        {
            get { return _Lobe; }
            set { if (value != _Lobe) { _Lobe = value; } }
        }

        /// <summary>
        /// Collection to store image objects
        /// </summary>
        private ObservableCollection<ImageObject> _ImageObjects = new ObservableCollection<ImageObject>();
        public ObservableCollection<ImageObject> ImageObjects
        {
            get { return _ImageObjects; }
            set { if (_ImageObjects != value) { _ImageObjects = value; RaisePropertyChanged("ImageObjects"); } }
        }

        #endregion

        #region Method

        #region 初始化
        private void InitTimer()
        {
            if (clean_timer == null)
            {
                clean_timer = new DispatcherTimer();
                clean_timer.Tick += Timer_Elapsed;
                clean_timer.Interval = TimeSpan.FromMilliseconds(1000 * 60);
                clean_timer.Start();
            }
        }


        /// <summary>
        /// 创建文件夹
        /// </summary>
        private void Dirs_init()
        {
            try
            {
                Screw_error_images_dir = GloPara.Header_folder + "\\" + "Images\\Screw\\Error_Images\\";
                Screw_motor_images_dir = GloPara.Header_folder + "\\" + "Images\\Screw\\Motor_Images\\";
                Screw_original_images_dir = GloPara.Header_folder + "\\" + "Images\\Screw\\Original_Images\\";
                Screw_tablet_images_dir = GloPara.Header_folder + "\\" + "Images\\Screw\\Tablet_Images\\";

                Drop1_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Drop1_Images\\";
                Drop2_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Drop2_Images\\";
                Drop3_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Drop3_Images\\";

                Grease_error_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Error_Images\\";
                Drop12_face_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Drop12_Face_Images\\";
                Drop3_face_images_dir = GloPara.Header_folder + "\\" + "Images\\Grease\\Drop3_Face_Images\\";

                Create_dir(Screw_error_images_dir);
                Create_dir(Screw_motor_images_dir);
                Create_dir(Screw_original_images_dir);
                Create_dir(Screw_tablet_images_dir);
                Create_dir(Drop1_images_dir);
                Create_dir(Drop2_images_dir);
                Create_dir(Drop3_images_dir);
                Create_dir(Grease_error_images_dir);
                Create_dir(Drop12_face_images_dir);
                Create_dir(Drop3_face_images_dir);

            }
            catch (Exception ex)
            {
                logger.Error("create_dirs|_" + ex);
            }
        }


        private void Timer_Elapsed(object sender, EventArgs e)
        {
            //logger.Info("Occupied_memory:_" + get_memory());
            //logger.Info("Available_memory:_" + get_available_memory());
            //logger.Debug("Capacity_memory:_" + get_sum_memory());
            //logger.Warn("222");
            GC.Collect();
            Clean_files();

            if ((DateTime.Now.Hour == 8 && DateTime.Now.Minute == 0) ||
                (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 0))  //如果当前时间是10点30分
            {
                CleanCount();
            }
        }
        #endregion

        #region 运行的模式




        /// <summary>
        /// 找基准点--重要
        /// </summary>
        private void Screw_DatumPositionMeasure(BitmapSource bitmap)
        {
            try
            {
                Run_status[0] = StatusType.running;
                PropertyInit();
                if (ScrewPara.Roi_DatumPointRange_X + ScrewPara.Roi_DatumPointRange_W > bitmap.Width)
                {
                    ScrewPara.Roi_DatumPointRange_W = (int)bitmap.Width - ScrewPara.Roi_DatumPointRange_X;
                }
                if (ScrewPara.Roi_DatumPointRange_Y + ScrewPara.Roi_DatumPointRange_H > bitmap.Height)
                {
                    ScrewPara.Roi_DatumPointRange_H = (int)bitmap.Height - ScrewPara.Roi_DatumPointRange_Y;
                }
                Rect datumPointRange = new Rect(ScrewPara.Roi_DatumPointRange_X, ScrewPara.Roi_DatumPointRange_Y, ScrewPara.Roi_DatumPointRange_W, ScrewPara.Roi_DatumPointRange_H);
                using (Mat roi_DatumPointRange = new Mat(bitmap.ToMat(), datumPointRange))
                using (Mat closingImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(roi_DatumPointRange, closingImage, 5);
                    Cv2.CvtColor(closingImage, closingImage, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(closingImage, closingImage, ScrewPara.ScrewDatumPointBlobThresh, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(closingImage, closingImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(closingImage, closingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    var rr = Cv2.ConnectedComponentsEx(closingImage);
                    DisplayRoi_DatumPointRange = closingImage.ToBitmapSource();
                    ConnectedComponents.Blob max_blob = null;
                    MaxAreaBlob(closingImage, ref max_blob);

                    if (max_blob == null)
                    {
                        DatumPoint_X = 0;
                        DatumPoint_Y = 0;
                        logger.Error("Screw_DatumPositionMeasure----max_blob==null： ");
                        string screw_error_images_path = Screw_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                        Create_dir(screw_error_images_path);
                        Cv2.ImWrite(screw_error_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", bitmap.ToMat());//保存NG原图

                    }
                    else
                    {
                        DatumPoint_X = max_blob.Left + ScrewPara.Roi_DatumPointRange_X;
                        DatumPoint_Y = max_blob.Top + ScrewPara.Roi_DatumPointRange_Y;

                        RecA = max_blob.Area;
                        RecH = max_blob.Height;
                        RecW = max_blob.Width;
                        if (GloPara.DebugMode)
                        {
                            logger.Debug("Rectangle: " + "W:" + max_blob.Width + "H: " + max_blob.Height + "A: " + max_blob.Area);
                        }
                    }

                }
                Run_status[0] = StatusType.end;

            }
            catch (Exception ex)
            {
                Run_status[0] = StatusType.error;

                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Screw_DatumPositionMeasure|  " + ex.Message);
                DatumPoint_X = 0;
                DatumPoint_Y = 0;
                string screw_error_images_path = Screw_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(screw_error_images_path);
                Cv2.ImWrite(screw_error_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", SourceImage.ToMat());//保存NG原图

            }
        }
        /// <summary>
        /// 初始化显示参数
        /// </summary>
        private void PropertyInit()
        {
            OpCircularCentroidX = 0;
            OpCircularCentroidY = 0;
            RecA = 0;
            RecH = 0;
            RecW = 0;
            //OpCircularArea = 0;
            //OpCircularHeight = 0;
            //OpCircularWidth = 0;
            //MotorCirA = 0;
            //MotorCirH = 0;
            //MotorCirW = 0;
        }

        /// <summary>
        /// 输入轮廓，返回圆度
        /// </summary>
        /// <param name="c">轮廓点</param>
        /// <returns></returns>
        public double ContourCircularity(Point[] c)
        {

            Point2d centroid = ContourCentroid(c);//质心,,很接近Blob质心
            double cx = centroid.X;
            double cy = centroid.Y;
            double d = 0;
            double f = c.Count();//找轮廓面积
            foreach (Point p in c)
            {
                d +=Math.Sqrt( Math.Pow((p.X - cx),2)+ Math.Pow((p.Y - cy),2));
            }
            double distance = d / f;
            double ds = 0;
            foreach (Point p in c)
            {
                ds += Math.Pow((Math.Sqrt(Math.Pow((p.X - cx), 2) + Math.Pow((p.Y - cy), 2))- distance),2);

            }
            double sigma2 = ds / f;
            double roundness = 1 - Math.Sqrt(sigma2) / distance;
            return roundness;

        }
        /// <summary>
        /// 输入轮廓，输出质心
        /// </summary>
        /// <param name="c">轮廓点</param>
        /// <returns></returns>
        public Point2d ContourCentroid(Point[] c)
        {
            Moments m = Cv2.Moments(c);
            double cx = m.M10 / m.M00;//质心,,很接近Blob质心
            double cy = m.M01 / m.M00;
            return new Point2d(cx, cy);
        }

        /// <summary>
        /// 填充指定圆轮廓，画轮廓，返回指定质心集合
        /// </summary>
        /// <param name="src">输入的二值图</param>
        /// <param name="canvas">输入的画布，轮廓将画在上面</param>
        /// <param name="diameter">预设的直径</param>
        /// <param name="range">直径正负差值范围</param>
        /// <param name="roundness_lower">圆度限制</param>
        /// <returns></returns>
        public List<Point2d> FillBlobFilterByCircularity(Mat src, Mat canvas, int diameter, int range, float roundness_lower,string name = "no-name",int thickness = 1)
        {
            logger.Info("分-秒-毫秒： " + DateTime.Now.ToString("mm-ss-fff"));
            //List<ConnectedComponents.Blob> targetBlobs = new List<ConnectedComponents.Blob>();
            src.FindContours(out Point[][] contours, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);//查找边缘
            List<Point[]> contours_filtered = new List<Point[]>();  // 用于存储根据条件筛选过后的区域对象
            List<Point[]> contours_filtered2 = new List<Point[]>();  // 用于存储根据条件筛选过后的区域对象
            List<Point2d> target_centroids = new List<Point2d>();  

            foreach (Point[] c in contours)
            {
                RotatedRect minRect = Cv2.MinAreaRect(c);

                // 根据高、宽筛选
                if (Math.Abs(minRect.Size.Height - diameter) <= range
                        && Math.Abs(minRect.Size.Width - diameter) <= range)
                {
                    contours_filtered.Add(c);
                }
            }
            Cv2.FillPoly(src, contours_filtered, Scalar.White);//先填充，再找轮廓，防止圆空心
            src.FindContours(out Point[][] contours2, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);//查找边缘
            foreach (Point[] c in contours2)
            {
                RotatedRect minRect2 = Cv2.MinAreaRect(c);

                // 根据高、宽筛选
                if (Math.Abs(minRect2.Size.Height - diameter) <= range
                        && Math.Abs(minRect2.Size.Width - diameter) <= range)
                {
                    double circularity = ContourCircularity(c);
                    if (circularity > roundness_lower)//圆度大于这个值时候，0.8
                    {

                        contours_filtered2.Add(c);
                        Point2d centroid_point = ContourCentroid(c);
                        target_centroids.Add(centroid_point);
                        if (GloPara.DebugMode)
                        {
                            logger.Info(name + " 宽度: " + minRect2.Size.Width);
                            logger.Info(name + " 高度: " + minRect2.Size.Height);
                            logger.Info(name + " 圆度: " + circularity);
                            logger.Info(name + " 质心: " + centroid_point);
                        }
                    }
                }
            }
            Cv2.DrawContours(canvas, contours_filtered2, -1, Scalar.GreenYellow, thickness);//轮廓点画在图上
            logger.Info("分-秒-毫秒： " + DateTime.Now.ToString("mm-ss-fff"));


            return target_centroids;
        }



        /// <summary>
        ///  计算圆孔偏移量
        /// </summary>
        private void Screw_CircularPositionMeasure(BitmapSource bitmap, BitmapSource canvasBitmap)
        {
            try
            {
                Run_status[1] = StatusType.running;
                Rect roi_Motor = new Rect(DatumPoint_X + ScrewPara.Roi_Motor_L, DatumPoint_Y + ScrewPara.Roi_Motor_T, ScrewPara.Roi_Motor_W, ScrewPara.Roi_Motor_H);
                Rect roi_Circular = new Rect(DatumPoint_X + ScrewPara.Roi_Circular_L, DatumPoint_Y + ScrewPara.Roi_Circular_T, ScrewPara.Roi_Circular_W, ScrewPara.Roi_Circular_H);
                using (Mat img = bitmap.ToMat().Clone())
                using (Mat imageDisplay = canvasBitmap.ToMat())
                using (Mat imageROI_Motor = new Mat(img, roi_Motor))
                using (Mat imageROI_Circular = new Mat(img, roi_Circular))
                using (Mat closingImageInv = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    List<Point2d> target_centroids = new List<Point2d>();
                    Cv2.MedianBlur(imageROI_Circular, closingImageInv, 5);
                    Cv2.CvtColor(closingImageInv, closingImageInv, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(closingImageInv, closingImageInv, ScrewPara.OpCircularThresh, 255, ThresholdTypes.BinaryInv);//圆的
                    Cv2.MorphologyEx(closingImageInv, closingImageInv, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(closingImageInv, closingImageInv, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Screw_MotorCircularPositionMeasure(imageROI_Circular);//找马达钣金的圆心
                    target_centroids = FillBlobFilterByCircularity(closingImageInv, imageROI_Circular, ScrewPara.OpCircular_W, ScrewPara.OpCircularRangeLimit_W, 0.9f,"OP圆孔",2);

                    if (target_centroids.Count == 1 && target_centroids[0].X != 0 && target_centroids[0].Y != 0)
                    {
                        OpCircularCentroidX = target_centroids[0].X + DatumPoint_X + ScrewPara.Roi_Circular_L;
                        OpCircularCentroidY = target_centroids[0].Y + DatumPoint_Y + ScrewPara.Roi_Circular_T;
                        //OpCircularWidth = targetBlobs[0].Width;
                        //OpCircularHeight = targetBlobs[0].Height;
                        //OpCircularArea = targetBlobs[0].Area;



                        if (Math.Abs(MotorCircularCentroidX - OpCircularCentroidX) <= ScrewPara.DeltaXYLimit && Math.Abs(MotorCircularCentroidY - OpCircularCentroidY) <= ScrewPara.DeltaXYLimit)//限制OP孔和马达孔的圆心的Δ差值
                        {
                            JudgeColorCircular = "OK";
                            JudgeTextCircular = "OK";

                            //画OK马达大小绿框到原图
                            Cv2.Rectangle(imageDisplay, roi_Motor, Scalar.GreenYellow, 10, LineTypes.AntiAlias, 0);
                            //画圈到原图
                            //Cv2.Circle(imageDisplay, new Point(OpCircularCentroidX, OpCircularCentroidY), targetBlobs[0].Width / 2, Scalar.Red, 3, LineTypes.Link8, 0);
                            //Cv2.Circle(imageDisplay, new Point(OpCircularCentroidX, OpCircularCentroidY), 5, Scalar.GreenYellow, -1, LineTypes.Link8, 0);
                            //Cv2.Circle(imageROI_Circular, new Point(targetBlobs[0].Centroid.X, targetBlobs[0].Centroid.Y), 3, Scalar.GreenYellow, -1, LineTypes.Link8, 0);//圆孔中心点，同时出现在Circular上了。。。
                    
                        }
                        else
                        {
                            JudgeColorCircular = "NG";
                            JudgeTextCircular = "NG";
                            Cv2.Rectangle(imageDisplay, roi_Motor, Scalar.Red, 10, LineTypes.AntiAlias, 0);
                            //Cv2.Circle(imageDisplay, new Point(OpCircularCentroidX, OpCircularCentroidY), targetBlobs[0].Width / 2, Scalar.Red, 3, LineTypes.Link8, 0);
                            Cv2.Circle(imageDisplay, new Point(OpCircularCentroidX, OpCircularCentroidY), 5, Scalar.GreenYellow, -1, LineTypes.Link8, 0);
                        }


                        if (GloPara.DebugMode)
                        {
                            logger.Debug(JudgeColorCircular + " CircularTargetBlobs.Count==1 X: " + OpCircularCentroidX + " Y " + OpCircularCentroidY);
                            //logger.Debug(JudgeColorCircular + " CircularTargetBlobs.Count==1 W: " + targetBlobs[0].Width + " H: " + targetBlobs[0].Height + "A:" + targetBlobs[0].Area);
                        }
                    }
                    else
                    {
                        JudgeColorCircular = "NG";
                        JudgeTextCircular = "NG";
                        GloPara.MotorNG += 1;
                        logger.Info("Circular.Count != 1");
                        OpCircularCentroidX = 0;
                        OpCircularCentroidY = 0;
                        // create_dir(Folder_Circular_NG + DateTime.Now.ToString("yyyy_MM_dd") + "\\");
                        Cv2.Rectangle(imageDisplay, roi_Motor, Scalar.Red, 10, LineTypes.AntiAlias, 0);//NG红色

                    }
                    //画Master点在全图上
                    Cv2.Circle(imageDisplay, new Point(GloPara.OriginX, GloPara.OriginY), 356 / 2, Scalar.Pink, 3, LineTypes.Link4, 0);//显示master的原位置。
                    Cv2.Line(imageDisplay, (int)(GloPara.OriginX - 350), (int)(GloPara.OriginY - 0), (int)(GloPara.OriginX + 350), (int)(GloPara.OriginY + 0), Scalar.Pink, 3, LineTypes.Link4, 0);
                    Cv2.Line(imageDisplay, (int)(GloPara.OriginX - 0), (int)(GloPara.OriginY - 350), (int)(GloPara.OriginX + 0), (int)(GloPara.OriginY + 350), Scalar.Pink, 3, LineTypes.Link4, 0);
                    //画在motor图上 
                    Cv2.Circle(imageROI_Motor, new Point((int)(OpCircularCentroidX - (DatumPoint_X + ScrewPara.Roi_Motor_L)), (int)(OpCircularCentroidY - (DatumPoint_Y + ScrewPara.Roi_Motor_T))), 3, Scalar.GreenYellow, -1, LineTypes.Link8, 0);//圆孔中心点，同时出现在Circular上了。。。
                    Cv2.Circle(imageROI_Motor, new Point(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Motor_L), GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Motor_T)), 356 / 2, Scalar.Pink, 1, LineTypes.Link4, 0);//显示master的原位置。
                    Cv2.Line(imageROI_Motor, (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Motor_L) - 350), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Motor_T) - 0), (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Motor_L) + 350), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Motor_T) + 0), Scalar.Pink, 2, LineTypes.Link4, 0);
                    Cv2.Line(imageROI_Motor, (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Motor_L) - 0), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Motor_T) - 350), (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Motor_L) + 0), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Motor_T) + 350), Scalar.Pink, 2, LineTypes.Link4, 0);
                    //Circular有画的十字在。。。。。是否原图被污染-----clone解决问题了？
                    Cv2.Circle(imageROI_Circular, new Point(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Circular_L), GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Circular_T)), 356 / 2, Scalar.Pink, 1, LineTypes.Link4, 0);//显示master的原位置。
                    Cv2.Line(imageROI_Circular, (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Circular_L) - 350), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Circular_T) - 0), (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Circular_L) + 350), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Circular_T) + 0), Scalar.Pink, 1, LineTypes.Link4, 0);
                    Cv2.Line(imageROI_Circular, (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Circular_L) - 0), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Circular_T) - 350), (int)(GloPara.OriginX - (DatumPoint_X + ScrewPara.Roi_Circular_L) + 0), (int)(GloPara.OriginY - (DatumPoint_Y + ScrewPara.Roi_Circular_T) + 350), Scalar.Pink, 1, LineTypes.Link4, 0);
                    //画基准点到原图
                    Cv2.Line(imageDisplay, DatumPoint_X - 200, DatumPoint_Y - 0, DatumPoint_X + 200, DatumPoint_Y + 0, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    Cv2.Line(imageDisplay, DatumPoint_X - 0, DatumPoint_Y - 200, DatumPoint_X + 0, DatumPoint_Y + 200, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    //画定位点范围
                    Cv2.Rectangle(imageDisplay, new Rect(ScrewPara.Roi_DatumPointRange_X, ScrewPara.Roi_DatumPointRange_Y, ScrewPara.Roi_DatumPointRange_W, ScrewPara.Roi_DatumPointRange_H), Scalar.Blue, 17, LineTypes.AntiAlias, 0);
                    //显示图片
                    DisplayCircularImage = imageROI_Circular.ToBitmapSource();
                    DisplayMotorImage = imageROI_Motor.ToBitmapSource();
                    ImageObjects[5].DisplayImage = imageDisplay.ToBitmapSource();
                    DisplayCloseCircularImage = closingImageInv.ToBitmapSource();
                }

                MotorOpCentroidDeltaX = Math.Abs(MotorCircularCentroidX - OpCircularCentroidX);
                MotorOpCentroidDeltaY = Math.Abs(MotorCircularCentroidY - OpCircularCentroidY);
                logger.Info("MotorOpCentroidDeltaX: " + MotorOpCentroidDeltaX + " MotorOpCentroidDeltaY: " + MotorOpCentroidDeltaY);
                Run_status[1] = StatusType.end;

            }
            catch (Exception ex)
            {
                Run_status[1] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Screw_CircularPositionMeasure| " + ex.Message);//出错直接NG
                JudgeColorCircular = "NG";
                JudgeTextCircular = "NG";
                GloPara.MotorNG += 1;
                OpCircularCentroidX = 0;
                OpCircularCentroidY = 0;
                MotorOpCentroidDeltaX = Math.Abs(MotorCircularCentroidX - OpCircularCentroidX);
                MotorOpCentroidDeltaY = Math.Abs(MotorCircularCentroidY - OpCircularCentroidY);
                logger.Error("MotorOpCentroidDeltaX: " + MotorOpCentroidDeltaX + " MotorOpCentroidDeltaY: " + MotorOpCentroidDeltaY);
                string screw_error_images_path = Screw_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(screw_error_images_path);
                Cv2.ImWrite(screw_error_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", SourceImage.ToMat());//保存NG原图


            }
        }

        /// <summary>
        /// 算马达圆心，对比op圆心做判断
        /// </summary>
        /// <param name="ROI"></param>
        private void Screw_MotorCircularPositionMeasure(Mat mat)
        {
            try
            {
                using (Mat roiImg = mat.Clone())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    List<Point2d> target_centroids = new List<Point2d>();
                    //List<ConnectedComponents.Blob> targetBlobs = new List<ConnectedComponents.Blob>();
                    Cv2.MedianBlur(roiImg, roiImg, 5);
                    Cv2.CvtColor(roiImg, roiImg, ColorConversionCodes.RGB2GRAY);
                    //Cv2.EqualizeHist(roiImg, roiImg);
                    Cv2.Threshold(roiImg, roiImg, ScrewPara.MotorCircularThresh, 255, ThresholdTypes.BinaryInv);//圆的
                    Cv2.MorphologyEx(roiImg, roiImg, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(roiImg, roiImg, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);



                    target_centroids = FillBlobFilterByCircularity(roiImg, mat, ScrewPara.MotorCircular_W, ScrewPara.MotorCircularRangLimit_W, 0.9f,"钣金圆孔", 2);




                    //roiImg.FindContours(out Point[][] contours, out HierarchyIndex[] hierarchyIndexes, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);//查找边缘
                    //List<Point[]> contours_filtered = new List<Point[]>();  // 用于存储根据条件筛选过后的区域对象
                    //                                                        // 用于筛选blob尺寸的条件，跟ConnectedComponents里面的Width， Height一样
                    //foreach (Point[] c in contours)
                    //{
                    //    RotatedRect minRect = Cv2.MinAreaRect(c);
                    //    // 根据高、宽筛选
                    //    if (Math.Abs(minRect.Size.Height - ScrewPara.MotorCircular_W) <= ScrewPara.MotorCircularRangLimit_W
                    //            && Math.Abs(minRect.Size.Width - ScrewPara.MotorCircular_W) <= ScrewPara.MotorCircularRangLimit_W)
                    //    {
                    //        contours_filtered.Add(c);
                    //        if (GloPara.DebugMode)
                    //        {
                    //            logger.Debug("MotorCircular " + "W: " + minRect.Size.Width + "H: " + minRect.Size.Height);

                    //        }
                    //    }
                    //}
                    //// 填充区域(第5个参数thickness设置为-1时表示填充, 大于0时仅在边缘描出厚度为thickness的线）
                    //Cv2.DrawContours(roiImg, contours_filtered, -1, Scalar.White, -1);//画图捕捉到的边缘的目标的图，图画在ClosingImageInv上，-1相当于填充。网上用FloodFill函数，下次可以试试
                    DisplayCloseMotorCircularImage = roiImg.ToBitmapSource();
                    //Cv2.DrawContours(mat, contours_filtered, -1, Scalar.DeepPink, 1);//轮廓点画在图上

                    //var cc = Cv2.ConnectedComponentsEx(roiImg);
                    //foreach (var blob in cc.Blobs.Skip(1))
                    //{
                    //    //还要Area吗
                    //    if (Math.Abs(blob.Width - ScrewPara.MotorCircular_W) <= ScrewPara.MotorCircularRangLimit_W &&
                    //        Math.Abs(blob.Height - ScrewPara.MotorCircular_W) <= ScrewPara.MotorCircularRangLimit_W)//圆
                    //    {
                    //        targetBlobs.Add(blob);
                    //        if (GloPara.DebugMode)
                    //        {
                    //            logger.Info("MotorCircularBlob " + "W: " + blob.Width + "H: " + blob.Height + "A: " + blob.Area);
                    //        }
                    //    }
                    //}
                    if (target_centroids.Count == 1 && target_centroids[0].X != 0 && target_centroids[0].Y != 0)
                    {
                        MotorCircularCentroidX = target_centroids[0].X + DatumPoint_X + ScrewPara.Roi_Circular_L;
                        MotorCircularCentroidY = target_centroids[0].Y + DatumPoint_Y + ScrewPara.Roi_Circular_T;

                        //MotorCirA = targetBlobs[0].Area;
                        //MotorCirH = targetBlobs[0].Height;
                        //MotorCirW = targetBlobs[0].Width;
                        if (GloPara.DebugMode)
                        {
                            logger.Debug("MotorCirculartargetBlobs  X: " + MotorCircularCentroidX + " Y: " + MotorCircularCentroidY);

                        }
                    }
                    else
                    {
                        MotorCircularCentroidX = 0;
                        MotorCircularCentroidY = 0;
                        //MotorCirA = 0;
                        //MotorCirH = 0;
                        //MotorCirW = 0;
                        logger.Info("MotorCirculartargetBlobs.Count!=1:  " + target_centroids.Count);
                    }

                }
            }
            catch (Exception ex)
            {
                Run_status[1] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Screw_MotorCircularPositionMeasure| " + ex.Message);//出错直接NG
                MotorCircularCentroidX = 0;
                MotorCircularCentroidY = 0;
                //MotorCirA = 0;
                //MotorCirH = 0;
                //MotorCirW = 0;
                string screw_error_images_path = Screw_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(screw_error_images_path);
                Cv2.ImWrite(screw_error_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", SourceImage.ToMat());//保存NG原图

            }

        }
        /// <summary>
        /// 预测压轴，和最终判决 
        /// </summary>
        private void Screw_TabletPredictors(BitmapSource bitmap, BitmapSource canvasBitmap)
        {
            try
            {
                Run_status[2] = StatusType.running;

                using (Mat image = bitmap.ToMat())
                using (Mat imageDisplay = canvasBitmap.ToMat())
                {
                    Lobe.LoadModel();
                    Lobe.Name = "Tablet";
                    string label = "";
                    double confidence = 0;
                    Rect roi_AMotor = new Rect(DatumPoint_X + ScrewPara.Roi_Motor_L, DatumPoint_Y + ScrewPara.Roi_Motor_T, ScrewPara.Roi_Motor_W, ScrewPara.Roi_Motor_H);
                    Rect roi_ATablet = new Rect(DatumPoint_X + ScrewPara.Roi_ATablet_X, DatumPoint_Y + ScrewPara.Roi_ATablet_Y, ScrewPara.Roi_ATablet_W, ScrewPara.Roi_ATablet_H);

                    using (Mat imageATablet = new Mat(image.Clone(), roi_ATablet))
                    using (Mat imageAMotor = new Mat(image.Clone(), roi_AMotor))
                    using (Mat imageROI_Tablet = new Mat(image.Clone(), roi_ATablet))
                    {
                        string screw_tablet_images_path;
                        string screw_original_images_path;
                        string screw_motor_images_path;

                        DisplayTabletImage = imageROI_Tablet.ToBitmapSource();
                        Lobe.Predict(imageROI_Tablet, ref label, ref confidence);//ImageROI_Tablet做判断。。
                        TabletConfidence = confidence;
                        TabletLabel = label;
                        if (label == "NG" || (label == "OK" && confidence < 0.8))
                        {
                            logger.Info("TabletNG" + label + "___" + confidence + "___" + LatestOPID);
                            GloPara.TabletNG += 1;
                            JudgeColor_TabletPredictor = "NG";
                            JudgeText_TabletPredictor = "NG";
                            //NG画框
                            Cv2.Rectangle(imageDisplay, roi_ATablet, Scalar.Red, 10, LineTypes.AntiAlias, 0);

                        }
                        else if (label == "OK" && confidence > 0.8)
                        {
                            JudgeColor_TabletPredictor = "OK";
                            JudgeText_TabletPredictor = "OK";
                            logger.Info("TabletOK" + label + "___" + confidence + "___" + LatestOPID);
                            Cv2.Rectangle(imageDisplay, roi_ATablet, Scalar.GreenYellow, 10, LineTypes.AntiAlias, 0);

                        }
                        //画图
                        //OP圆孔
                        Cv2.PutText(imageDisplay, "X:" + OpCircularCentroidX, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                        Cv2.PutText(imageDisplay, "Y:" + OpCircularCentroidY, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 150), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                        ////OP孔WHA
                        //Cv2.PutText(imageDisplay, "OpCirW:" + OpCircularWidth, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 600), HersheyFonts.HersheyComplex, 5, Scalar.DarkOrange, 6);
                        //Cv2.PutText(imageDisplay, "OpCirH:" + OpCircularHeight, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 750), HersheyFonts.HersheyComplex, 5, Scalar.DarkOrange, 6);
                        //Cv2.PutText(imageDisplay, "OpCirA:" + OpCircularArea, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 900), HersheyFonts.HersheyComplex, 5, Scalar.DarkOrange, 6);
                        //马达孔WHA
                        //Cv2.PutText(imageDisplay, "MCirW:" + MotorCirW, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1050), HersheyFonts.HersheyComplex, 5, Scalar.Blue, 6);
                        //Cv2.PutText(imageDisplay, "MCirH:" + MotorCirH, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1200), HersheyFonts.HersheyComplex, 5, Scalar.Blue, 6);
                        //Cv2.PutText(imageDisplay, "MCirA:" + MotorCirA, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1350), HersheyFonts.HersheyComplex, 5, Scalar.Blue, 6);
                        //右下角治具方的WHA
                        Cv2.PutText(imageDisplay, "RecW:" + RecW, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1500), HersheyFonts.HersheyComplex, 5, Scalar.DeepPink, 6);
                        Cv2.PutText(imageDisplay, "RecH:" + RecH, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1650), HersheyFonts.HersheyComplex, 5, Scalar.DeepPink, 6);
                        Cv2.PutText(imageDisplay, "RecA:" + RecA, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 1800), HersheyFonts.HersheyComplex, 5, Scalar.DeepPink, 6);
                        //最后判断
                        if (JudgeText_TabletPredictor == "OK" && JudgeColorCircular == "OK")  //最后OK时
                        {
                            ShiftX = 0;
                            ShiftY = 0;
                            double m = Math.PI / 2;//补90°,旋转坐标系。
                            ShiftX = GloPara.XRatio * (((OpCircularCentroidX - GloPara.OriginX) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) + ((OpCircularCentroidY - GloPara.OriginY) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));
                            ShiftY = GloPara.YRatio * (((OpCircularCentroidY - GloPara.OriginY) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) - ((OpCircularCentroidX - GloPara.OriginX) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));

                            //显示偏移量
                            Cv2.PutText(imageDisplay, "X_:" + ShiftX, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 300), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                            Cv2.PutText(imageDisplay, "Y_:" + ShiftY, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 450), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                            screw_motor_images_path = Screw_motor_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";
                            screw_tablet_images_path = Screw_tablet_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";
                            screw_original_images_path = Screw_original_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";
                        }
                        else//最后NG
                        {
                            ////显示偏移量，NG因该时0
                            Cv2.PutText(imageDisplay, "X_:" + ShiftX, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 300), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                            Cv2.PutText(imageDisplay, "Y_:" + ShiftY, new Point(DatumPoint_X + 245 + 50, DatumPoint_Y - 2296 + 450), HersheyFonts.HersheyComplex, 5, Scalar.Red, 6);
                            screw_motor_images_path = Screw_motor_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";

                            screw_tablet_images_path = Screw_tablet_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";
                            screw_original_images_path = Screw_original_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";

                        }

                        Create_dir(screw_motor_images_path);
                        Create_dir(screw_tablet_images_path);
                        Create_dir(screw_original_images_path);
                        //马达roi的压缩图
                        save_compressed_img(imageAMotor, 0.2, screw_motor_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg");
                        //NG压轴图
                        Cv2.ImWrite(screw_tablet_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", imageATablet);
                        //保存OK原图和画布
                        Cv2.ImWrite(screw_original_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", image);//保存OK原图 

                        ImageObjects[5].DisplayImage = imageDisplay.ToBitmapSource();//传递显示图像
                        GloPara.TabletNGRatio = GloPara.TabletNG / GloPara.TotalTablet;
                        GloPara.TabletNGRatio360 = GloPara.TabletNGRatio * 360;
                        GloPara.MotorNGRatio = GloPara.MotorNG / GloPara.TotalMotor;
                        GloPara.MotorNGRatio360 = GloPara.MotorNGRatio * 360;
                        SaveGloParaToJsonData();
                        SaveData(@"Data\\", SaveDataType.all);
                        SaveData(@"Data\\", SaveDataType.mo);

                    }
                }
                Run_status[2] = StatusType.end;

            }

            catch (Exception ex)
            {
                Run_status[2] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Screw_TabletPredictors| " + ex.Message);
                JudgeColor_TabletPredictor = "NG";
                JudgeText_TabletPredictor = "NG";
                GloPara.TabletNG += 1;
                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 12 });
                string screw_error_images_path = Screw_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(screw_error_images_path);
                Cv2.ImWrite(screw_error_images_path + Read49.ToString() + "_Fixture__" + LatestOPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", SourceImage.ToMat());//保存NG原图
                ImageObjects[5].DisplayImage = SourceImage;
                GloPara.TabletNGRatio = GloPara.TabletNG / GloPara.TotalTablet;
                GloPara.TabletNGRatio360 = GloPara.TabletNGRatio * 360;
                GloPara.MotorNGRatio = GloPara.MotorNG / GloPara.TotalMotor;
                GloPara.MotorNGRatio360 = GloPara.MotorNGRatio * 360;
                SaveGloParaToJsonData();
                SaveData(@"Data\\", SaveDataType.all);
                SaveData(@"Data\\", SaveDataType.mo);

            }
        }

        #endregion

        #region 开始线程
        /// <summary>
        /// ScrewStart
        /// </summary>
        private void ScrewStart()
        {
            try
            {

                for (int i = 0; i < 3; i++)//使状态置为wait状态
                {
                    Run_status[i] = StatusType.wait;
                }
                BitmapSource tempImg;
                if (SpinCtrl.AcquisitionBitmapFromCam(0, out tempImg))//拍照为true时
                {
                    SourceImage = tempImg;
                    ImageObjects[5].DisplayImage = SourceImage.Clone();
                    RotateImage_Screw = Rotate_arbitrarily_angle(SourceImage, ScrewPara.RotateAngle_Screw);//旋转图片
                    ImageObjects[5].DisplayImage = RotateImage_Screw.Clone();
                    _waitForResponse.WaitOne(2000);//等待读取到OPID，读取在ReadData线程，一般都比来到这里快，超时2秒就放过算了。
                    Screw_DatumPositionMeasure(RotateImage_Screw);//检测定位点
                    if (DatumPoint_X != 0)//定位点不为0时。
                    {
                        //GC.Collect();
                        if (!RepairMode)//非修理模式下，测量圆孔，预测压杆
                        {
                            Screw_CircularPositionMeasure(RotateImage_Screw, ImageObjects[5].DisplayImage);//测量圆孔位置，
                            Screw_TabletPredictors(RotateImage_Screw, ImageObjects[5].DisplayImage);//预测压杆
                                                                                                    //判断写入PLC
                            if (JudgeText_TabletPredictor == "OK" && JudgeColorCircular == "OK")  //最后OK时
                            {
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 51, 1, new ushort[] { (ushort)ShiftX });//孔位，X轴的偏移量
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 53, 1, new ushort[] { (ushort)ShiftY });//孔位，Y轴的偏移量
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 11 });//OK给11
                            }
                            else
                            {
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 12 });//NG给12

                            }
                        }
                        else//修理模式时，不测量圆孔，仅预判压杆
                        {
                            OpCircularCentroidX = GloPara.OriginX;
                            OpCircularCentroidY = GloPara.OriginY;
                            JudgeColorCircular = "NA";
                            JudgeTextCircular = "NA";

                            Screw_TabletPredictors(RotateImage_Screw, ImageObjects[5].DisplayImage);
                            //判断写入PLC
                            if (JudgeText_TabletPredictor == "OK")  //最后OK时
                            {
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 51, 1, new ushort[] { 0 });  //孔位，X轴的偏移量
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 53, 1, new ushort[] { 0 });  //孔位，Y轴的偏移量
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 11 });//OK给11
                            }
                            else
                            {
                                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 12 });//NG给12

                            }
                        }

                    }
                    else//没测量到定位点时
                    {
                        Run_status[0] = StatusType.error;
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 12 });
                    }
                }
                DiskMg.GetDiskSpaceInfo();//更新磁盘信息。
            }
            catch (Exception ex)
            {
                for (int i = 0; i < 3; i++)
                {
                    Run_status[i] = StatusType.error;
                }
                logger.Error("ScrewStart| " + ex.Message);
                //ScrewStart();
            }
        }

        private void MasterSpotCheckStart()
        {
            try
            {

                JudgeColorCircular = "NN";
                BitmapSource tempImg1;
                SpinCtrl.AcquisitionBitmapFromCam(0, out tempImg1);
                SourceImage = tempImg1;
                ImageObjects[5].DisplayImage = SourceImage;
                RotateImage_Screw = Rotate_arbitrarily_angle(SourceImage, ScrewPara.RotateAngle_Screw);//依旧旋转图片
                ImageObjects[5].DisplayImage = RotateImage_Screw;
                SpotCheck(RotateImage_Screw);//点检Master现在的孔位置和先前的孔位置是否在误差范围内。



            }
            catch (Exception ex)
            {
                logger.Error("MasterSpotCheckStart| " + ex.Message);

            }
        }







        private bool QueueData()
        {
            Drop3OPID = "";
            Drop12OPID = "";
            ScOPID = "";
            Drop3Fixture = 0;
            Drop12Fixture = 0;
            if (OPID == "" || OPID == null || OPID != LatestOPID)//防止复位导致错位
            {
                Drop3OPID = QueueValue(GloPara.FormerOPID, OPID, 4);//第三滴用
                Drop3Fixture = QueueValue(GloPara.PrevioseFixture, Read49, 4);//第三滴用

                Drop12OPID = QueueValue(GloPara.Drop12FormerOPID, OPID, 2);//第12滴用
                Drop12Fixture = QueueValue(GloPara.Drop12PrevioseFixture, Read49, 2);//第12滴用

                ScOPID = QueueValue(GloPara.PreviousOPID, OPID, 1);//螺丝的
                ShiftXsc = QueueValue(GloPara.ShiftXSC, ShiftX, 1);//偏移x
                ShiftYsc = QueueValue(GloPara.ShiftYSC, ShiftY, 1);//偏移y
                ScFixture = QueueValue(GloPara.PreviousFixture, Read49, 1);//螺丝的

            }
            LatestOPID = OPID;
            OPID = "";
            return true;
        }
        /// <summary>
        /// 记录数据&队列
        /// </summary>
        private void ReadData()
        {
            try
            {
                _waitForResponse.Reset();//阻塞线程，直到读取OPID号为止
                ushort[] read49 = new ushort[1];
                read49[0] = 0;
                PlcCtrl.PlcRead(ModbusCommand.FuncReadMultipleRegisters, 49, 1, ref read49);
                Read49 = read49[0];
                ReadOPID();//读取OPID，读完将写空覆盖原文件,读取的OPID文件，需要和IS约定好，到配置文件修改
                QueueData();
                _waitForResponse.Set();
                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 48, 1, new ushort[] { 11 });

                //Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                logger.Error("ReadData| " + ex.Message);
                Thread.Sleep(10);
                //ReadData();
            }
        }



        #endregion

        #region 点检 校正 测试
        /// <summary>
        /// 点检模式
        /// </summary>
        private void SpotCheck(BitmapSource mat)
        {

            try
            {
                using (Mat image = mat.ToMat())
                using (Mat imageMedian = new Mat())
                using (Mat gray = new Mat())
                using (Mat bin = new Mat())
                using (Mat closingImage = new Mat())
                using (Mat openingImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1))) //进行结构算子生成 
                {
                    List<Point2d> target_centroids = new List<Point2d>();
                    Cv2.MedianBlur(image, imageMedian, 5);//中值滤波//////////////////////////////////////////////
                    Cv2.CvtColor(imageMedian, gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(gray, bin, ScrewPara.MasterCircularThresh, 255, ThresholdTypes.BinaryInv);//小于value变黑，反之变为最大值；255最大值；value = 222适合
                    Cv2.MorphologyEx(bin, openingImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(openingImage, closingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);//point及之后的可以省略

                    target_centroids = FillBlobFilterByCircularity(closingImage, image, (int)ScrewPara.MasterCircular_W, (int)ScrewPara.MasterCircularRange_W, 0.93f, "Master圆孔",10);

                    logger.Info("Master圆孔目标个数： " + target_centroids.Count);

                    if (target_centroids.Count == 2)
                    {

                        int originhole;
                        originhole = Math.Abs(target_centroids[0].X) > target_centroids[1].X ? 0 : 1;
                        CorrectOrigin_X = target_centroids[originhole].X;
                        CorrectOrigin_Y = target_centroids[originhole].Y;
                        TestHole_X = target_centroids[1 - originhole].X;
                        TestHole_Y = target_centroids[1 - originhole].Y;




                        Cv2.PutText(image, "X:" + CorrectOrigin_X,
                                  new Point(0, 150), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y:" + CorrectOrigin_Y,
                                  new Point(0, 250), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "X2:" + TestHole_X,
                                  new Point(0, 550), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y2:" + TestHole_Y,
                                  new Point(0, 650), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);

                        CorrectOrigin_X = GloPara.OriginX;
                        CorrectOrigin_Y = GloPara.OriginY;
                        ShiftX = CorrectOrigin_X;
                        ShiftY = CorrectOrigin_Y;
                        Cv2.PutText(image, "X_:" + (CorrectOrigin_X - GloPara.OriginX),
                                  new Point(0, 350), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y_:" + (CorrectOrigin_Y - GloPara.OriginY),
                                  new Point(0, 450), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);


                        if (Math.Abs(CorrectOrigin_X - GloPara.OriginX) <= GloPara.LimitOriginXY && Math.Abs(CorrectOrigin_Y - GloPara.OriginY) <= GloPara.LimitOriginXY)
                        {

                            double m = Math.PI / 2;//90°
                            //图片和机器臂X和Y反过来了
                            double ShiftX_ = GloPara.XRatio * (((TestHole_X - GloPara.OriginX) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) + ((TestHole_Y - GloPara.OriginY) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));
                            double ShiftY_ = GloPara.YRatio * (((TestHole_Y - GloPara.OriginY) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) - ((TestHole_X - GloPara.OriginX) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));
                            Cv2.PutText(image, "X2_:" + ShiftX_,
                                      new Point(0, 750), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);
                            Cv2.PutText(image, "Y2_:" + ShiftY_,
                                      new Point(0, 850), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);

                            logger.Info("X: " + CorrectOrigin_X + " Y:" + CorrectOrigin_Y);
                            logger.Info("XT: " + TestHole_X + " YT:" + TestHole_Y);
                            logger.Info("XS: " + ShiftX + " YS:" + ShiftY);
                            JudgeColorCircular = "OK";
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 59, 1, new ushort[] { 11 });//在误差范围内给11


                        }
                        else
                        {
                            logger.Warn("X-Y 与原来相差太大 X: " + Math.Abs(CorrectOrigin_X - GloPara.OriginX) + "Y:" + Math.Abs(CorrectOrigin_Y - GloPara.OriginY));
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 59, 1, new ushort[] { 12 });//在误差范围内给外
                            JudgeColorCircular = "NG";
                        }

                    }
                    else  
                    {
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 59, 1, new ushort[] { 12 });
                        CorrectOrigin_X = 0;
                        CorrectOrigin_Y = 0;
                        logger.Warn("targetBlobs.Count!=2| " + target_centroids.Count);
                        JudgeColorCircular = "NG";

                    }
                    for (int i = 0; i < target_centroids.Count; i++)
                    {
                        Cv2.Line(image, (int)(target_centroids[i].X - 200), (int)(target_centroids[i].Y - 0), (int)(target_centroids[i].X + 200), (int)(target_centroids[i].Y + 0), Scalar.Red, 6, LineTypes.Link4, 0);
                        Cv2.Line(image, (int)(target_centroids[i].X - 0), (int)(target_centroids[i].Y - 200), (int)(target_centroids[i].X + 0), (int)(target_centroids[i].Y + 200), Scalar.Red, 6, LineTypes.Link4, 0);

                    }

                    //Cv2.Line(image, (int)(GloPara.OriginX - 350), (int)(GloPara.OriginY - 0), (int)(GloPara.OriginX + 350), (int)(GloPara.OriginY + 0), Scalar.Blue, 3, LineTypes.Link4, 0);
                    //Cv2.Line(image, (int)(GloPara.OriginX - 0), (int)(GloPara.OriginY - 350), (int)(GloPara.OriginX + 0), (int)(GloPara.OriginY + 350), Scalar.Blue, 3, LineTypes.Link4, 0);

                    //Cv2.Line(image, (int)(GloPara.OriginX - 350), (int)(GloPara.OriginY - 0), (int)(GloPara.OriginX + 350), (int)(GloPara.OriginY + 0), Scalar.GreenYellow, 3, LineTypes.Link4, 0);
                    //Cv2.Line(image, (int)(GloPara.OriginX - 0), (int)(GloPara.OriginY - 350), (int)(GloPara.OriginX + 0), (int)(GloPara.OriginY + 350), Scalar.GreenYellow, 3, LineTypes.Link4, 0);


                    DisplayTabletImage = closingImage.ToBitmapSource();
                    DisplayMotorImage = openingImage.ToBitmapSource();
                    ImageObjects[5].DisplayImage = image.ToBitmapSource();
                }
            }
            catch (Exception ex)
            {
                logger.Error("OriginCalibration2|" + ex.Message);
                PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 59, 1, new ushort[] { 12 });
                CorrectOrigin_X = 0;
                CorrectOrigin_Y = 0;
                JudgeColorCircular = "NG";

            }



        }


        #region 校正模式

        /// <summary>
        /// 校正模式
        /// </summary>
        private void OriginCalibration2(BitmapSource bitmapSource)
        {
            try
            {

                using (Mat image = bitmapSource.ToMat().Clone())
                using (Mat imageMedian = new Mat())
                using (Mat gray = new Mat())
                using (Mat Bin = new Mat())
                using (Mat closingImage = new Mat())
                using (Mat openingImage = new Mat())
                using (Mat connectdeImg = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1))) //进行结构算子生成 
                {
                    List<Point2d> target_centroids = new List<Point2d>();//单个。暂时不用集合
                    Cv2.MedianBlur(image, imageMedian, 5);
                    Cv2.CvtColor(imageMedian, gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(gray, Bin, ScrewPara.MasterCircularThresh, 255, ThresholdTypes.BinaryInv);//小于value变黑，反之变为最大值；255最大值；value = 222适合
                    Cv2.MorphologyEx(Bin, openingImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(openingImage, closingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);//point及之后的可以省略

                    target_centroids = FillBlobFilterByCircularity(closingImage, image, (int)ScrewPara.MasterCircular_W, (int)ScrewPara.MasterCircularRange_W, 0.93f,"Master圆孔", 10);

                    logger.Info("Master圆孔目标个数： " + target_centroids.Count);
                    if (target_centroids.Count == 2)//是不是应该有两个？哪个在前哪个在后？
                    {

                        int originhole;
                        originhole = Math.Abs(target_centroids[0].X) > target_centroids[1].X ? 0 : 1;
                        CorrectOrigin_X = target_centroids[originhole].X;
                        CorrectOrigin_Y = target_centroids[originhole].Y;
                        TestHole_X = target_centroids[1 - originhole].X;
                        TestHole_Y = target_centroids[1 - originhole].Y;


                        double pixelX = Math.Sqrt(Math.Pow(target_centroids[0].X - target_centroids[1].X, 2) + Math.Pow(target_centroids[0].Y - target_centroids[1].Y, 2));
                        GloPara.XRatio = GloPara.MasterDistanceX / pixelX;//两孔的实际距离,XY轴比例应该相同

                        GloPara.OriginX = CorrectOrigin_X;
                        GloPara.OriginY = CorrectOrigin_Y;

                        GloPara.Master1Angle = Math.Atan2(CorrectOrigin_Y - TestHole_Y, CorrectOrigin_X - TestHole_X);  //这个不用判断第几象限
                        double m = Math.PI / 2;//90°

                        //X和Y反过来了
                        LineDetection(bitmapSource.ToMat().Clone(), image);//找线，求YRatio


                        ShiftX = GloPara.XRatio * (((TestHole_X - GloPara.OriginX) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) + ((TestHole_Y - GloPara.OriginY) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));
                        ShiftY = GloPara.YRatio * (((TestHole_Y - GloPara.OriginY) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) - ((TestHole_X - GloPara.OriginX) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));

                        
                        Cv2.PutText(image, "X:" + CorrectOrigin_X,
                                  new Point(0, 150), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y:" + CorrectOrigin_Y,
                                  new Point(0, 250), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "X2:" + TestHole_X,
                                  new Point(0, 350), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y2:" + TestHole_Y,
                                  new Point(0, 450), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "X2_:" + ShiftX,
                                  new Point(0, 550), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);
                        Cv2.PutText(image, "Y2_:" + ShiftY,
                                  new Point(0, 650), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);

                        logger.Info("X: " + CorrectOrigin_X + " Y:" + CorrectOrigin_Y);
                        logger.Info("XT: " + TestHole_X + " YT:" + TestHole_Y);
                        logger.Info("XShit: " + ShiftX + " YShit:" + ShiftY);

                    }
                    else 
                    {
                        CorrectOrigin_X = 0;
                        CorrectOrigin_Y = 0;
                        logger.Warn("targetBlobs.Count!=2| " + target_centroids.Count);
                        JudgeColorCircular = "NG";


                    }

                    for(int i = 0; i < target_centroids.Count; i++)
                    {
                        Cv2.Line(image, (int)(target_centroids[i].X - 200), (int)(target_centroids[i].Y - 0), (int)(target_centroids[i].X + 200), (int)(target_centroids[i].Y + 0), Scalar.Red, 6, LineTypes.Link4, 0);
                        Cv2.Line(image, (int)(target_centroids[i].X - 0), (int)(target_centroids[i].Y - 200), (int)(target_centroids[i].X + 0), (int)(target_centroids[i].Y + 200), Scalar.Red, 6, LineTypes.Link4, 0);

                    }


                    //Cv2.Line(image, (int)(GloPara.OriginX - 350), (int)(GloPara.OriginY - 0), (int)(GloPara.OriginX + 350), (int)(GloPara.OriginY + 0), Scalar.Blue, 3, LineTypes.Link4, 0);
                    //Cv2.Line(image, (int)(GloPara.OriginX - 0), (int)(GloPara.OriginY - 350), (int)(GloPara.OriginX + 0), (int)(GloPara.OriginY + 350), Scalar.Blue, 3, LineTypes.Link4, 0);
                    //Cv2.Line(image, (int)(GloPara.OriginX - 350), (int)(GloPara.OriginY - 0), (int)(GloPara.OriginX + 350), (int)(GloPara.OriginY + 0), Scalar.DarkRed, 3, LineTypes.Link4, 0);
                    //Cv2.Line(image, (int)(GloPara.OriginX - 0), (int)(GloPara.OriginY - 350), (int)(GloPara.OriginX + 0), (int)(GloPara.OriginY + 350), Scalar.DarkRed, 3, LineTypes.Link4, 0);



                    DisplayTabletImage = closingImage.ToBitmapSource();
                    DisplayMotorImage = openingImage.ToBitmapSource();
                    ImageObjects[5].DisplayImage = image.ToBitmapSource();

                }

            }
            catch (Exception ex)
            {
                logger.Error("OriginCalibration2|" + ex.Message);
                CorrectOrigin_X = 0;
                CorrectOrigin_Y = 0;
                JudgeColorCircular = "NG";

            }
        }


        /// <summary>
        /// 霍夫线，取Master底边的线，Y坐标
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="Image"></param>
        private void LineDetection(Mat mat, Mat image)
        {
            try
            {
                using (Mat gray = mat.Clone())
                {

                    Cv2.CvtColor(gray, gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.Canny(gray, gray, 140, 20);
                    LineSegmentPoint[] lines;
                    lines = Cv2.HoughLinesP(gray, 1, Cv2.PI / 90, 50, 80, 10);
                    List<double> points = new List<double>();
                    DisplayCloseCircularImage = gray.ToBitmapSource();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].P1.X > 1000 && lines[i].P1.Y > 2000)
                        {
                            if (lines[i].P1.Y == lines[i].P2.Y)
                            {
                                if (lines[i].P2.Y < 2800)
                                {
                                    if (Math.Abs(lines[i].P1.X - GloPara.OriginX) <= 300)
                                    {
                                        Cv2.Line(image, lines[i].P1, lines[i].P2, Scalar.RandomColor(), 30);
                                        logger.Info(lines[i].P1 + "       " + lines[i].P2);
                                        points.Add(lines[i].P1.Y);
                                    }
                                }


                            }
                        }
                    }
                    logger.Debug(points.Count);
                    if (points.Count >= 1)
                    {
                        GloPara.YRatio = 1482 / (points.Min() - GloPara.OriginY);
                        JudgeColorCircular = "OK";
                        logger.Info(points.Min());

                    }
                    else
                    {
                        logger.Warn(points.Count);

                    }

                }

            }
            catch (Exception ex)
            {
                logger.Error("LineDetection|" + ex.Message);
                JudgeColorCircular = "NG";
            }
        }





        #endregion

        /// <summary>
        /// 测试模式
        /// </summary>
        void TestMasterCircularXY(BitmapSource bitmapSource)
        {
            try
            {
                using (Mat image = bitmapSource.ToMat())
                using (Mat imageMedian = new Mat())
                using (Mat gray = new Mat())
                using (Mat bin = new Mat())
                using (Mat closingImage = new Mat())
                using (Mat openingImage = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    List<Point2d> target_centroids = new List<Point2d>();
                    Cv2.MedianBlur(image, imageMedian, 5);
                    Cv2.CvtColor(imageMedian, gray, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(gray, bin, ScrewPara.MasterCircularThresh, 255, ThresholdTypes.BinaryInv);
                    Cv2.MorphologyEx(bin, openingImage, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    Cv2.MorphologyEx(openingImage, closingImage, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);


                    target_centroids = FillBlobFilterByCircularity(closingImage, image, (int)ScrewPara.MasterCircular_W, (int)ScrewPara.MasterCircularRange_W, 0.93f, "Master圆孔", 10);

                    logger.Info("Master圆孔目标个数： " + target_centroids.Count);


                    if (target_centroids.Count == 2)//是不是应该有两个？哪个在前哪个在后？
                    {
                        int originhole;
                        if (!GloPara.MasterSecondHole)
                        {

                            originhole = Math.Abs(target_centroids[0].X) > target_centroids[1].X ? 0 : 1;

                        }
                        else
                        {
                            originhole = Math.Abs(target_centroids[0].X) > target_centroids[1].X ? 1 : 0;

                        }
                        CorrectOrigin_X = target_centroids[originhole].X;
                        CorrectOrigin_Y = target_centroids[originhole].Y;
                        TestHole_X = target_centroids[1 - originhole].X;
                        TestHole_Y = target_centroids[1 - originhole].Y;

                    

                        double m = Math.PI / 2;//90°

                        ShiftX = GloPara.XRatio * (((CorrectOrigin_X - GloPara.OriginX) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) + ((CorrectOrigin_Y - GloPara.OriginY) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));
                        ShiftY = GloPara.YRatio * (((CorrectOrigin_Y - GloPara.OriginY) * Math.Cos(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)) - ((CorrectOrigin_X - GloPara.OriginX) * Math.Sin(GloPara.Master1Angle + GloPara.RobotAngleDelta - m)));

                        //X和Y反过来了
                        Cv2.PutText(image, "X:" + CorrectOrigin_X,
                                  new Point(0, 150), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y:" + CorrectOrigin_Y,
                                  new Point(0, 250), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "X2:" + TestHole_X,
                                  new Point(0, 350), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "Y2:" + TestHole_Y,
                                  new Point(0, 450), HersheyFonts.HersheyComplex, 4, Scalar.Red, 4);
                        Cv2.PutText(image, "X2_:" + ShiftX,
                                  new Point(0, 550), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);
                        Cv2.PutText(image, "Y2_:" + ShiftY,
                                  new Point(0, 650), HersheyFonts.HersheyComplex, 4, Scalar.Blue, 4);

                        logger.Info("X: " + CorrectOrigin_X + " Y:" + CorrectOrigin_Y);
                        logger.Info("XT: " + TestHole_X + " YT:" + TestHole_Y);
                        logger.Info("XS: " + ShiftX + " YS:" + ShiftY);



                    }
                    else 
                    {
                        logger.Warn("TestModel.Count!=2| " + target_centroids.Count);

                    }
                    for (int i = 0; i < target_centroids.Count; i++)
                    {
                        Cv2.Line(image, (int)(target_centroids[i].X - 200), (int)(target_centroids[i].Y - 0), (int)(target_centroids[i].X + 200), (int)(target_centroids[i].Y + 0), Scalar.Red, 6, LineTypes.Link4, 0);
                        Cv2.Line(image, (int)(target_centroids[i].X - 0), (int)(target_centroids[i].Y - 200), (int)(target_centroids[i].X + 0), (int)(target_centroids[i].Y + 200), Scalar.Red, 6, LineTypes.Link4, 0);

                    }

                    ImageObjects[5].DisplayImage = image.ToBitmapSource();
                    DisplayTabletImage = closingImage.ToBitmapSource();
                    DisplayMotorImage = openingImage.ToBitmapSource();



                }

            }
            catch (Exception ex)
            {
                logger.Error("TestModel|" + ex.Message);

            }
        }


        #endregion

        #region 用本地图片模式
        /////////////////////////////////////////////////////////////////////////////////使用本地图片模式
        /// <summary>
        /// 按钮，使用本地图片
        /// </summary>
        void LocalImageProcess()
        {
            try
            {

                string imageFileNmae = UselocalImageFlie();



                //LocalImage = SelectLocalImage();
                if (imageFileNmae != null)
                {
                    using (Mat mat = new Mat(imageFileNmae, ImreadModes.Unchanged))
                    {
                        LocalImage = mat.ToBitmapSource();
                    }
                    RotateImage_Screw = Rotate_arbitrarily_angle(LocalImage, ScrewPara.RotateAngle_Screw);

                    ImageObjects[5].DisplayImage = RotateImage_Screw.Clone();

                    logger.Warn("TabletStartTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));

                    Screw_DatumPositionMeasure(RotateImage_Screw);

                    Screw_CircularPositionMeasure(RotateImage_Screw, ImageObjects[5].DisplayImage);
                    Screw_TabletPredictors(RotateImage_Screw, ImageObjects[5].DisplayImage);



                    logger.Warn("TabletEndTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));

                }

            }
            catch (Exception ex)
            {
                logger.Error("LocalImageProcess| " + ex.Message);

            }
        }

        #endregion

        #region 压缩和截图

        /// <summary>
        /// 压缩和保存图片，用这个比较快一点，几十毫秒
        /// </summary>
        /// <param name="src" 输入图片></param>
        /// <param name="factor" 应该填小于1的正数></param>
        /// <param name="path" 保存位置地址要带后缀></param>
        private void save_compressed_img(Mat src, double factor, string path)
        {
            try
            {
                using (Mat mat = src.Clone())//clone防止污染原图
                {
                    Cv2.Resize(mat, mat, new OpenCvSharp.Size(mat.Width * factor, mat.Height * factor), 0, 0, InterpolationFlags.Area);
                    Cv2.ImWrite(path, mat);
                }
            }
            catch (Exception ex)
            {
                logger.Error("save_compressed_img|_" + ex);
            }

        }

        ////////////////////////循环截图,有点卡，有点吃内存--8G==开启线程就不会卡，占用1G左右,产线正常使用后也是1G

        /// <summary>
        /// 压缩图用
        /// </summary>
        /// <param name="InputFolder"></param>
        /// <param name="OutputFolder"></param>
        void CompressImage(string inputFolder, string outputFolder)
        {
            try
            {
                //Filter = "图像文件|*.jpg;*.png;*.jpeg;*.bmp;*.gif|所有文件|*.*"//限定文件类型？

                Create_dir(outputFolder);
                if (Directory.Exists(inputFolder))//文件夹 Directory ；文件 File.Exists。
                {
                    DirectoryInfo theFolder = new DirectoryInfo(inputFolder);
                    FileInfo[] fileInfo = theFolder.GetFiles("*.*");//全部文件类型，限定
                    Task.Run(() => MessageBox.Show("开始了，等着吧！！！\n搞完会弹窗哒！！！\n懒得做进度条\n( *︾▽︾)\t( *︾▽︾)\t( *︾▽︾)", "图片压缩", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));

                    foreach (FileInfo NextFile in fileInfo) //遍历文件夹里的文件
                    {
                        GetThumImage(NextFile.FullName, 18, 3, outputFolder + "\\" + NextFile.Name.Remove(NextFile.Name.Length - 4) + ".jpg");
                        //using (Bitmap curBitmap = (Bitmap)Image.FromFile(NextFile.FullName))
                        //{
                        //    ImageObjects[5].DisplayImage = curBitmap.ToBitmapSource();
                        //}

                    }
                    Task.Run(() => MessageBox.Show("压缩搞完啦！！！\n(ヘ･_･)ヘ┳━┳\n(╯°□°）╯︵ ┻━┻", "图片压缩", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));

                }
            }
            catch (Exception ex)
            {
                logger.Error("ImagesCut|  " + ex.Message);
                Task.Run(() => MessageBox.Show("出错啦！\n不会就不要搞啦！\n╮(╯▽╰)╭╮(╯▽╰)╭\n(￣_,￣ )", "图片压缩", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));
            }
        }
        /// <summary>
        /// 这种压缩图片方式不用了，有点慢
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="quality"></param>
        /// <param name="multiple"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        private bool getThumImage(Mat sourceFile, long quality, int multiple, string outputFile)
        {
            try
            {
                long imageQuality = quality;
                Bitmap sourceImage = sourceFile.ToBitmap();
                ImageCodecInfo myImageCodecInfo = GetEncoderInfo("image/jpeg");
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                using (EncoderParameters myEncoderParameters = new EncoderParameters(1))
                using (EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, imageQuality))
                {
                    myEncoderParameters.Param[0] = myEncoderParameter;
                    float xWidth = sourceImage.Width;
                    float yWidth = sourceImage.Height;
                    Bitmap newImage = new Bitmap((int)(xWidth / multiple), (int)(yWidth / multiple));
                    using (Graphics g = Graphics.FromImage(newImage))
                    {
                        g.DrawImage(sourceImage, 0, 0, xWidth / multiple, yWidth / multiple);
                        newImage.Save(outputFile, myImageCodecInfo, myEncoderParameters);
                        return true;
                    }
                }
            }

            catch (Exception ex)
            {
                logger.Error("getThumImage|  " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 生成缩略图，不用这种压缩了，有点慢
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
                ImageCodecInfo myImageCodecInfo = GetEncoderInfo("image/jpeg");
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                using (Bitmap sourceImage = new Bitmap(sourceFile))
                using (EncoderParameters myEncoderParameters = new EncoderParameters(1))
                using (EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, imageQuality))
                {
                    myEncoderParameters.Param[0] = myEncoderParameter;
                    float xWidth = sourceImage.Width;
                    float yWidth = sourceImage.Height;
                    Bitmap newImage = new Bitmap((int)(xWidth / multiple), (int)(yWidth / multiple));
                    using (Graphics g = Graphics.FromImage(newImage))
                    {
                        g.DrawImage(sourceImage, 0, 0, xWidth / multiple, yWidth / multiple);
                        newImage.Save(outputFile, myImageCodecInfo, myEncoderParameters);

                        return true;
                    }
                }

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

        #region 写表格
        /// <summary>
        /// Save all data 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="type"></param>
        private void SaveData(string folderPath, SaveDataType type)///////////.CSV表格
        {
            // ================== saving csv file
            bool addHeader = false;
            // prefix
            string prefix = "unknown";
            switch (type)
            {
                case SaveDataType.mo:
                    prefix = "mo";
                    break;
                case SaveDataType.drop12:
                    prefix = "drop12";
                    break;

                case SaveDataType.sc:
                    prefix = "sc";
                    break;
                case SaveDataType.all:
                    prefix = "all";
                    break;

                case SaveDataType.drop3:
                    prefix = "drop3";
                    break;
                case SaveDataType.test:
                    prefix = "test";
                    break;
                default:
                    prefix = "unknown";
                    break;
            }
            Create_dir(folderPath);
            string fileName = DateTime.Now.ToString("yyyy_MM_dd") + prefix + ".csv";
            string filePath = folderPath + "\\" + fileName;
            if (!File.Exists(filePath))
            {
                addHeader = true;
            }
            string header = "";
            string record = "";

            switch (type)
            {
                case SaveDataType.all://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,OPID,Fixture,TabletLabel,Confidence,ImCircular_X,ImCircular_Y,Physical_X,Physical_Y,ScOPID,RecW,RecH,RecA,Drop3OPID";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},\r\n",
                       DateTime.Now,
                       LatestOPID,
                       Read49,
                       //FilteredCount,
                       TabletLabel,
                       TabletConfidence,
                       OpCircularCentroidX,
                       OpCircularCentroidY,
                       ShiftX,
                       ShiftY,
                       ScOPID,
                       //OpCircularWidth,
                       //OpCircularHeight,
                       //OpCircularArea,
                       RecW,
                       RecH,
                       RecA,
                       //MotorCirW,
                       //MotorCirH,
                       //MotorCirA,
                       Drop3OPID
                       );
                    break;
                case SaveDataType.sc://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,OPID,Fixture,Physical_X,Physical_Y";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},\r\n",
                        DateTime.Now,
                        ScOPID,
                        ScFixture,
                        ShiftXsc,
                        ShiftYsc
                        );
                    break;
                case SaveDataType.mo://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,OPID,TabletLabel,TabletConfidence,JudgeTextCircular,MotorCircularCentroidX,MotorCircularCentroidY,OpCircularCentroidX,OpCircularCentroidY,MotorOpCentroidDeltaX,MotorOpCentroidDeltaY,Drop3OPID";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},\r\n",
                        DateTime.Now,
                        LatestOPID,
                        TabletLabel,
                        TabletConfidence,
                        JudgeTextCircular,
                        //MotorCirA,
                        //MotorCirH,
                        //MotorCirW,
                        MotorCircularCentroidX,
                        MotorCircularCentroidY,
                        OpCircularCentroidX,
                        OpCircularCentroidY,
                        //OpCircularArea,
                        //OpCircularHeight,
                        //OpCircularWidth,
                        MotorOpCentroidDeltaX,
                        MotorOpCentroidDeltaY,
                        Drop3OPID
                        );
                    break;
                case SaveDataType.drop3://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,Drop3OPID,Drop3Fixture,DatumPoint_X,DatumPoint_Y,Drop3DatumPoint_W,Drop3DatumPoint_H,Drop3DatumPoint_A,Drop3_W,Drop3_H,Drop3_A,Drop3Judge,Magnet1Brightness,Magnet2Brightness,Magnet3Brightness,Magnet4Brightness,Drop3_mW,Drop3_mH,Drop3_mA";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},\r\n",
                       DateTime.Now,
                       Drop3OPID,
                       Drop3Fixture,
                       DatumPoint_X,
                       DatumPoint_Y,
                       Drop3DatumPoint_W,
                       Drop3DatumPoint_H,
                       Drop3DatumPoint_A,
                       Drop3GreaseBlob_W,
                       Drop3GreaseBlob_H,
                       Drop3GreaseBlob_A,
                       NegativeJudge,
                       Magnet1Brightness,
                       Magnet2Brightness,
                       Magnet3Brightness,
                       Magnet4Brightness,
                       Drop3MinorBlob_W,
                       Drop3MinorBlob_H,
                       Drop3MinorBlob_A
                       );
                    break;
                case SaveDataType.drop12://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,Drop3OPID,Drop3Fixture,DatumPointdrop12_X,DatumPointdrop12_Y,DatumPointdrop12_W,DatumPointdrop12_H,DatumPointdrop12_A,Drop1_W,Drop1_H,Drop1_A,Drop1Judge,Drop2_W,Drop2_H,Drop2_A,Drop2Judge,SumBrightnessDrop12,Drop2_mW,Drop2_mH,Drop2_mA";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},\r\n",
                       DateTime.Now,
                       Drop12OPID,
                       Drop12Fixture,
                       DatumPointDrop12_X,
                       DatumPointDrop12_Y,
                       DatumPointDrop12_W,
                       DatumPointDrop12_H,
                       DatumPointDrop12_A,
                       Drop1GreaseBlob_W,
                       Drop1GreaseBlob_H,
                       Drop1GreaseBlob_A,
                       PositiveJudge,
                       Drop2GreaseBlob_W,
                       Drop2GreaseBlob_H,
                       Drop2GreaseBlob_A,
                       Positive2Judge,
                       SumBrightnessDrop12,
                       Drop2MinorBlob_W,
                       Drop2MinorBlob_H,
                       Drop2MinorBlob_A

                       );
                    break;
                case SaveDataType.test://表格   SaveDataType.ben时，其他位置写-1；如果不分type是否就写在一个文件上?
                    header = "DateTime,Drop3OPID,Drop3Fixture,DatumPoint_X,DatumPoint_Y,Drop3DatumPoint_W,Drop3DatumPoint_H,Drop3DatumPoint_A,Drop3GreaseBlob_W,Drop3GreaseBlob_H,Drop3GreaseBlob_A,NegativeJudge";
                    record += string.Format("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},\r\n",
                        DateTime.Now,
                        Drop3OPID,
                        Drop3Fixture,
                        DatumPoint_X,
                        DatumPoint_Y,
                        Drop3DatumPoint_W,
                        Drop3DatumPoint_H,
                        Drop3DatumPoint_A,
                        Drop3GreaseBlob_W,
                        Drop3GreaseBlob_H,
                        Drop3GreaseBlob_A,
                        NegativeJudge,
                        DatumPointDrop12_X,
                        DatumPointDrop12_Y,
                        DatumPointDrop12_W,
                        DatumPointDrop12_H,
                        DatumPointDrop12_A,
                        Drop1GreaseBlob_W,
                        Drop1GreaseBlob_H,
                        Drop1GreaseBlob_A,
                        PositiveJudge,
                        Drop2GreaseBlob_W,
                        Drop2GreaseBlob_H,
                        Drop2GreaseBlob_A,
                        Positive2Judge

                        );
                    break;

                default:
                    break;
            }

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Append))//打开文件
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))//打开文件后，写
                {
                    if (addHeader)//wen文件不存在时，添加Header
                        sw.WriteLine(header);//文件不存在时，要写头文件
                    //sw.WriteLine(record);
                    sw.Write(record);//写了头文件再写记录

                    sw.Close();
                    fs.Close();//用完后关闭
                }
            }
            catch (Exception ex)
            {
                logger.Error("DataSave|__" + ex.Message);
            }

        }

        #endregion

        #region 清理文件

        /// <summary>
        /// Delete File
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        private bool CleanFile(string path, ushort day)//Day需要填正数。
        {
            try
            {
                //文件夹路径
                DirectoryInfo dyInfo = new DirectoryInfo(path);
                //获取文件夹下所有的文件
                if (Directory.Exists(path))
                {
                    foreach (FileInfo feInfo in dyInfo.GetFiles())
                    {
                        //判断文件日期是否小于Day，是则删除
                        if (feInfo.CreationTime < DateTime.Now.AddDays(-day))//三天前
                        {
                            feInfo.Delete();//删除文件

                        }
                    }
                    foreach (DirectoryInfo dyInfoDelete in dyInfo.GetDirectories())//删除文件夹
                    {
                        //判断文件夹日期是否小于Day，是则删除
                        if (dyInfoDelete.CreationTime < DateTime.Now.AddDays(-day))//三天前
                        {
                            dyInfoDelete.Delete(true);//删除文件夹


                        }
                    }
                }
                else
                {
                    logger.Warn(path + " not exists");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error("CleanFile| " + ex.Message);
                return false;
            }
        }
        /// <summary>
        /// 早晚八点清零
        /// </summary>
        private void CleanCount()
        {
            

                GloPara.TotalMotor = 0;
                GloPara.TotalTablet = 0;
                GloPara.MotorNG = 0;
                GloPara.MotorNGRatio = 0;
                GloPara.MotorNGRatio360 = 0;
                GloPara.TabletNG = 0;
                GloPara.TabletNGRatio = 0;
                GloPara.TabletNGRatio360 = 0;
                SaveGloParaToJsonData();
            

        }

        private void Clean_files()
        {

            if ((DateTime.Now.Hour == 11 && DateTime.Now.Minute == 40) ||
                (DateTime.Now.Hour == 17 && DateTime.Now.Minute == 35) ||
                (DateTime.Now.Hour == 20 && DateTime.Now.Minute == 45 ||
                DiskMg.UsedSpaceRatio > 0.95))
            {
                if (DiskMg.UsedSpaceRatio > 0.6) //磁盘占用率大于60%时删除N天前文件
                {
                    Task.Run(() => CleanAllFolderAndFile(GloPara.CleanDay_long, GloPara.CleanDay_short));
                }
            }
            //if (DiskMg.UsedSpaceRatio > 0.95) //磁盘占用率大于95%时删除N天前文件
            //{
            //    Task.Run(() => CleanAllFolderAndFile(GloPara.CleanDay_long, GloPara.CleanDay_short));
            //}
        }

        private void CleanAllFolderAndFile(ushort longday, ushort shortday)
        {
            ////挂起
            //foreach (Thread i in thread)
            //{
            //    i.Suspend();
            //}
            suspendRequested = Clean_files_flag = true;
            Thread.Sleep(1500);//延迟1.5s执行，确保线程都运行完后，暂停线程，suspendRequested----暂停线程用

            logger.Info("清理文件中");

            CleanFile(Screw_motor_images_dir, longday);
            CleanFile(Screw_error_images_dir, shortday);
            CleanFile(Screw_original_images_dir, shortday);
            CleanFile(Screw_tablet_images_dir, shortday);

            CleanFile(Folder_Logs_Debug_logs, longday);
            CleanFile(Folder_Logs_Error_logs, longday);
            CleanFile(Folder_Logs_Warn_logs, longday);
            CleanFile(Folder_Logs_Info_logs, longday);
            CleanFile(@"Data\\", longday);
            /////////////////////油脂的
            CleanFile(Drop1_images_dir, longday);
            //第二滴
            CleanFile(Drop2_images_dir, longday);
            //第三滴
            CleanFile(Drop3_images_dir, longday);
            //删除原图吧。没什么用，占内存
            CleanFile(Grease_error_images_dir, shortday);
            CleanFile(Drop3_face_images_dir, shortday);
            CleanFile(Drop12_face_images_dir, shortday);


            suspendRequested = Clean_files_flag = false;
            DiskMg.GetDiskSpaceInfo();//更新磁盘信息。
            logger.Info("清理文件完");

            //解除挂起线程
            //foreach (Thread i in thread)
            //{
            //    i.Resume();
            //}



        }


        #endregion

        #region 全局按键
        /// <summary>
        /// 按下Tab，保存Json设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DivideKeyDownHandler(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Divide)
            {


            }
        }
        /// <summary>
        /// 按下=，隐藏or显示设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OemplusKeyDownHandler(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Oemplus)
            {
            }
        }
        #endregion

        #region 方法



        public static bool MaxAreaBlob(Mat binImg, ref ConnectedComponents.Blob rect)
        {

            ConnectedComponents cc = Cv2.ConnectedComponentsEx(binImg);
            //int num = cc.LabelCount;  //cc1连通组件的数量

            //判断选取符合孔的高宽
            //if (num > 1)
            //{
            //    int Areamax = 0;
            //    for (int i = 0; i < num; i++)
            //    {
            //        int Area = cc.Blobs[i].Area;
            //        Console.WriteLine(i + "," + Area);
            //        if (Area > Areamax)
            //        {
            //            Areamax = Area;
            //            rect = cc.Blobs[i].Rect;
            //        }
            //    }
            //}
            int Areamax = 0;

            foreach (var recblob in cc.Blobs.Skip(1))
            {
                int Area = recblob.Area;
                if (Area > Areamax)
                {
                    Areamax = Area;
                    rect = recblob;
                }
            }


            return true;
        }

        /// <summary>
        /// 用于填充区域
        /// </summary>
        /// <param name="src"></param>
        /// <param name="order_h"></param>
        /// <param name="range_h"></param>
        /// <param name="order_w"></param>
        /// <param name="range_w"></param>
        private void FillBlob(Mat src, int order_h, int range_h, int order_w, int range_w)
        {

            // draw contours
            src.FindContours(out Point[][] contours, out HierarchyIndex[] hierarchyIndexes, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
            List<Point[]> contours_filtered = new List<Point[]>();  // 用于存储根据条件筛选过后的区域对象

            // 用于筛选blob尺寸的条件

            foreach (Point[] c in contours)
            {
                RotatedRect minRect = Cv2.MinAreaRect(c);
                // 根据高、宽筛选
                if (Math.Abs(minRect.Size.Height - order_h) <= range_h
                        && Math.Abs(minRect.Size.Width - order_w) <= range_w)
                {
                    contours_filtered.Add(c);
                }
            }
            // 填充区域(第5个参数thickness设置为-1时表示填充, 大于0时仅在边缘描出厚度为thickness的线）
            Cv2.DrawContours(src, contours_filtered, -1, Scalar.White, -1);


        }

        /// <summary>
        /// 检测当前内存
        /// </summary>
        /// <returns></returns>
        private string Get_memory()//准
        {

            try
            {
                using (PerformanceCounter pf1 = new PerformanceCounter("Process", "Working Set - Private", "Screw"))//第二个参数就是得到只有工作集
                {
                    long b = (long)pf1.NextValue();
                    for (int i = 0; i < 2; i++)
                    {
                        b /= 1024;
                    }
                    //string text = $"{ pf1.NextValue() / 1024}";
                    return b + "MB";
                }
            }
            catch (Exception ex)
            {
                logger.Error("get_memory|_" + ex);
                return "err_" + "MB";

            }


        }
        /// <summary>
        /// 获取当前总内存
        /// </summary>
        /// <returns></returns>
        private string Get_sum_memory()
        {
            try
            {
                using (ManagementClass cimobject1 = new ManagementClass("Win32_PhysicalMemory"))
                using (ManagementObjectCollection moc1 = cimobject1.GetInstances())
                {
                    double capacity = 0;
                    foreach (ManagementObject mo1 in moc1)
                    {
                        capacity += ((Math.Round(Int64.Parse(mo1.Properties["Capacity"].Value.ToString()) / 1024 / 1024 / 1024.0, 1)));
                    }

                    return capacity + "Gb";
                }

            }
            catch (Exception ex)
            {
                logger.Error("get_sum_memory|_" + ex);
                return "err_" + "Gb";

            }

        }

        /// <summary>
        /// 获取当前剩余内存
        /// </summary>
        /// <returns></returns>
        private string Get_available_memory()
        {
            try
            {
                using (ManagementClass cimobject2 = new ManagementClass("Win32_PerfFormattedData_PerfOS_Memory"))
                using (ManagementObjectCollection moc2 = cimobject2.GetInstances())
                {
                    double available = 0;
                    foreach (ManagementObject mo2 in moc2)
                    {
                        available += ((Math.Round(Int64.Parse(mo2.Properties["AvailableMBytes"].Value.ToString()) / 1024.0, 1)));

                    }


                    return available + "Gb";
                }

            }
            catch (Exception ex)
            {
                logger.Error("get_available_memory|_" + ex);
                return "err_" + "Gb";

            }

        }




        /// <summary>
        /// 创建文件夹用
        /// </summary>
        /// <param name="path"></param>
        private void Create_dir(string path)
        {
            if (!Directory.Exists(path))   //如果文件夹不存在则创建
            {
                Directory.CreateDirectory(path);
            }
        }
        private void Open_folder(string path)
        {
            if (Directory.Exists(path))   //如果文件夹不存在则创建
            {
                System.Diagnostics.Process.Start(path);
            }
            else
            {
                MessageBox.Show("没有此文件夹!");
            }
        }


        /// <summary>
        /// 画框防止超出图片范围
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="map"></param>
        /// <returns></returns>
        public static int[] RoiPointLimit(int x, int y, int w, int h, BitmapSource map)
        {
            int[] xywh = new int[4];
            xywh[2] = w;
            xywh[3] = h;
            if (x < 0)
            {
                xywh[0] = 0;
                xywh[2] = (w + x) < 0 ? w : w + x;
            }
            else if (x > map.Width)
            {
                xywh[0] = (int)map.Width - w;
                xywh[2] = w;
            }
            else
            {
                xywh[0] = x;
                if (w + x > map.Width)
                {
                    xywh[2] = (int)map.Width - x;
                }
            }
            if (y < 0)
            {
                xywh[1] = 0;
                xywh[3] = (h + y) < 0 ? h : h + y;

            }
            else if (y > map.Height)
            {
                xywh[1] = (int)map.Height - h;
                xywh[3] = h;
            }
            else
            {
                xywh[1] = y;
                if (h + y > map.Height)
                {
                    xywh[3] = (int)map.Height - y;
                }
            }
            return xywh;
        }


        /// <summary>
        /// 队列弄OPID,,给测量油脂用
        /// </summary>
        /// <param name="que"></param>
        /// <param name="Enqueuevalue"></param>
        /// <param name="Dequeuevalue"></param>
        /// <param name="Count"></param>
        private string QueueValue(Queue<string> que, string enqueuevalue, ushort count)
        {
            que.Enqueue(enqueuevalue);

            while (que.Count > count + 1)
            {
                _ = que.Dequeue();

            }


            return que.Count > count ? que.Dequeue() : "0";
        }
        private double QueueValue(Queue<double> que, double enqueuevalue, ushort count)
        {
            que.Enqueue(enqueuevalue);

            while (que.Count > count + 1)
            {
                _ = que.Dequeue();

            }

            return que.Count > count ? que.Dequeue() : 0;
        }
        /// <summary>
        /// 队列弄治具号
        /// </summary>
        /// <param name="que"></param>
        /// <param name="Enqueuevalue"></param>
        /// <param name="Dequeuevalue"></param>
        /// <param name="Count"></param>
        private ushort QueueValue(Queue<ushort> que, ushort enqueuevalue, ushort count)
        {
            que.Enqueue(enqueuevalue);

            while (que.Count > count + 1)
            {
                _ = que.Dequeue();

            }

            return que.Count > count ? que.Dequeue() : (ushort)0;

        }





        ///计算图片平均灰度，也可标准差
        private double GetGrayAvg(Mat mat)
        {
            try
            {
                //using (Mat gray_roi = mat.Channels() == 3 ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat)
                using (Mat img = mat.Channels() == 3 ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat)
                using (Mat mean = new Mat())
                using (Mat stdDev = new Mat())
                {
                    //if (mat.Channels() == 3)
                    //{
                    //    Cv2.CvtColor(mat, img, ColorConversionCodes.BGR2GRAY);
                    //}
                    //else
                    //{
                    //    img = mat;
                    //}
                    //Cv2.Mean(mat);
                    Cv2.MeanStdDev(img, mean, stdDev);
                    Scalar AvgBot = mean.Mean();
                    Scalar StdBot = stdDev.Mean();

                    logger.Info("Avg: " + AvgBot + "Std: " + StdBot);
                    return AvgBot.Val0;
                }


            }
            catch (Exception ex)
            {
                logger.Error("GetGrayAvg Run Fail!" + ex.Message);
                return 0;
            }

        }


        /// <summary>
        /// 保存所有JsonData设置
        /// </summary>
        void SaveAllJsonData()
        {
            SavePLCToJsonData();
            SaveScrewMoParameterToJsonData();
            SaveGloParaToJsonData();
            //SaveSerialToJsonData();
        }
        /// <summary>
        /// ReadOpId 读txt里的OPID，读完后写空格覆盖文档
        /// </summary>
        private void ReadOPID()
        {
            try
            {
                string opid_now;
                opid_now = Read_txt(GloPara.OPIDFile);
                if (opid_now != "" || opid_now != null)//为防止复位后，自己先读
                {
                    GloPara.TotalTablet += 1;
                    GloPara.TotalMotor += 1;
                }
                OPID = opid_now;
                Write_txt(GloPara.OPIDFile, "");//读完后写空格，是覆盖的，不是转行写的，写空格防止复位后OPID一致导致移位错乱


            }

            catch (Exception ex)
            {
                logger.Error("ReadOPID| " + ex.Message);
                OPID = "00000000000000";
            }

        }

        private string Read_txt(string file_path)
        {
            try
            {
                string line = "";
                string li;
                using (StreamReader sr = new StreamReader(File.Open(file_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {

                    while ((li = sr.ReadLine()) != null)
                    {
                        //logger.Trace(li);
                        line = li;
                        //相当于读到最末尾了。
                    }
                    return line.Trim();//去除对于空格

                }

            }
            catch (Exception ex)
            {
                logger.Error("read_txt| " + ex.Message);

                return "";
            }

        }

        //Write file with steamwrite
        private void Write_txt(string file_path, string write_content)
        {

            try
            {
                using (FileStream fs = new FileStream(file_path, FileMode.Create))//Create就是创建，会覆盖
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine(write_content);
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                    //logger.Trace(write_content);
                }

            }
            catch (Exception ex)
            {


                logger.Error("write_txt| " + ex.Message);

            }
        }


        /// <summary>
        /// 选择图片
        /// </summary>
        private BitmapSource SelectLocalImage()
        {
            BitmapSource bitmapSource = null;
            try
            {
                Microsoft.Win32.OpenFileDialog openfiledialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.png;*.jpeg;*.bmp;*.gif|所有文件|*.*"//限定文件类型？
                };

                if ((bool)openfiledialog.ShowDialog())//如果点击确定
                {
                    ImagePath = openfiledialog.FileName;//选择的文件名给属性。。

                    using (Mat Image = new Mat(ImagePath))
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



        #endregion

        #region Command

        /// <summary>
        /// Unlock command
        /// </summary>
        private ICommand _UnlockCommand;
        public ICommand UnlockCommand
        {
            get
            {
                if (_UnlockCommand == null)
                {
                    _UnlockCommand = new RelayCommand(
                        param => this.UnlockExecute(),
                        param => this.CanUnlock()
                    );
                }
                return _UnlockCommand;
            }
        }

        private bool CanUnlock()
        {
            return true;
        }
        private void UnlockExecute()
        {

            if (Password == GloPara.Password)
            {
                VisibilitySettings = true;

                Password = "";
                for (int i = 0; i < ImageObjects.Count; i++)
                {
                    ImageObjects[i].RoiAdjOn = true;
                }
            }
            else
            {
                logger.Warn("密码错误!!!!!");
                VisibilitySettings = false;
                for (int i = 0; i < ImageObjects.Count; i++)
                {
                    ImageObjects[i].RoiAdjOn = false;
                }
            }

        }


        /// <summary>
        /// 基准点范围的roi
        /// </summary>
        private ICommand _DatumRangeRoiSettingCommand;
        public ICommand DatumRangeRoiSettingCommand
        {
            get
            {
                if (_DatumRangeRoiSettingCommand == null)
                {
                    _DatumRangeRoiSettingCommand = new RelayCommand(
                        param => this.UseDatumRangeRoiSetting(),
                        param => this.CanDatumRangeRoiSetting()
                    );
                }
                return _DatumRangeRoiSettingCommand;
            }
        }
        private bool CanDatumRangeRoiSetting()
        {
            return true;
        }
        private void UseDatumRangeRoiSetting()
        {


            int[] xywh = RoiPointLimit(ImageObjects[5].MarkLeft, ImageObjects[5].MarkTop, ImageObjects[5].MarkWidth, ImageObjects[5].MarkHeight, ImageObjects[5].DisplayImage);


            ScrewPara.Roi_DatumPointRange_X = xywh[0];
            ScrewPara.Roi_DatumPointRange_Y = xywh[1];
            ScrewPara.Roi_DatumPointRange_W = xywh[2];
            ScrewPara.Roi_DatumPointRange_H = xywh[3];



            using (Mat mat = ImageObjects[5].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(xywh[0], xywh[1], xywh[2], xywh[3]), Scalar.RandomColor(), 25, LineTypes.AntiAlias, 0);//1
                ImageObjects[5].DisplayImage = mat.ToBitmapSource();
                Task.Run(() => MessageBox.Show("记得保存设置！！！\n(#｀-_ゝ-)", "Tip", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));
            }
        }
        /// <summary>
        /// 马达位置的roi
        /// </summary>
        private ICommand _AMotorRoiSettingCommand;
        public ICommand AMotorRoiSettingCommand
        {
            get
            {
                if (_AMotorRoiSettingCommand == null)
                {
                    _AMotorRoiSettingCommand = new RelayCommand(
                        param => this.UseAMotorRoiSetting(),
                        param => this.CanAMotorRoiSetting()
                    );
                }
                return _AMotorRoiSettingCommand;
            }
        }
        private bool CanAMotorRoiSetting()
        {
            return true;
        }
        private void UseAMotorRoiSetting()
        {
            int[] xywh = new int[4];
            xywh = RoiPointLimit(ImageObjects[5].MarkLeft, ImageObjects[5].MarkTop, ImageObjects[5].MarkWidth, ImageObjects[5].MarkHeight, ImageObjects[5].DisplayImage);


            ScrewPara.Roi_Motor_L = xywh[0];
            ScrewPara.Roi_Motor_T = xywh[1];
            ScrewPara.Roi_Motor_W = xywh[2];
            ScrewPara.Roi_Motor_H = xywh[3];

            using (Mat mat = ImageObjects[5].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(xywh[0], xywh[1], xywh[2], xywh[3]), Scalar.RandomColor(), 25, LineTypes.AntiAlias, 0);//1
                ImageObjects[5].DisplayImage = mat.ToBitmapSource();
                Task.Run(() => MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)", "Tip", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));
            }

        }

        /// <summary>
        /// 压杆位置的roi
        /// </summary>
        private ICommand _ATabletRoiSettingCommand;
        public ICommand ATabletRoiSettingCommand
        {
            get
            {
                if (_ATabletRoiSettingCommand == null)
                {
                    _ATabletRoiSettingCommand = new RelayCommand(
                        param => this.UseATabletRoiSetting(),
                        param => this.CanATabletRoiSetting()
                    );
                }
                return _ATabletRoiSettingCommand;
            }
        }
        private bool CanATabletRoiSetting()
        {
            return true;
        }
        private void UseATabletRoiSetting()
        {

            int[] xywh = new int[4];
            xywh = RoiPointLimit(ImageObjects[5].MarkLeft, ImageObjects[5].MarkTop, ImageObjects[5].MarkWidth, ImageObjects[5].MarkHeight, ImageObjects[5].DisplayImage);


            ScrewPara.Roi_ATablet_X = xywh[0];
            ScrewPara.Roi_ATablet_Y = xywh[1];
            ScrewPara.Roi_ATablet_W = xywh[2];
            ScrewPara.Roi_ATablet_H = xywh[3];

            using (Mat mat = ImageObjects[5].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(xywh[0], xywh[1], xywh[2], xywh[3]), Scalar.RandomColor(), 25, LineTypes.AntiAlias, 0);//1
                ImageObjects[5].DisplayImage = mat.ToBitmapSource();
                Task.Run(() => MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)", "Tip", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));
            }
        }
        /// <summary>
        /// 圆孔的roi
        /// </summary>
        private ICommand _CircularRoiSettingCommand;
        public ICommand CircularRoiSettingCommand
        {
            get
            {
                if (_CircularRoiSettingCommand == null)
                {
                    _CircularRoiSettingCommand = new RelayCommand(
                        param => this.UseCircularRoiSetting(),
                        param => this.CanCircularRoiSetting()
                    );
                }
                return _CircularRoiSettingCommand;
            }
        }
        private bool CanCircularRoiSetting()
        {
            return true;
        }
        private void UseCircularRoiSetting()
        {
            int[] xywh = new int[4];
            xywh = RoiPointLimit(ImageObjects[5].MarkLeft, ImageObjects[5].MarkTop, ImageObjects[5].MarkWidth, ImageObjects[5].MarkHeight, ImageObjects[5].DisplayImage);


            ScrewPara.Roi_Circular_L = xywh[0];
            ScrewPara.Roi_Circular_T = xywh[1];
            ScrewPara.Roi_Circular_W = xywh[2];
            ScrewPara.Roi_Circular_H = xywh[3];

            using (Mat mat = ImageObjects[5].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(xywh[0], xywh[1], xywh[2], xywh[3]), Scalar.RandomColor(), 25, LineTypes.AntiAlias, 0);//1
                ImageObjects[5].DisplayImage = mat.ToBitmapSource();
                Task.Run(() => MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)", "Tip", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));
            }

        }



        /// <summary>
        /// Save JsonCommand
        /// </summary>
        private ICommand _SaveParaCommand;

        public ICommand SaveParaCommand
        {
            get
            {
                if (_SaveParaCommand == null)
                {
                    _SaveParaCommand = new RelayCommand(
                        param => this.UseSavePara(),
                        param => this.CanSavePara()
                    );
                }
                return _SaveParaCommand;
            }
        }
        private bool CanSavePara()
        {
            return true;
        }
        private void UseSavePara()
        {

            try
            {
                if (MessageBox.Show("确认要保存配置？\n点击备份了吗？\nw(ﾟДﾟ)w\n…(⊙_⊙;)…", "清理", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    logger.Info("取消保存");
                }
                else
                {
                    if (CopyDir(CreateJsonFolder, "ConfigsBackup\\AutoSave\\" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss") + "\\" + CreateJsonFolder + "\\"))
                    {
                        MessageBox.Show("备份点了吗？\n点存之前也会备份的！\n欸，就防止你不点备份！\nψ(｀∇´)ψ\t(～￣▽￣)～");
                        SaveAllJsonData();
                        SaveGreaseMorParaToJsonData();
                        SaveImageOjbects();
                        MessageBox.Show("已保存设定\n┌( ´_ゝ` )┐", "保存设定");
                    }
                    else
                    {
                        MessageBox.Show("备份失败，保存设定也不能！\n找售后吧！！！\n(❁´◡`❁)", "保存设定");
                    }
                }


            }
            catch (Exception ex)
            {
                logger.Error("UseSaveGreaseParameters|__" + ex.Message);
                MessageBox.Show("备份失败，保存设定也不能！\n找售后吧！！！\n(❁´◡`❁)", "保存设定");

            }


        }

        /// <summary>
        /// 读设置
        /// </summary>
        private ICommand _LoadParaCommand;

        public ICommand LoadParaCommand
        {
            get
            {
                if (_LoadParaCommand == null)
                {
                    _LoadParaCommand = new RelayCommand(
                        param => this.UseLoadPara(),
                        param => this.CanLoadPara()
                    );
                }
                return _LoadParaCommand;
            }
        }
        private bool CanLoadPara()
        {
            return true;
        }
        private void UseLoadPara()
        {
            LoadGreaseMorParaJsonData();
            //LoadImageObjects();
            LoadPLCJsonData();
            LoadScrewMoParameterJsonData();
            LoadGloParaJsonData();

        }


        private ICommand _OpenImageFolderCommand;

        public ICommand OpenImageFolderCommand
        {
            get
            {
                if (_OpenImageFolderCommand == null)
                {
                    _OpenImageFolderCommand = new RelayCommand(
                        param => this.UseOpenImageFolder(),
                        param => this.CanOpenImageFolder()
                    );
                }
                return _OpenImageFolderCommand;
            }
        }
        private bool CanOpenImageFolder()
        {
            return true;
        }
        private void UseOpenImageFolder()
        {

            Open_folder(GloPara.Header_folder + "\\Images\\");

        }


        private ICommand _OpenMotorImageFolderCommand;

        public ICommand OpenMotorImageFolderCommand
        {
            get
            {
                if (_OpenMotorImageFolderCommand == null)
                {
                    _OpenMotorImageFolderCommand = new RelayCommand(
                        param => this.UseOpenMotorImageFolder(),
                        param => this.CanOpenMotorImageFolder()
                    );
                }
                return _OpenMotorImageFolderCommand;
            }
        }
        private bool CanOpenMotorImageFolder()
        {
            return true;
        }
        private void UseOpenMotorImageFolder()
        {

            Open_folder(Screw_motor_images_dir);

        }
        private ICommand _OpenDrop3ImageFolderCommand;

        public ICommand OpenDrop3ImageFolderCommand
        {
            get
            {
                if (_OpenDrop3ImageFolderCommand == null)
                {
                    _OpenDrop3ImageFolderCommand = new RelayCommand(
                        param => this.UseOpenDrop3ImageFolder(),
                        param => this.CanOpenDrop3ImageFolder()
                    );
                }
                return _OpenDrop3ImageFolderCommand;
            }
        }
        private bool CanOpenDrop3ImageFolder()
        {
            return true;
        }
        private void UseOpenDrop3ImageFolder()
        {

            Open_folder(Drop3_face_images_dir);

        }
        private ICommand _OpenDrop12ImageFolderCommand;

        public ICommand OpenDrop12ImageFolderCommand
        {
            get
            {
                if (_OpenDrop12ImageFolderCommand == null)
                {
                    _OpenDrop12ImageFolderCommand = new RelayCommand(
                        param => this.UseOpenDrop12ImageFolder(),
                        param => this.CanOpenDrop12ImageFolder()
                    );
                }
                return _OpenDrop12ImageFolderCommand;
            }
        }
        private bool CanOpenDrop12ImageFolder()
        {
            return true;
        }
        private void UseOpenDrop12ImageFolder()
        {

            Open_folder(Drop12_face_images_dir);

        }


        private ICommand _OpenPressImageFolderCommand;

        public ICommand OpenPressImageFolderCommand
        {
            get
            {
                if (_OpenPressImageFolderCommand == null)
                {
                    _OpenPressImageFolderCommand = new RelayCommand(
                        param => this.UseOpenPressImageFolder(),
                        param => this.CanOpenPressImageFolder()
                    );
                }
                return _OpenPressImageFolderCommand;
            }
        }
        private bool CanOpenPressImageFolder()
        {
            return true;
        }
        private void UseOpenPressImageFolder()
        {

            Open_folder(Screw_tablet_images_dir);

        }


        private ICommand _OpenMinorDrop1ImageFolderCommand;

        public ICommand OpenMinorDrop1ImageFolderCommand
        {
            get
            {
                if (_OpenMinorDrop1ImageFolderCommand == null)
                {
                    _OpenMinorDrop1ImageFolderCommand = new RelayCommand(
                        param => this.UseOpenMinorDrop1ImageFolder(),
                        param => this.CanOpenMinorDrop1ImageFolder()
                    );
                }
                return _OpenMinorDrop1ImageFolderCommand;
            }
        }
        private bool CanOpenMinorDrop1ImageFolder()
        {
            return true;
        }
        private void UseOpenMinorDrop1ImageFolder()
        {

            Open_folder(Drop1_images_dir);

        }



        private ICommand _OpenMinorDrop2ImageFolderCommand;

        public ICommand OpenMinorDrop2ImageFolderCommand
        {
            get
            {
                if (_OpenMinorDrop2ImageFolderCommand == null)
                {
                    _OpenMinorDrop2ImageFolderCommand = new RelayCommand(
                        param => this.UseOpenMinorDrop2ImageFolder(),
                        param => this.CanOpenMinorDrop2ImageFolder()
                    );
                }
                return _OpenMinorDrop2ImageFolderCommand;
            }
        }
        private bool CanOpenMinorDrop2ImageFolder()
        {
            return true;
        }
        private void UseOpenMinorDrop2ImageFolder()
        {

            Open_folder(Drop2_images_dir);

        }



        private ICommand _OpenMinorDrop3ImageFolderCommand;

        public ICommand OpenMinorDrop3ImageFolderCommand
        {
            get
            {
                if (_OpenMinorDrop3ImageFolderCommand == null)
                {
                    _OpenMinorDrop3ImageFolderCommand = new RelayCommand(
                        param => this.UseOpenMinorDrop3ImageFolder(),
                        param => this.CanOpenMinorDrop3ImageFolder()
                    );
                }
                return _OpenMinorDrop3ImageFolderCommand;
            }
        }
        private bool CanOpenMinorDrop3ImageFolder()
        {
            return true;
        }
        private void UseOpenMinorDrop3ImageFolder()
        {
            Open_folder(Drop3_images_dir);


        }


        public ICommand OpenBackupFolderCommand => new RelayCommand(obj =>
        {
            Open_folder(@"ConfigsBackup");

        });
        public ICommand OpenConfigFolderCommand => new RelayCommand(obj =>
        {
            Open_folder(@"Config");

        });


        public ICommand OpenDataFolderCommand => new RelayCommand(obj =>
        {
            Open_folder(@"Data\\");

        });

        public ICommand OpenLogesFolderCommand => new RelayCommand(obj =>
        {
            Open_folder(@"logs\\");

        });


        private ICommand _CorrectionCommand;

        public ICommand CorrectionCommand
        {
            get
            {
                if (_CorrectionCommand == null)
                {
                    _CorrectionCommand = new RelayCommand(
                        param => this.UseCorrect(),
                        param => this.CanCorrect()
                    );
                }
                return _CorrectionCommand;
            }
        }
        private bool CanCorrect()
        {
            return true;
        }
        private void UseCorrect()
        {
            try
            {
                Task.Run(() =>
                {
                    if (SpinCtrl.AcquisitionBitmapFromCam(0, out BitmapSource temp))
                    {
                        SourceImage = temp;
                        ImageObjects[5].DisplayImage = SourceImage.Clone();
                        RotateImage_Screw = Rotate_arbitrarily_angle(SourceImage, ScrewPara.RotateAngle_Screw);
                        ImageObjects[5].DisplayImage = RotateImage_Screw.Clone();
                        TestMasterCircularXY(RotateImage_Screw);
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 51, 1, new ushort[] { (ushort)ShiftX });
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 53, 1, new ushort[] { (ushort)ShiftY });
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 11 });
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Error("UseCorrect|_" + ex);
            }

        }



        private ICommand _UseLocalImageCommand;

        public ICommand UseLocalImageCommand
        {
            get
            {
                if (_UseLocalImageCommand == null)
                {
                    _UseLocalImageCommand = new RelayCommand(
                        param => this.UseUseLocalImage(),
                        param => this.CanUseLocalImage()
                    );
                }
                return _UseLocalImageCommand;
            }
        }
        private bool CanUseLocalImage()
        {
            return true;
        }
        private void UseUseLocalImage()
        {
            LocalImageProcess();
        }






        /// <summary>
        /// 清理文件数据
        /// </summary>
        private ICommand _CleanCommand;

        public ICommand CleanCommand
        {
            get
            {
                if (_CleanCommand == null)
                {
                    _CleanCommand = new RelayCommand(
                        param => this.UseClean(),
                        param => this.CanClean()
                    );
                }
                return _CleanCommand;
            }
        }
        private bool CanClean()
        {
            return true;
        }

        private void UseClean()
        {
            if (MessageBox.Show($"确认要清理指定日期前的图片数据？\n {GloPara.CleanDay_long} 天前的较重要文件\n{GloPara.CleanDay_short} 天前的非重要文件\n¯\\(°_o)/¯\n…(⊙_⊙;)…", "清理", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
            {
                logger.Info("清理取消");

            }
            else
            {
                if (MessageBox.Show("确认要清理？\nw(ﾟДﾟ)w\n…(⊙_⊙;)…", "清理", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    logger.Info("清理取消");

                }
                else
                {
                    Task.Run(() => CleanAllFolderAndFile(GloPara.CleanDay_long, GloPara.CleanDay_short));
                }


            }
        }


        private ICommand _CalculateCommand;

        public ICommand CalculateCommand
        {
            get
            {
                if (_CalculateCommand == null)
                {
                    _CalculateCommand = new RelayCommand(
                        param => this.UseCalculate(),
                        param => this.CanCalculate()
                    );
                }
                return _CalculateCommand;
            }
        }
        private bool CanCalculate()
        {
            return true;
        }
        private void UseCalculate()
        {

            //Result = Divisor / (Subtrahend - Minuend);
            GloPara.RobotAngleDelta = Math.Atan2((-GloPara.MasterHole1_X) - (-GloPara.MasterHole2_X), GloPara.MasterHole1_Y - GloPara.MasterHole2_Y);



        }


        private ICommand _OriginCalibration2Command;

        public ICommand OriginCalibration2Command
        {
            get
            {
                if (_OriginCalibration2Command == null)
                {
                    _OriginCalibration2Command = new RelayCommand(
                        param => this.UseOriginCalibration2(),
                        param => this.CanOriginCalibration2()
                    );
                }
                return _OriginCalibration2Command;
            }
        }
        private bool CanOriginCalibration2()
        {
            return true;
        }
        private void UseOriginCalibration2()
        {
            try
            {
                BitmapSource temp;
                if (SpinCtrl.AcquisitionBitmapFromCam(0, out temp))
                {
                    GloPara.RobotAngleDelta = Math.Atan2((-GloPara.MasterHole1_X) - (-GloPara.MasterHole2_X), GloPara.MasterHole1_Y - GloPara.MasterHole2_Y);

                    SourceImage = temp;
                    ImageObjects[5].DisplayImage = SourceImage;
                    RotateImage_Screw = Rotate_arbitrarily_angle(SourceImage, ScrewPara.RotateAngle_Screw);
                    ImageObjects[5].DisplayImage = RotateImage_Screw;

                    Task.Run(() => OriginCalibration2(RotateImage_Screw));
                    //机器臂XY的偏差角度
                    MessageBox.Show("记得按下保存");
                }
            }
            catch (Exception ex)
            {
                logger.Error("UseOriginCalibration2|_" + ex);
            }

        }
        public ICommand UseImageOriginCalibration2Command => new RelayCommand(obj =>
        {

            //LocalImage = SelectLocalImage();


            string imageFileNmae = UselocalImageFlie();



            //LocalImage = SelectLocalImage();
            if (imageFileNmae != null)
            {
                using (Mat mat = new Mat(imageFileNmae, ImreadModes.Unchanged))
                {
                    LocalImage = mat.ToBitmapSource();
                    RotateImage_Screw = Rotate_arbitrarily_angle(LocalImage, ScrewPara.RotateAngle_Screw);

                    Task.Run(() => OriginCalibration2(RotateImage_Screw));

                }
            }


            //if (LocalImage != null)
            //{


            //}
        });


        public ICommand UseImageTestMasterCircularXYCommand => new RelayCommand(obj =>
        {
            //LocalImage = SelectLocalImage();


            string imageFileNmae = UselocalImageFlie();
            if (imageFileNmae != null)
            {
                using (Mat mat = new Mat(imageFileNmae, ImreadModes.Unchanged))
                {
                    LocalImage = mat.ToBitmapSource();
                    RotateImage_Screw = Rotate_arbitrarily_angle(LocalImage, ScrewPara.RotateAngle_Screw);

                    Task.Run(() => TestMasterCircularXY(RotateImage_Screw));

                }
            }

            //if (LocalImage != null)
            //{


            //}
        });




        private ICommand _StartProcessCommand;
        public ICommand StartProcessCommand
        {
            get
            {
                if (_StartProcessCommand == null)
                {
                    _StartProcessCommand = new RelayCommand(
                        param => this.UseStartProcess(),
                        param => this.CanStartProcess()
                    );
                }
                return _StartProcessCommand;
            }
        }
        private bool CanStartProcess()
        {
            return !GloPara.AutoStart;
        }
        private void UseStartProcess()
        {
            GloPara.AutoStart = true;
            SaveGloParaToJsonData();
            Task.Run(() => MainThread());
        }

        #endregion

        ///////GREASE油脂的

        #region Field

        private static string GreaseMorphologicalParametersFile = CreateJsonFolder + "\\GreasePartParameters.config";
        private bool LoadGreaseMorParaJsonData()
        {
            try
            {
                if (File.Exists(GreaseMorphologicalParametersFile))
                {
                    GreasePara = JsonConvert.DeserializeObject<GreasePartParameters>(File.ReadAllText(GreaseMorphologicalParametersFile), new JsonSerializerSettings//修改parameters为自己需要存储的文件就OK？
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
                    Create_dir(CreateJsonFolder);
                    GreasePara = new GreasePartParameters
                    {
                        //第一滴
                        Drop1GreaseBlobThresh = 233,
                        Drop1GreaseBlobMinimum_W = 50,//小于这个就没了
                        Drop1GreaseBlobLower_W = 259,//小于这个为少
                        Drop1GreaseBlobUpper_W = 330,//大于这个为多
                        Drop1GreaseBlobMinimum_H = 0,
                        Drop1GreaseBlobUpper_H = 500,
                        Drop1GreaseBlobMinimum_A = 5500,
                        Drop1GreaseBlobLower_A = 6575,
                        Drop1GreaseBlobUpper_A = 11700,
                        //第二滴
                        Drop2GreaseBlobThresh = 233,
                        Drop2GreaseBlobMinimum_W = 40,//小于这个就没了
                        Drop2GreaseBlobLower_W = 125,//小于这个为少
                        Drop2GreaseBlobUpper_W = 300,//大于这个为多
                        Drop2GreaseBlobMinimum_H = 0,
                        Drop2GreaseBlobUpper_H = 500,
                        Drop2GreaseBlobMinimum_A = 2500,
                        Drop2GreaseBlobLower_A = 5500,
                        Drop2GreaseBlobUpper_A = 6900,
                        //第三滴
                        Drop3GreaseBlobThresh = 233,
                        Drop3GreaseBlobMinimum_W = 40,
                        Drop3GreaseBlobLower_W = 118,
                        Drop3GreaseBlobUpper_W = 300,
                        Drop3GreaseBlobMinimum_H = 0,
                        Drop3GreaseBlobUpper_H = 500,
                        Drop3GreaseBlobMinimum_A = 2500,
                        Drop3GreaseBlobLower_A = 3300,
                        Drop3GreaseBlobUpper_A = 5150,
                        //找点
                        DatumPointBinValue = 233,
                        //DatumPointWidth = 283,
                        //DatumPointWidthLimit = 50,
                        //DatumPointHeight = 186,
                        //DatumPointHeightLimit = 50,
                        //DatumPointArea = 42406,
                        //DatumPointAreaLimit = 8000
                    };


                    SaveGreaseMorParaToJsonData();
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
        /// Save SaveGreaseMorParaToJsonData setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveGreaseMorParaToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(GreaseMorphologicalParametersFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, GreasePara, typeof(GreasePartParameters));//修改parameters为自己需要存储的类的属性和命令
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

        #region Property



        private Queue<bool> Drop1GreaseContinuousBadQueue = new Queue<bool>();
        private Queue<bool> Drop2GreaseContinuousBadQueue = new Queue<bool>();
        private Queue<bool> Drop3GreaseContinuousBadQueue = new Queue<bool>();


        private ushort _GreaseContinuousBadDrop;
        public ushort GreaseContinuousBadDrop
        {
            get { return _GreaseContinuousBadDrop; }
            set { if (value != _GreaseContinuousBadDrop) { _GreaseContinuousBadDrop = value; RaisePropertyChanged("GreaseContinuousBadDrop"); } }
        }
        private bool _GreaseContinuousBad;
        public bool GreaseContinuousBad
        {
            get { return _GreaseContinuousBad; }
            set { if (value != _GreaseContinuousBad) { _GreaseContinuousBad = value; RaisePropertyChanged("GreaseContinuousBad"); } }
        }

        private double _SumBrightnessDrop12;
        public double SumBrightnessDrop12
        {
            get { return _SumBrightnessDrop12; }
            set { if (value != _SumBrightnessDrop12) { _SumBrightnessDrop12 = value; RaisePropertyChanged("SumBrightnessDrop12"); } }
        }
        //第一二滴面亮度结果
        private ushort _SumBrightnessDrop12Judge;
        public ushort SumBrightnessDrop12Judge
        {
            get { return _SumBrightnessDrop12Judge; }
            set { if (value != _SumBrightnessDrop12Judge) { _SumBrightnessDrop12Judge = value; RaisePropertyChanged("SumBrightnessDrop12Judge"); } }
        }


        private double _Magnet1Brightness;
        public double Magnet1Brightness
        {
            get { return _Magnet1Brightness; }
            set { if (value != _Magnet1Brightness) { _Magnet1Brightness = value; RaisePropertyChanged("Magnet1Brightness"); } }
        }
        private double _Magnet2Brightness;
        public double Magnet2Brightness
        {
            get { return _Magnet2Brightness; }
            set { if (value != _Magnet2Brightness) { _Magnet2Brightness = value; RaisePropertyChanged("Magnet2Brightness"); } }
        }
        private double _Magnet3Brightness;
        public double Magnet3Brightness
        {
            get { return _Magnet3Brightness; }
            set { if (value != _Magnet3Brightness) { _Magnet3Brightness = value; RaisePropertyChanged("Magnet3Brightness"); } }
        }
        private double _Magnet4Brightness;
        public double Magnet4Brightness
        {
            get { return _Magnet4Brightness; }
            set { if (value != _Magnet4Brightness) { _Magnet4Brightness = value; RaisePropertyChanged("Magnet4Brightness"); } }
        }



        private bool _IsRotate;
        /// <summary>
        /// 正反面是否旋转图片用
        /// </summary>
        public bool IsRotate
        {
            get { return _IsRotate; }
            set { if (value != _IsRotate) { _IsRotate = value; RaisePropertyChanged("IsRotate"); } }
        }


        private ushort _Drop1LessPosition;
        /// <summary>
        /// 第一滴少量时，少量的位置给PLC
        /// </summary>
        public ushort Drop1LessPosition
        {
            get { return _Drop1LessPosition; }
            set { if (value != _Drop1LessPosition) { _Drop1LessPosition = value; RaisePropertyChanged("Drop1LessPosition"); } }
        }

        //private ushort _Drop2LessPosition;
        //public ushort Drop2LessPosition
        //{
        //    get { return _Drop2LessPosition; }
        //    set { if (value != _Drop2LessPosition) { _Drop2LessPosition = value; RaisePropertyChanged("Drop2LessPosition"); } }
        //}

        private ushort _Drop1Judge;
        /// <summary>
        /// 第一滴的判断，写给PLC用
        /// </summary>
        public ushort Drop1Judge
        {
            get { return _Drop1Judge; }
            set { if (value != _Drop1Judge) { _Drop1Judge = value; RaisePropertyChanged("Drop1Judge"); } }
        }
        private ushort _Drop2Judge;
        public ushort Drop2Judge
        {
            get { return _Drop2Judge; }
            set { if (value != _Drop2Judge) { _Drop2Judge = value; RaisePropertyChanged("Drop2Judge"); } }
        }
        private ushort _Drop3Judge;
        public ushort Drop3Judge
        {
            get { return _Drop3Judge; }
            set { if (value != _Drop3Judge) { _Drop3Judge = value; RaisePropertyChanged("Drop3Judge"); } }
        }



        private int _Drop3DatumPoint_W;
        /// <summary>
        /// 第三滴面定位点blob的大小
        /// </summary>
        public int Drop3DatumPoint_W
        {
            get { return _Drop3DatumPoint_W; }
            set { if (_Drop3DatumPoint_W != value) { _Drop3DatumPoint_W = value; RaisePropertyChanged("Drop3DatumPoint_W"); } }
        }
        private int _Drop3DatumPoint_H;

        public int Drop3DatumPoint_H
        {
            get { return _Drop3DatumPoint_H; }
            set { if (_Drop3DatumPoint_H != value) { _Drop3DatumPoint_H = value; RaisePropertyChanged("Drop3DatumPoint_H"); } }
        }
        private int _Drop3DatumPoint_A;

        public int Drop3DatumPoint_A
        {
            get { return _Drop3DatumPoint_A; }
            set { if (_Drop3DatumPoint_A != value) { _Drop3DatumPoint_A = value; RaisePropertyChanged("Drop3DatumPoint_A"); } }
        }
        private int _DatumPointDrop12_W;

        public int DatumPointDrop12_W
        {
            get { return _DatumPointDrop12_W; }
            set { if (_DatumPointDrop12_W != value) { _DatumPointDrop12_W = value; RaisePropertyChanged("DatumPointDrop12_W"); } }
        }
        private int _DatumPointDrop12_H;

        public int DatumPointDrop12_H
        {
            get { return _DatumPointDrop12_H; }
            set { if (_DatumPointDrop12_H != value) { _DatumPointDrop12_H = value; RaisePropertyChanged("DatumPointDrop12_H"); } }
        }
        private int _DatumPointDrop12_A;

        public int DatumPointDrop12_A
        {
            get { return _DatumPointDrop12_A; }
            set { if (_DatumPointDrop12_A != value) { _DatumPointDrop12_A = value; RaisePropertyChanged("DatumPointDrop12_A"); } }
        }

        private int _Drop1GreaseBlob_W;

        public int Drop1GreaseBlob_W
        {
            get { return _Drop1GreaseBlob_W; }
            set { if (_Drop1GreaseBlob_W != value) { _Drop1GreaseBlob_W = value; RaisePropertyChanged("Drop1GreaseBlob_W"); } }
        }
        private int _Drop1GreaseBlob_H;

        public int Drop1GreaseBlob_H
        {
            get { return _Drop1GreaseBlob_H; }
            set { if (_Drop1GreaseBlob_H != value) { _Drop1GreaseBlob_H = value; RaisePropertyChanged("Drop1GreaseBlob_H"); } }
        }
        private int _Drop1GreaseBlob_A;
        public int Drop1GreaseBlob_A
        {
            get { return _Drop1GreaseBlob_A; }
            set { if (_Drop1GreaseBlob_A != value) { _Drop1GreaseBlob_A = value; RaisePropertyChanged("Drop1GreaseBlob_A"); } }
        }

        private int _Drop2GreaseBlob_W;
        public int Drop2GreaseBlob_W
        {
            get { return _Drop2GreaseBlob_W; }
            set { if (_Drop2GreaseBlob_W != value) { _Drop2GreaseBlob_W = value; RaisePropertyChanged("Drop2GreaseBlob_W"); } }
        }
        private int _Drop2GreaseBlob_H;

        public int Drop2GreaseBlob_H
        {
            get { return _Drop2GreaseBlob_H; }
            set { if (_Drop2GreaseBlob_H != value) { _Drop2GreaseBlob_H = value; RaisePropertyChanged("Drop2GreaseBlob_H"); } }
        }
        private int _Drop2GreaseBlob_A;

        public int Drop2GreaseBlob_A
        {
            get { return _Drop2GreaseBlob_A; }
            set { if (_Drop2GreaseBlob_A != value) { _Drop2GreaseBlob_A = value; RaisePropertyChanged("Drop2GreaseBlob_A"); } }
        }
        /// <summary>
        /// 棱部分油脂信息用于记录
        /// </summary>
        private int _Drop2MinorBlob_W;
        public int Drop2MinorBlob_W
        {
            get { return _Drop2MinorBlob_W; }
            set { if (_Drop2MinorBlob_W != value) { _Drop2MinorBlob_W = value; RaisePropertyChanged("Drop2MinorBlob_W"); } }
        }
        private int _Drop2MinorBlob_H;
        public int Drop2MinorBlob_H
        {
            get { return _Drop2MinorBlob_H; }
            set { if (_Drop2MinorBlob_H != value) { _Drop2MinorBlob_H = value; RaisePropertyChanged("Drop2MinorBlob_H"); } }
        }
        private int _Drop2MinorBlob_A;
        public int Drop2MinorBlob_A
        {
            get { return _Drop2MinorBlob_A; }
            set { if (_Drop2MinorBlob_A != value) { _Drop2MinorBlob_A = value; RaisePropertyChanged("Drop2MinorBlob_A"); } }
        }

        private int _Drop3MinorBlob_W;
        public int Drop3MinorBlob_W
        {
            get { return _Drop3MinorBlob_W; }
            set { if (_Drop3MinorBlob_W != value) { _Drop3MinorBlob_W = value; RaisePropertyChanged("Drop3MinorBlob_W"); } }
        }
        private int _Drop3MinorBlob_H;
        public int Drop3MinorBlob_H
        {
            get { return _Drop3MinorBlob_H; }
            set { if (_Drop3MinorBlob_H != value) { _Drop3MinorBlob_H = value; RaisePropertyChanged("Drop3MinorBlob_H"); } }
        }
        private int _Drop3MinorBlob_A;
        public int Drop3MinorBlob_A
        {
            get { return _Drop3MinorBlob_A; }
            set { if (_Drop3MinorBlob_A != value) { _Drop3MinorBlob_A = value; RaisePropertyChanged("Drop3MinorBlob_A"); } }
        }



        private int _Drop3GreaseBlob_W;

        public int Drop3GreaseBlob_W
        {
            get { return _Drop3GreaseBlob_W; }
            set { if (_Drop3GreaseBlob_W != value) { _Drop3GreaseBlob_W = value; RaisePropertyChanged("Drop3GreaseBlob_W"); } }
        }
        private int _Drop3GreaseBlob_H;

        public int Drop3GreaseBlob_H
        {
            get { return _Drop3GreaseBlob_H; }
            set { if (_Drop3GreaseBlob_H != value) { _Drop3GreaseBlob_H = value; RaisePropertyChanged("Drop3GreaseBlob_H"); } }
        }
        private int _Drop3GreaseBlob_A;

        public int Drop3GreaseBlob_A
        {
            get { return _Drop3GreaseBlob_A; }
            set { if (_Drop3GreaseBlob_A != value) { _Drop3GreaseBlob_A = value; RaisePropertyChanged("Drop3GreaseBlob_A"); } }
        }


        private int _DatumPointDrop3_X;

        public int DatumPointDrop3_X
        {
            get { return _DatumPointDrop3_X; }
            set { if (_DatumPointDrop3_X != value) { _DatumPointDrop3_X = value; RaisePropertyChanged("DatumPointDrop3_X"); } }
        }
        private int _DatumPointDrop3_Y;
        public int DatumPointDrop3_Y
        {
            get { return _DatumPointDrop3_Y; }
            set { if (_DatumPointDrop3_Y != value) { _DatumPointDrop3_Y = value; RaisePropertyChanged("DatumPointDrop3_Y"); } }
        }
        private int _DatumPointDrop12_X;

        public int DatumPointDrop12_X
        {
            get { return _DatumPointDrop12_X; }
            set { if (_DatumPointDrop12_X != value) { _DatumPointDrop12_X = value; RaisePropertyChanged("DatumPointDrop12_X"); } }
        }
        private int _DatumPointDrop12_Y;
        public int DatumPointDrop12_Y
        {
            get { return _DatumPointDrop12_Y; }
            set { if (_DatumPointDrop12_Y != value) { _DatumPointDrop12_Y = value; RaisePropertyChanged("DatumPointDrop12_Y"); } }
        }

        private ObservableCollection<StatusType> _Run_status = new ObservableCollection<StatusType>();
        public ObservableCollection<StatusType> Run_status
        {
            get { return _Run_status; }
            set { if (_Run_status != value) { _Run_status = value; RaisePropertyChanged("Run_status"); } }
        }

        private JudgeType _PositiveJudge;
        public JudgeType PositiveJudge
        {
            get { return _PositiveJudge; }
            set { if (_PositiveJudge != value) { _PositiveJudge = value; RaisePropertyChanged("PositiveJudge"); } }
        }
        private JudgeType _Positive2Judge;

        public JudgeType Positive2Judge
        {
            get { return _Positive2Judge; }
            set { if (_Positive2Judge != value) { _Positive2Judge = value; RaisePropertyChanged("Positive2Judge"); } }
        }
        private JudgeType _NegativeJudge;

        public JudgeType NegativeJudge
        {
            get { return _NegativeJudge; }
            set { if (_NegativeJudge != value) { _NegativeJudge = value; RaisePropertyChanged("NegativeJudge"); } }
        }




        /// <summary>
        /// source image
        /// </summary>
        private BitmapSource _GreaseSourceImage;
        public BitmapSource GreaseSourceImage
        {
            get { return _GreaseSourceImage; }
            set
            {
                if (value != _GreaseSourceImage)
                {
                    _GreaseSourceImage = value;
                    _GreaseSourceImage.Freeze();
                    RaisePropertyChanged("GreaseSourceImage");
                }
            }
        }
        /// <summary>
        /// source image
        /// </summary>
        private BitmapSource _GreaseSourceImage2;
        public BitmapSource GreaseSourceImage2
        {
            get { return _GreaseSourceImage2; }
            set
            {
                if (value != _GreaseSourceImage2)
                {
                    _GreaseSourceImage2 = value;
                    _GreaseSourceImage2.Freeze();
                    RaisePropertyChanged("GreaseSourceImage2");
                }
            }
        }

        private BitmapSource _BinOriginalPositiveImage;
        public BitmapSource BinOriginalPositiveImage
        {
            get { return _BinOriginalPositiveImage; }
            set
            {
                if (value != _BinOriginalPositiveImage)
                {
                    _BinOriginalPositiveImage = value;
                    _BinOriginalPositiveImage.Freeze();
                    RaisePropertyChanged("BinOriginalPositiveImage");

                }
            }
        }
        private BitmapSource _BinOriginalPositiveDrop12Image;
        public BitmapSource BinOriginalPositiveDrop12Image
        {
            get { return _BinOriginalPositiveDrop12Image; }
            set
            {
                if (value != _BinOriginalPositiveDrop12Image)
                {
                    _BinOriginalPositiveDrop12Image = value;
                    _BinOriginalPositiveDrop12Image.Freeze();
                    RaisePropertyChanged("BinOriginalPositiveDrop12Image");

                }
            }
        }

        private BitmapSource _Drop2MinorCanvas;
        public BitmapSource Drop2MinorCanvas
        {
            get { return _Drop2MinorCanvas; }
            set
            {
                if (value != _Drop2MinorCanvas)
                {
                    _Drop2MinorCanvas = value;
                    _Drop2MinorCanvas.Freeze();
                    RaisePropertyChanged("Drop2MinorCanvas");
                }
            }
        }
        private BitmapSource _Drop2MinorBin;
        public BitmapSource Drop2MinorBin
        {
            get { return _Drop2MinorBin; }
            set
            {
                if (value != _Drop2MinorBin)
                {
                    _Drop2MinorBin = value;
                    _Drop2MinorBin.Freeze();
                    RaisePropertyChanged("Drop2MinorBin");
                }
            }
        }


        private BitmapSource _Drop3MinorCanvas;
        public BitmapSource Drop3MinorCanvas
        {
            get { return _Drop3MinorCanvas; }
            set
            {
                if (value != _Drop3MinorCanvas)
                {
                    _Drop3MinorCanvas = value;
                    _Drop3MinorCanvas.Freeze();
                    RaisePropertyChanged("Drop3MinorCanvas");
                }
            }
        }
        private BitmapSource _Drop3MinorBin;
        public BitmapSource Drop3MinorBin
        {
            get { return _Drop3MinorBin; }
            set
            {
                if (value != _Drop3MinorBin)
                {
                    _Drop3MinorBin = value;
                    _Drop3MinorBin.Freeze();
                    RaisePropertyChanged("Drop3MinorBin");
                }
            }
        }


        private BitmapSource _BinPosiiveImage;
        public BitmapSource BinPosiiveImage
        {
            get { return _BinPosiiveImage; }
            set
            {
                if (value != _BinPosiiveImage)
                {
                    _BinPosiiveImage = value;
                    _BinPosiiveImage.Freeze();
                    RaisePropertyChanged("BinPosiiveImage");
                }
            }
        }
        private BitmapSource _BinPosiive2Image;
        public BitmapSource BinPosiive2Image
        {
            get { return _BinPosiive2Image; }
            set
            {
                if (value != _BinPosiive2Image)
                {
                    _BinPosiive2Image = value;
                    _BinPosiive2Image.Freeze();
                    RaisePropertyChanged("BinPosiive2Image");
                }
            }
        }
        private BitmapSource _BinNegativeImage;
        public BitmapSource BinNegativeImage
        {
            get { return _BinNegativeImage; }
            set
            {
                if (value != _BinNegativeImage)
                {
                    _BinNegativeImage = value;
                    _BinNegativeImage.Freeze();
                    RaisePropertyChanged("BinNegativeImage");
                }
            }
        }
        private BitmapSource _RotateImage_Screw;
        public BitmapSource RotateImage_Screw
        {
            get { return _RotateImage_Screw; }
            set
            {
                if (value != _RotateImage_Screw)
                {
                    _RotateImage_Screw = value;
                    _RotateImage_Screw.Freeze();
                    RaisePropertyChanged("RotateImage_Screw");
                }
            }
        }

        private BitmapSource _RotateImageDrop12;
        public BitmapSource RotateImageDrop12
        {
            get { return _RotateImageDrop12; }
            set
            {
                if (value != _RotateImageDrop12)
                {
                    _RotateImageDrop12 = value;
                    _RotateImageDrop12.Freeze();
                    RaisePropertyChanged("RotateImageDrop12");
                }
            }
        }

        private BitmapSource _RotateImageDrop3;
        public BitmapSource RotateImageDrop3
        {
            get { return _RotateImageDrop3; }
            set
            {
                if (value != _RotateImageDrop3)
                {
                    _RotateImageDrop3 = value;
                    _RotateImageDrop3.Freeze();
                    RaisePropertyChanged("RotateImageDrop3");
                }
            }
        }

        private GreasePartParameters _GreasePara = new GreasePartParameters();
        public GreasePartParameters GreasePara
        {
            get { return _GreasePara; }
            set { if (_GreasePara != value) { _GreasePara = value; RaisePropertyChanged("GreasePara"); } }
        }




        #endregion

        #region Method

        #region 第三滴面的定位点检测
        /// <summary>
        /// 找定位位置
        /// </summary>
        private void DatumPointDrop3Capture(BitmapSource bitmap)
        {
            try
            {
                Run_status[6] = StatusType.running;

                DatumPointDrop3_X = 0;
                DatumPointDrop3_Y = 0;
                Drop3DatumPoint_W = 0;
                Drop3DatumPoint_H = 0;
                Drop3DatumPoint_A = 0;
                using (Mat image = bitmap.Clone().ToMat().SubMat(new Rect(GreasePara.DatumPointDrop3_Left, GreasePara.DatumPointDrop3_Top, GreasePara.DatumPointDrop3_Width, GreasePara.DatumPointDrop3_Height)))
                using (Mat bin = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(image, bin, 5);
                    Cv2.CvtColor(bin, bin, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(bin, bin, GreasePara.DatumPointBinValue, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, new Point(-1, -1), 2, BorderTypes.Constant, Scalar.Gold);

                    BinOriginalPositiveImage = bin.ToBitmapSource();//显示bin原图


                    ConnectedComponents.Blob max_blob = null;
                    MaxAreaBlob(bin, ref max_blob);


                    if (max_blob == null)
                    {
                        Run_status[6] = StatusType.error;

                        logger.Error("DatumPointDrop3Capture----max_blob==null ");

                        string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                        Create_dir(grease_error_images_path);
                        //没有框框的
                        Cv2.ImWrite(grease_error_images_path + "max_blob=NULL" + Drop3Fixture.ToString() + "_Fixture__" + Drop3OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());

                    }
                    else
                    {
                        //左+上
                        DatumPointDrop3_X = max_blob.Rect.Left + GreasePara.DatumPointDrop3_Left;
                        DatumPointDrop3_Y = max_blob.Rect.Top + GreasePara.DatumPointDrop3_Top;/////原下
                        Drop3DatumPoint_W = max_blob.Width;
                        Drop3DatumPoint_H = max_blob.Height;
                        Drop3DatumPoint_A = max_blob.Area;
                        if (GloPara.DebugMode)
                        {
                            logger.Info("DatumPointDrop3Capture: " + "W: " + max_blob.Width + "H: " + max_blob.Height + "A: " + max_blob.Area);
                            logger.Info("DatumPointDrop3Capture: " + "X: " + DatumPointDrop3_X + "Y: " + DatumPointDrop3_Y);
                        }


                    }
                }
                Run_status[6] = StatusType.end;


            }
            catch (Exception ex)
            {
                Run_status[6] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(grease_error_images_path);
                //没有框框的
                Cv2.ImWrite(grease_error_images_path + "DatumPointCaptureError" + Drop3Fixture.ToString() + "_Fixture__" + Drop3OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());
                logger.Error("DatumPointDrop3Capture|  " + ex.Message);
                DatumPointDrop3_X = 0;
                DatumPointDrop3_Y = 0;
                Drop3DatumPoint_W = 0;
                Drop3DatumPoint_H = 0;
                Drop3DatumPoint_A = 0;
            }
        }
        #endregion

        #region 第一二滴定位点检测
        /// <summary>
        /// 找定位位置,第一滴和第二滴用
        /// </summary>
        private void DatumPointDrop12Capture(BitmapSource bitmap)
        {
            try
            {
                Run_status[3] = StatusType.running;
                DatumPointDrop12_X = 0;
                DatumPointDrop12_Y = 0;
                DatumPointDrop12_W = 0;
                DatumPointDrop12_H = 0;
                DatumPointDrop12_A = 0;
                using (Mat image = bitmap.Clone().ToMat().SubMat(new Rect(GreasePara.DatumPointDrop12_Left, GreasePara.DatumPointDrop12_Top, GreasePara.DatumPointDrop12_Width, GreasePara.DatumPointDrop12_Height)))
                using (Mat bin = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    Cv2.MedianBlur(image, bin, 5);
                    Cv2.CvtColor(bin, bin, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(bin, bin, GreasePara.DatumPointDrop12BinValue, 255, ThresholdTypes.Binary);//
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);

                    BinOriginalPositiveDrop12Image = bin.ToBitmapSource();//显示bin原图

                    ConnectedComponents.Blob max_blob = null;
                    MaxAreaBlob(bin, ref max_blob);


                    if (max_blob == null)
                    {
                        Run_status[3] = StatusType.error;
                        logger.Error("DatumPointDrop3Capture----max_blob==null ");
                        //没有框框的
                        string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                        Create_dir(grease_error_images_path);
                        Cv2.ImWrite(grease_error_images_path + "max_blob=NULL" + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());
                    }
                    else
                    {
                        //右+下
                        DatumPointDrop12_X = max_blob.Rect.Right + GreasePara.DatumPointDrop12_Left;
                        DatumPointDrop12_Y = max_blob.Rect.Bottom + GreasePara.DatumPointDrop12_Top;
                        DatumPointDrop12_W = max_blob.Width;
                        DatumPointDrop12_H = max_blob.Height;
                        DatumPointDrop12_A = max_blob.Area;
                        if (GloPara.DebugMode)
                        {
                            logger.Debug("DatumPointDrop12Capture: " + "W: " + DatumPointDrop12_W + "H: " + DatumPointDrop12_H + "A: " + DatumPointDrop12_A);
                            logger.Debug("DatumPointDrop12Capture: " + "X: " + DatumPointDrop12_X + "Y: " + DatumPointDrop12_Y);
                        }

                    }
                }
                Run_status[3] = StatusType.end;

            }
            catch (Exception ex)
            {
                Run_status[3] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(grease_error_images_path);
                //没有框框的
                Cv2.ImWrite(grease_error_images_path + "DatumPointCaptureError" + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());
                logger.Error("DatumPointDrop12Capture|  " + ex.Message);
                DatumPointDrop12_X = 0;
                DatumPointDrop12_Y = 0;
                DatumPointDrop12_W = 0;
                DatumPointDrop12_H = 0;
                DatumPointDrop12_A = 0;
            }
        }
        #endregion

        #region 第一二滴测量
        /// <summary>
        /// 第一滴，和第二点，实际是反面 
        /// </summary>
        /// <param name="InputImage"></param>
        private bool Drop1GreaseMeasure(BitmapSource bitmap)
        {
            try
            {
                //初始化状态
                Run_status[4] = StatusType.running;
                Drop1GreaseBlob_W = 0;
                Drop1GreaseBlob_H = 0;
                Drop1GreaseBlob_A = 0;
                Drop2GreaseBlob_W = 0;
                Drop2GreaseBlob_H = 0;
                Drop2GreaseBlob_A = 0;
                Drop1Judge = 0;
                Drop2Judge = 0;

                Rect roi_Drop1 = new Rect(DatumPointDrop12_X + (int)GreasePara.Drop1Roi_L, DatumPointDrop12_Y + (int)GreasePara.Drop1Roi_T, (int)GreasePara.Drop1Roi_W, (int)GreasePara.Drop1Roi_H);//1
                Rect roi_Drop2 = new Rect(DatumPointDrop12_X + (int)GreasePara.Drop2Roi_L, DatumPointDrop12_Y + (int)GreasePara.Drop2Roi_T, (int)GreasePara.Drop2Roi_W, (int)GreasePara.Drop2Roi_H);//2
                List<ConnectedComponents.Blob> drop1GreaseBlobs = new List<ConnectedComponents.Blob>();
                using (Mat image = bitmap.ToMat().Clone())
                using (Mat drop1Image = new Mat(image, roi_Drop1))//边框问题
                using (Mat drop1ImageDisplay = drop1Image.Clone())//取消CLONE
                using (Mat drop2Image = new Mat(image, roi_Drop2))
                using (Mat bin = new Mat())
                //using (Mat filtered = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    string drop12_face_images_path = Drop12_face_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                    Create_dir(drop12_face_images_path);
                    Cv2.ImWrite(drop12_face_images_path + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", image);

                    string drop1_images_path;

                    Cv2.MedianBlur(drop1Image, bin, 5);
                    Cv2.CvtColor(bin, bin, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(bin, bin, GreasePara.Drop1GreaseBlobThresh, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    //Cv2.MorphologyEx(Bin, Bin, MorphTypes.Close, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    var rr = Cv2.ConnectedComponentsEx(bin);

                    Task t = new Task(() => Drop2GreaseMeasure(drop2Image, image));//创建了线程还未开启
                    t.Start();


                    foreach (var drop1Blob in rr.Blobs.Skip(1))
                    {
                        if (drop1Blob.Width >= GreasePara.Drop1GreaseBlobMinimum_W &&
                            drop1Blob.Height >= GreasePara.Drop1GreaseBlobMinimum_H &&
                            drop1Blob.Area >= GreasePara.Drop1GreaseBlobMinimum_A) //获取大于的roi
                        {
                            if (drop1Blob.Centroid.X >= GreasePara.Drop1JudgePosition_Left &&
                                drop1Blob.Centroid.Y >= GreasePara.Drop1JudgePosition_Top &&
                                drop1Blob.Centroid.X <= GreasePara.Drop1JudgePosition_Left + GreasePara.Drop1JudgePosition_Width &&
                                drop1Blob.Centroid.Y <= GreasePara.Drop1JudgePosition_Top + GreasePara.Drop1JudgePosition_Height)
                            {
                                drop1GreaseBlobs.Add(drop1Blob);
                            }
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("Drop1Blobs: " + "W:" + drop1GreaseBlobs[0].Width + "H: " + drop1GreaseBlobs[0].Height + "A: " + drop1GreaseBlobs[0].Area);

                            }
                        }
                    }

                    //给值
                    if (drop1GreaseBlobs.Count > 1)
                    {

                        for (int i = 0; i < drop1GreaseBlobs.Count; i++)
                        {

                            Drop1GreaseBlob_W += drop1GreaseBlobs[i].Width;
                            Drop1GreaseBlob_H = Math.Max(Drop1GreaseBlob_H, drop1GreaseBlobs[i].Height);
                            Drop1GreaseBlob_A += drop1GreaseBlobs[i].Area;
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("Drop1Blobs.Count>=1： " + drop1GreaseBlobs.Count);
                                //注意XY，后续可能要改
                                logger.Debug("Drop1Blobs: " + i + "W:" + drop1GreaseBlobs[i].Width + " H: " + drop1GreaseBlobs[i].Height + " A: " + drop1GreaseBlobs[i].Area);
                                logger.Debug("Drop1Blobs: " + i + "X:" + drop1GreaseBlobs[i].Centroid.X + " Y: " + drop1GreaseBlobs[i].Centroid.Y);
                            }
                        }
                    }

                    else if (drop1GreaseBlobs.Count == 1)
                    {
                        Drop1GreaseBlob_W = drop1GreaseBlobs[0].Width;
                        Drop1GreaseBlob_H = drop1GreaseBlobs[0].Height;
                        Drop1GreaseBlob_A = drop1GreaseBlobs[0].Area;
                    }
                    else
                    {
                        Drop1GreaseBlob_W = 0;
                        Drop1GreaseBlob_H = 0;
                        Drop1GreaseBlob_A = 0;
                    }
                    Point drop1PointTL = new Point(DatumPointDrop12_X + GreasePara.Drop1Roi_L - 10, DatumPointDrop12_Y + GreasePara.Drop1Roi_T - 10);
                    Point drop1PointBR = new Point(DatumPointDrop12_X + GreasePara.Drop1Roi_L + GreasePara.Drop1Roi_W + 10, DatumPointDrop12_Y + GreasePara.Drop1Roi_T + GreasePara.Drop1Roi_H + 10);
                    //判断
                    if (Drop1GreaseBlob_W <= GreasePara.Drop1GreaseBlobUpper_W &&
                        Drop1GreaseBlob_H <= GreasePara.Drop1GreaseBlobUpper_H &&
                        Drop1GreaseBlob_A <= GreasePara.Drop1GreaseBlobUpper_A &&
                        Drop1GreaseBlob_W >= GreasePara.Drop1GreaseBlobLower_W &&
                        Drop1GreaseBlob_H >= GreasePara.Drop1GreaseBlobLower_H &&
                        Drop1GreaseBlob_A >= GreasePara.Drop1GreaseBlobLower_A)

                    {
                        PositiveJudge = JudgeType.OK;
                        Drop1Judge = Drop1_OK;

                        Cv2.Rectangle(image, drop1PointTL, drop1PointBR, Scalar.GreenYellow, 10, LineTypes.AntiAlias, 0);//1
                        drop1_images_path = Drop1_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";

                    }

                    else if (Drop1GreaseBlob_W >= GreasePara.Drop1GreaseBlobUpper_W ||
                             Drop1GreaseBlob_H >= GreasePara.Drop1GreaseBlobUpper_H ||
                             Drop1GreaseBlob_A >= GreasePara.Drop1GreaseBlobUpper_A)
                    {
                        PositiveJudge = JudgeType.NG2;//NG2表多量
                        Drop1Judge = Drop1_TooMuch;
                        Cv2.Rectangle(image, drop1PointTL, drop1PointBR, Scalar.DeepPink, 10, LineTypes.AntiAlias, 0);//1
                        drop1_images_path = Drop1_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG2\\";

                    }
                    else
                    {
                        ushort temposition = 0;
                        Drop1LessPosition = 0;
                        temposition = repairGrease(GreasePara.Drop1VerticalLine, GreasePara.Drop1VerticalLine + GreasePara.Drop1GreaseBlobLower_W, drop1GreaseBlobs);
                        Drop1LessPosition = (ushort)(temposition << 4);
                        PositiveJudge = JudgeType.NG;//少就NG
                        Cv2.Rectangle(image, drop1PointTL, drop1PointBR, Scalar.Red, 10, LineTypes.AntiAlias, 0);//1
                        Drop1Judge = Drop1_TooLittle;
                        drop1_images_path = Drop1_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";
                    }


                    //画的十字架，定位点位置
                    Cv2.Line(image, DatumPointDrop12_X - 200, DatumPointDrop12_Y - 0, DatumPointDrop12_X + 200, DatumPointDrop12_Y + 0, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    Cv2.Line(image, DatumPointDrop12_X - 0, DatumPointDrop12_Y - 200, DatumPointDrop12_X + 0, DatumPointDrop12_Y + 200, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    //画定位点范围
                    Cv2.Rectangle(image, new Rect(GreasePara.DatumPointDrop12_Left, GreasePara.DatumPointDrop12_Top, GreasePara.DatumPointDrop12_Width, GreasePara.DatumPointDrop12_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//1

                    //过滤掉target以外的blob
                    //rr.FilterByBlobs(bin, filtered, drop1GreaseBlobs);
                    if (GloPara.DebugMode)
                    {
                        logger.Debug("Drop1Blobs.Count： " + drop1GreaseBlobs.Count);


                        logger.Debug("Drop1Target: " + "W:" + Drop1GreaseBlob_W + " H:" + Drop1GreaseBlob_H + " A:" + Drop1GreaseBlob_A);
                        logger.Debug("DatumPointDrop12: " + "X:" + DatumPointDrop12_X + " Y: " + DatumPointDrop12_Y);
                        //亮度的Roi
                        Rect roi = new Rect(GreasePara.SumBrightnessDrop12Roi_Left + DatumPointDrop12_X, GreasePara.SumBrightnessDrop12Roi_Top + DatumPointDrop12_Y, GreasePara.SumBrightnessDrop12Roi_Width, GreasePara.SumBrightnessDrop12Roi_Height);
                        //画亮度识别区域
                        Cv2.Rectangle(image, roi, Scalar.GreenYellow, 20, LineTypes.AntiAlias, 0);//3
                        //画blob识别区域
                        Cv2.Rectangle(drop1ImageDisplay, new Rect(GreasePara.Drop1JudgePosition_Left, GreasePara.Drop1JudgePosition_Top, GreasePara.Drop1JudgePosition_Width, GreasePara.Drop1JudgePosition_Height), Scalar.Blue, 1, LineTypes.AntiAlias, 0);//1
                        BinPosiiveImage = bin.ToBitmapSource();//第一滴的open图

                    }
                    ImageObjects[0].DisplayImage = image.ToBitmapSource();//第一二滴的大图画布
                    //画竖线
                    Cv2.Line(drop1ImageDisplay, new Point(GreasePara.Drop1VerticalLine, 0), new Point(GreasePara.Drop1VerticalLine, drop1ImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);//第一滴红竖线
                    Cv2.Line(drop1ImageDisplay, new Point(GreasePara.Drop1VerticalLine + GreasePara.Drop1GreaseBlobLower_W, 0), new Point(GreasePara.Drop1VerticalLine + GreasePara.Drop1GreaseBlobLower_W, drop1ImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);

                    for (int i = 0; i < drop1GreaseBlobs.Count; i++)
                    {
                        Cv2.Rectangle(drop1ImageDisplay, drop1GreaseBlobs[i].Rect, Scalar.Red, 1, LineTypes.AntiAlias, 0);//1
                    }

                    //二值图和原图融合，
                    //Cv2.CvtColor(bin, bin, ColorConversionCodes.GRAY2BGR555);
                    //Cv2.AddWeighted(drop1ImageDisplay, 0.8, bin, 0.9, 0.0, drop1ImageDisplay);

                    Cv2.PutText(drop1ImageDisplay, "L: " + GreasePara.Drop1GreaseBlobLower_W + " < " + Drop1GreaseBlob_W + " < " + GreasePara.Drop1GreaseBlobUpper_W, new Point(5, 15), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 0);
                    Cv2.PutText(drop1ImageDisplay, "W: " + GreasePara.Drop1GreaseBlobLower_H + " < " + Drop1GreaseBlob_H + " < " + GreasePara.Drop1GreaseBlobUpper_H, new Point(5, 30), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 0);
                    Cv2.PutText(drop1ImageDisplay, "A: " + GreasePara.Drop1GreaseBlobLower_A + " < " + Drop1GreaseBlob_A + " < " + GreasePara.Drop1GreaseBlobUpper_A, new Point(5, 45), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 0);
                    Create_dir(drop1_images_path);
                    Cv2.ImWrite(drop1_images_path + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", drop1ImageDisplay);


                    ImageObjects[2].DisplayImage = drop1ImageDisplay.ToBitmapSource();//第一滴的小图画布

                    t.Wait();

                }
                Run_status[4] = StatusType.end;

                return true;
            }
            catch (Exception ex)
            {
                Run_status[4] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Drop1GreaseMeasure|  " + ex.Message);
                PositiveJudge = JudgeType.ERROR;
                string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(grease_error_images_path);
                Cv2.ImWrite(grease_error_images_path + "PositiveGreaseError" + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());

                ImageObjects[0].DisplayImage = bitmap;
                Drop1GreaseBlob_W = -1;
                Drop1GreaseBlob_H = -1;
                Drop1GreaseBlob_A = -1;
                Drop1Judge = Drop1_Abnromal;
                Drop2Judge = Drop2_Abnromal;
                return false;

            }
        }
        /// <summary>
        ///第二滴
        /// </summary>
        /// <param name="InputImage"小图></param>
        /// <param name="mat"原图画布></param>
        private void Drop2GreaseMeasure(Mat inputMat, Mat mat)
        {
            try
            {
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                using (Mat inputImageDisplay = inputMat.Clone())
                using (Mat bin = new Mat())
                {
                    string drop2_images_path;

                    Point drop2PointTL = new Point(DatumPointDrop12_X + GreasePara.Drop2Roi_L - 10, DatumPointDrop12_Y + GreasePara.Drop2Roi_T - 10);
                    Point drop2PointBR = new Point(DatumPointDrop12_X + GreasePara.Drop2Roi_L + GreasePara.Drop2Roi_W + 10, DatumPointDrop12_Y + GreasePara.Drop2Roi_T + GreasePara.Drop2Roi_H + 10);
                    List<ConnectedComponents.Blob> drop2GreaseBlobs = new List<ConnectedComponents.Blob>();
                    Cv2.MedianBlur(inputMat, bin, 5);
                    Cv2.CvtColor(bin, bin, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(bin, bin, GreasePara.Drop2GreaseBlobThresh, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    var rr2 = Cv2.ConnectedComponentsEx(bin);
                    foreach (var drop2blob in rr2.Blobs.Skip(1))
                    {
                        if (drop2blob.Width >= GreasePara.Drop2GreaseBlobMinimum_W &&
                            drop2blob.Height >= GreasePara.Drop2GreaseBlobMinimum_H &&
                            drop2blob.Area >= GreasePara.Drop2GreaseBlobMinimum_A) //获取大于的roi
                        {
                            if (drop2blob.Centroid.X >= GreasePara.Drop2JudgePosition_Left &&
                                drop2blob.Centroid.Y >= GreasePara.Drop2JudgePosition_Top &&
                                drop2blob.Centroid.X <= GreasePara.Drop2JudgePosition_Left + GreasePara.Drop2JudgePosition_Width &&
                                drop2blob.Centroid.Y <= GreasePara.Drop2JudgePosition_Top + GreasePara.Drop2JudgePosition_Height)
                            {
                                drop2GreaseBlobs.Add(drop2blob);
                            }
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("Drop2Blobs: " + "W:" + drop2GreaseBlobs[0].Width + "H: " + drop2GreaseBlobs[0].Height + "A: " + drop2GreaseBlobs[0].Area);

                            }

                        }
                    }
                    if (drop2GreaseBlobs.Count > 1)
                    {
                        for (int i = 0; i < drop2GreaseBlobs.Count; i++)
                        {
                            //if (PositiveGrease2Blobs[i].Centroid.Y >= GreasePara.Drop2YLowerLimit)//防止加入马达
                            //{
                            Drop2GreaseBlob_W += drop2GreaseBlobs[i].Width;
                            Drop2GreaseBlob_H = Math.Max(Drop2GreaseBlob_H, drop2GreaseBlobs[i].Height);
                            Drop2GreaseBlob_A += drop2GreaseBlobs[i].Area;
                            //}
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("Drop2Blobs: " + i + " W:" + drop2GreaseBlobs[i].Width + " H:" + drop2GreaseBlobs[i].Height + " A:" + drop2GreaseBlobs[i].Area);
                                logger.Debug("Drop2Blobs: " + i + " X:" + drop2GreaseBlobs[i].Centroid.X + " Y:" + drop2GreaseBlobs[i].Centroid.Y);
                                logger.Debug("Drop2Blobs.Count>=1： " + drop2GreaseBlobs.Count);

                            }

                            //注意XY，后续可能要改


                        }
                    }
                    else if (drop2GreaseBlobs.Count == 1)
                    {
                        Drop2GreaseBlob_W = drop2GreaseBlobs[0].Width;
                        Drop2GreaseBlob_H = drop2GreaseBlobs[0].Height;
                        Drop2GreaseBlob_A = drop2GreaseBlobs[0].Area;
                    }
                    else
                    {
                        Drop2GreaseBlob_W = 0;
                        Drop2GreaseBlob_H = 0;
                        Drop2GreaseBlob_A = 0;
                    }
                    //判断
                    if (Drop2GreaseBlob_W <= GreasePara.Drop2GreaseBlobUpper_W &&
                        Drop2GreaseBlob_H <= GreasePara.Drop2GreaseBlobUpper_H &&
                        Drop2GreaseBlob_A <= GreasePara.Drop2GreaseBlobUpper_A &&
                        Drop2GreaseBlob_W >= GreasePara.Drop2GreaseBlobLower_W &&
                        Drop2GreaseBlob_H >= GreasePara.Drop2GreaseBlobLower_H &&
                        Drop2GreaseBlob_A >= GreasePara.Drop2GreaseBlobLower_A)
                    {
                        int judge;
                        //第二滴:TH=127；lin1=-60；line2=15(差值15，+15向下);Roi（0，46），Roi_W=Image.w(435)，Roi_W=Image.h-46即82.//图片大小435*128
                        //>w；h;a; W对应w,H,A-1900?1500?.
                        //Monitor.Enter(this);//阻止第二滴和第三滴同步运行
                        using (Mat canvas = inputMat.Clone())
                        {
                            Mat minibin = new Mat();
                            VerticalEdgeSearch(inputMat, bin, GreasePara.Drop2VerTh, GreasePara.Drop2MinorRoi_T, GreasePara.Drop2MinorRoi_W, canvas, out minibin, GreasePara.Drop2MiniLimitWHA, GreasePara.Drpo2VerticalEdgeSub_w, false, out judge, out int blob_w, out int blob_h, out int blob_a);
                            if (GloPara.DebugMode)
                            {
                                Drop2MinorCanvas = canvas.ToBitmapSource();
                                Drop2MinorBin = minibin.ToBitmapSource();
                            }
                            minibin.Dispose();
                            //写棱上的信息到图片
                            Cv2.PutText(inputImageDisplay, "mL: " + GreasePara.Drop2MiniLimitWHA[0] + " < " + blob_w, new Point(5, 60), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Cv2.PutText(inputImageDisplay, "mW: " + GreasePara.Drop2MiniLimitWHA[1] + " < " + blob_h, new Point(5, 75), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Cv2.PutText(inputImageDisplay, "mA: " + GreasePara.Drop2MiniLimitWHA[2] + " < " + blob_a, new Point(5, 90), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Drop2MinorBlob_W = blob_w;
                            Drop2MinorBlob_H = blob_h;
                            Drop2MinorBlob_A = blob_a;

                        }
                        //Monitor.Exit(this);

                        if (judge == 1)
                        {
                            Positive2Judge = JudgeType.OK;
                            Drop2Judge = Drop2_OK;
                            Cv2.Rectangle(mat, drop2PointTL, drop2PointBR, Scalar.GreenYellow, 10, LineTypes.AntiAlias, 0);//2
                            drop2_images_path = Drop2_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";


                        }
                        else if (judge == 0)
                        {
                            //ushort temposition2 = 0;
                            //Drop2LessPosition = 0;
                            //temposition2 = repairGrease(GreasePara.Drop2VerticalLine, GreasePara.Drop2VerticalLine + GreasePara.Drop2GreaseBlobLower_W, drop2GreaseBlobs);
                            //Drop2LessPosition = (ushort)(temposition2 << 8);
                            Positive2Judge = JudgeType.NG;//少就NG
                            Drop2Judge = Drop2_TooLittle;
                            Cv2.Rectangle(mat, drop2PointTL, drop2PointBR, Scalar.Red, 10, LineTypes.AntiAlias, 0);//2
                            drop2_images_path = Drop2_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";

                        }

                        else
                        {
                            Positive2Judge = JudgeType.NG2;
                            Drop2Judge = Drop2_TooMuch;
                            Cv2.Rectangle(mat, drop2PointTL, drop2PointBR, Scalar.DeepPink, 10, LineTypes.AntiAlias, 0);//2
                            drop2_images_path = Drop2_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG2\\";
                        }

                    }
                    //多了
                    else if (Drop2GreaseBlob_W >= GreasePara.Drop2GreaseBlobUpper_W ||
                             Drop2GreaseBlob_H >= GreasePara.Drop2GreaseBlobUpper_H ||
                             Drop2GreaseBlob_A >= GreasePara.Drop2GreaseBlobUpper_A)
                    {
                        Positive2Judge = JudgeType.NG2;
                        Drop2Judge = Drop2_TooMuch;
                        Cv2.Rectangle(mat, drop2PointTL, drop2PointBR, Scalar.DeepPink, 10, LineTypes.AntiAlias, 0);//2
                        drop2_images_path = Drop2_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG2\\";
                    }
                    //少了
                    else
                    {

                        Positive2Judge = JudgeType.NG;//少就NG
                        Drop2Judge = Drop2_TooLittle;
                        Cv2.Rectangle(mat, drop2PointTL, drop2PointBR, Scalar.Red, 10, LineTypes.AntiAlias, 0);//2
                        drop2_images_path = Drop2_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";
                    }

                    if (GloPara.DebugMode)
                    {
                        logger.Debug("Drop2Blobs.Count： " + drop2GreaseBlobs.Count);
                        logger.Debug("Drop2Target: " + "W:" + Drop2GreaseBlob_W + " H:" + Drop2GreaseBlob_H + " A:" + Drop2GreaseBlob_A);
                        logger.Debug("Drop2Target: " + "X:" + DatumPointDrop3_X + " Y:" + DatumPointDrop3_Y);
                        //画原图识别区域
                        Cv2.Rectangle(inputImageDisplay, new Rect(GreasePara.Drop2JudgePosition_Left, GreasePara.Drop2JudgePosition_Top, GreasePara.Drop2JudgePosition_Width, GreasePara.Drop2JudgePosition_Height), Scalar.Blue, 1, LineTypes.AntiAlias, 0);//第二滴判定位置
                        BinPosiive2Image = bin.ToBitmapSource();//第二滴open图
                                                                // rr2.RenderBlobs(InputImageDisplay);//背景就是黑色，blob随机颜色

                    }
                    //画竖线//红色竖线
                    Cv2.Line(inputImageDisplay, new Point(GreasePara.Drop2VerticalLine, 0), new Point(GreasePara.Drop2VerticalLine, inputImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);
                    Cv2.Line(inputImageDisplay, new Point(GreasePara.Drop2VerticalLine + GreasePara.Drop2GreaseBlobLower_W, 0), new Point(GreasePara.Drop2VerticalLine + GreasePara.Drop2GreaseBlobLower_W, inputImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);
                    //画选中的blob框
                    for (int i = 0; i < drop2GreaseBlobs.Count; i++)
                    {

                        Cv2.Rectangle(inputImageDisplay, drop2GreaseBlobs[i].Rect, Scalar.Red, 1, LineTypes.AntiAlias, 0);//1

                    }
                    //画整体测量出油脂的信息
                    Cv2.PutText(inputImageDisplay, "L: " + GreasePara.Drop2GreaseBlobLower_W + " < " + Drop2GreaseBlob_W + " < " + GreasePara.Drop2GreaseBlobUpper_W, new Point(5, 15), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightSkyBlue, 0);
                    Cv2.PutText(inputImageDisplay, "W: " + GreasePara.Drop2GreaseBlobLower_H + " < " + Drop2GreaseBlob_H + " < " + GreasePara.Drop2GreaseBlobUpper_H, new Point(5, 30), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightSkyBlue, 0);
                    Cv2.PutText(inputImageDisplay, "A: " + GreasePara.Drop2GreaseBlobLower_A + " < " + Drop2GreaseBlob_A + " < " + GreasePara.Drop2GreaseBlobUpper_A, new Point(5, 45), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightSkyBlue, 0);
                    Create_dir(drop2_images_path);
                    Cv2.ImWrite(drop2_images_path + Drop12Fixture.ToString() + "_Fixture__" + Drop12OPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", inputImageDisplay);

                    ImageObjects[3].DisplayImage = inputImageDisplay.ToBitmapSource();//第二滴图画布
                }
            }
            catch (Exception ex)
            {
                Run_status[4] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Drop2GreaseMeasure|  " + ex.Message);
                Positive2Judge = JudgeType.ERROR;
                Drop2GreaseBlob_W = -1;
                Drop2GreaseBlob_H = -1;
                Drop2GreaseBlob_A = -1;
                Drop2Judge = Drop2_Abnromal;
            }
        }
        #endregion

        #region 第三滴测量
        /// <summary>
        /// 第三滴，实际称为正面
        /// </summary>
        /// <param name="InputImage"></param>
        private void Drop3GreaseMeasure(BitmapSource bitmap)
        {
            try
            {
                Run_status[7] = StatusType.running;
                Drop3Judge = 0;
                Drop3GreaseBlob_W = 0;
                Drop3GreaseBlob_H = 0;
                Drop3GreaseBlob_A = 0;//确保参数传递。
                Rect roi_Drop3 = new Rect(DatumPointDrop3_X + (int)GreasePara.Drop3Roi_L, DatumPointDrop3_Y + (int)GreasePara.Drop3Roi_T, (int)GreasePara.Drop3Roi_W, (int)GreasePara.Drop3Roi_H);//3
                List<ConnectedComponents.Blob> drop3GreaseBlobs = new List<ConnectedComponents.Blob>();
                using (Mat image = bitmap.Clone().ToMat())
                using (Mat drop3Image = new Mat(image, roi_Drop3))
                using (Mat drop3ImageDisplay = drop3Image.Clone())
                using (Mat bin = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(8, 8), new Point(-1, -1)))
                {
                    string drop3_face_images_dir_path = Drop3_face_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                    Create_dir(drop3_face_images_dir_path);
                    Cv2.ImWrite(drop3_face_images_dir_path + Drop3Fixture.ToString() + "_Fixture__" + Drop3OPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", image);
                    string drop3_images_path;

                    Point drop3PointTL = new Point(DatumPointDrop3_X + (int)GreasePara.Drop3Roi_L - 10, DatumPointDrop3_Y + (int)GreasePara.Drop3Roi_T - 10);
                    Point drop3PointBR = new Point(DatumPointDrop3_X + (int)GreasePara.Drop3Roi_L + (int)GreasePara.Drop3Roi_W + 10, DatumPointDrop3_Y + (int)GreasePara.Drop3Roi_T + (int)GreasePara.Drop3Roi_H + 10);
                    Cv2.CvtColor(drop3Image, bin, ColorConversionCodes.RGB2GRAY);
                    Cv2.Threshold(bin, bin, GreasePara.Drop3GreaseBlobThresh, 255, ThresholdTypes.Binary);
                    Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, new Point(-1, -1), 1, BorderTypes.Constant, Scalar.Gold);
                    var rr3 = Cv2.ConnectedComponentsEx(bin);
                    foreach (var drop3blob in rr3.Blobs.Skip(1))
                    {
                        if (Math.Abs(drop3blob.Width) >= GreasePara.Drop3GreaseBlobMinimum_W &&
                            Math.Abs(drop3blob.Height) >= GreasePara.Drop3GreaseBlobMinimum_H &&
                            Math.Abs(drop3blob.Area) >= GreasePara.Drop3GreaseBlobMinimum_A) //方定位
                        {
                            if (drop3blob.Centroid.X >= GreasePara.Drop3JudgePosition_Left &&
                                drop3blob.Centroid.Y >= GreasePara.Drop3JudgePosition_Top &&
                                drop3blob.Centroid.X <= GreasePara.Drop3JudgePosition_Left + GreasePara.Drop3JudgePosition_Width &&
                                drop3blob.Centroid.Y <= GreasePara.Drop3JudgePosition_Top + GreasePara.Drop3JudgePosition_Height)
                            {
                                drop3GreaseBlobs.Add(drop3blob);
                            }
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("NegativeGreaseBlobs: " + "W:" + drop3GreaseBlobs[0].Width + "H: " + drop3GreaseBlobs[0].Height + "A: " + drop3GreaseBlobs[0].Area);

                            }
                        }
                    }
                    if (drop3GreaseBlobs.Count > 1)
                    {
                        for (int i = 0; i < drop3GreaseBlobs.Count; i++)
                        {

                            Drop3GreaseBlob_W += drop3GreaseBlobs[i].Width;
                            Drop3GreaseBlob_H = Math.Max(Drop3GreaseBlob_H, drop3GreaseBlobs[i].Height);
                            Drop3GreaseBlob_A += drop3GreaseBlobs[i].Area;
                            //注意XY，后续可能要改
                            if (GloPara.DebugMode)
                            {
                                logger.Debug("Drop3GreaseBlobs: " + i + "W:" + drop3GreaseBlobs[i].Width + "H: " + drop3GreaseBlobs[i].Height + "A: " + drop3GreaseBlobs[i].Area);
                                logger.Debug("Drop3GreaseBlobs: " + i + "X:" + drop3GreaseBlobs[i].Centroid.X + "Y: " + drop3GreaseBlobs[i].Centroid.Y);
                                logger.Debug("Drop3GreaseBlobs.Count>=1： " + drop3GreaseBlobs.Count);

                            }
                        }
                    }
                    else if (drop3GreaseBlobs.Count == 1)
                    {
                        Drop3GreaseBlob_W = drop3GreaseBlobs[0].Width;
                        Drop3GreaseBlob_H = drop3GreaseBlobs[0].Height;
                        Drop3GreaseBlob_A = drop3GreaseBlobs[0].Area;
                        //正常
                    }
                    else
                    {
                        Drop3GreaseBlob_W = 0;
                        Drop3GreaseBlob_H = 0;
                        Drop3GreaseBlob_A = 0;
                    }

                    //正常
                    if (Drop3GreaseBlob_W <= GreasePara.Drop3GreaseBlobUpper_W &&
                        Drop3GreaseBlob_H <= GreasePara.Drop3GreaseBlobUpper_H &&
                        Drop3GreaseBlob_A <= GreasePara.Drop3GreaseBlobUpper_A &&
                        Drop3GreaseBlob_W >= GreasePara.Drop3GreaseBlobLower_W &&
                        Drop3GreaseBlob_H >= GreasePara.Drop3GreaseBlobLower_H &&
                        Drop3GreaseBlob_A >= GreasePara.Drop3GreaseBlobLower_A)
                    {
                        //油脂正常再检查棱位置

                        int judge;
                        //
                        //Monitor.Enter(this);//用于线程同步，由于资源不共用，还是算了 
                        using (Mat canvas = drop3Image.Clone())
                        {

                            Mat minibin = new Mat();

                            VerticalEdgeSearch(drop3Image, bin, GreasePara.Drop3VerTh, GreasePara.Drop3MinorRoi_T, GreasePara.Drop3MinorRoi_W, canvas, out minibin, GreasePara.Drop3MiniLimitWHA, GreasePara.Drpo3VerticalEdgeSub_w, true, out judge, out int blob_w, out int blob_h, out int blob_a);
                            if (GloPara.DebugMode)
                            {
                                Drop3MinorCanvas = canvas.ToBitmapSource();
                                Drop3MinorBin = minibin.ToBitmapSource();
                            }
                            minibin.Dispose();
                            //写棱上的信息到图片
                            Cv2.PutText(drop3ImageDisplay, "mL: " + GreasePara.Drop3MiniLimitWHA[0] + " < " + blob_w, new Point(5, 60), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Cv2.PutText(drop3ImageDisplay, "mW: " + GreasePara.Drop3MiniLimitWHA[1] + " < " + blob_h, new Point(5, 75), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Cv2.PutText(drop3ImageDisplay, "mA: " + GreasePara.Drop3MiniLimitWHA[2] + " < " + blob_a, new Point(5, 90), HersheyFonts.HersheyDuplex, 0.5, Scalar.LightCoral, 1);
                            Drop3MinorBlob_W = blob_w;
                            Drop3MinorBlob_H = blob_h;
                            Drop3MinorBlob_A = blob_a;
                        }
                        //Monitor.Exit(this);
                        if (judge == 1)
                        {
                            NegativeJudge = JudgeType.OK;
                            Drop3Judge = Drop3_OK;
                            Cv2.Rectangle(image, drop3PointTL, drop3PointBR, Scalar.GreenYellow, 10, LineTypes.AntiAlias, 0);//3
                            drop3_images_path = Drop3_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\OK\\";


                        }
                        else if (judge == 0)
                        {
                            NegativeJudge = JudgeType.NG;
                            Drop3Judge = Drop3_TooLittle;
                            Cv2.Rectangle(image, drop3PointTL, drop3PointBR, Scalar.Red, 10, LineTypes.AntiAlias, 0);//3
                            drop3_images_path = Drop3_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";

                        }
                        else
                        {
                            NegativeJudge = JudgeType.NG2;
                            Drop3Judge = Drop3_TooMuch;
                            Cv2.Rectangle(image, drop3PointTL, drop3PointBR, Scalar.DeepPink, 10, LineTypes.AntiAlias, 0);//3
                            drop3_images_path = Drop3_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG2\\";
                        }

                    }
                    //多了
                    else if (Drop3GreaseBlob_W >= GreasePara.Drop3GreaseBlobUpper_W ||
                             Drop3GreaseBlob_H >= GreasePara.Drop3GreaseBlobUpper_H ||
                             Drop3GreaseBlob_A >= GreasePara.Drop3GreaseBlobUpper_A)
                    {
                        NegativeJudge = JudgeType.NG2;
                        Drop3Judge = Drop3_TooMuch;
                        Cv2.Rectangle(image, drop3PointTL, drop3PointBR, Scalar.DeepPink, 10, LineTypes.AntiAlias, 0);//3
                        drop3_images_path = Drop3_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG2\\";

                    }
                    //少了
                    else
                    {
                        NegativeJudge = JudgeType.NG;
                        Drop3Judge = Drop3_TooLittle;
                        Cv2.Rectangle(image, drop3PointTL, drop3PointBR, Scalar.Red, 10, LineTypes.AntiAlias, 0);//3
                        drop3_images_path = Drop3_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\NG\\";
                    }
                    //画原图十字定位点
                    Cv2.Line(image, DatumPointDrop3_X - 200, DatumPointDrop3_Y - 0, DatumPointDrop3_X + 200, DatumPointDrop3_Y + 0, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    Cv2.Line(image, DatumPointDrop3_X - 0, DatumPointDrop3_Y - 200, DatumPointDrop3_X + 0, DatumPointDrop3_Y + 200, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                    //画原图定位区域  //画定位点范围
                    Cv2.Rectangle(image, new Rect(GreasePara.DatumPointDrop3_Left, GreasePara.DatumPointDrop3_Top, GreasePara.DatumPointDrop3_Width, GreasePara.DatumPointDrop3_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//3
                                                                                                                                                                                                                                  //亮度的Roi

                    if (GloPara.DebugMode)
                    {
                        logger.Debug("Drop3Blobs.Count： " + drop3GreaseBlobs.Count);

                        logger.Info("Drop3Target: " + "W:" + Drop3GreaseBlob_W + " H:" + Drop3GreaseBlob_H + " A:" + Drop3GreaseBlob_A);
                        logger.Info("Drop3Target: " + "X:" + DatumPointDrop3_X + " Y: " + DatumPointDrop3_Y);
                        // rr3.RenderBlobs(NegativeImageDisplay);//背景就是黑色，blob随机颜色

                        //画小图识别blob区域
                        Cv2.Rectangle(drop3ImageDisplay, new Rect(GreasePara.Drop3JudgePosition_Left, GreasePara.Drop3JudgePosition_Top, GreasePara.Drop3JudgePosition_Width, GreasePara.Drop3JudgePosition_Height), Scalar.Blue, 1, LineTypes.AntiAlias, 0);
                        //画第三地判断范围
                        //画磁石位置
                        Rect magnetroi = new Rect(GreasePara.Magnet1BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet1BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet1BrightnessRoi_Width, GreasePara.Magnet1BrightnessRoi_Height);
                        Cv2.Rectangle(image, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet1", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        //第二个
                        magnetroi = new Rect(GreasePara.Magnet2BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet2BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet2BrightnessRoi_Width, GreasePara.Magnet2BrightnessRoi_Height);
                        Cv2.Rectangle(image, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet2", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        //第三个
                        magnetroi = new Rect(GreasePara.Magnet3BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet3BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet3BrightnessRoi_Width, GreasePara.Magnet3BrightnessRoi_Height);
                        Cv2.Rectangle(image, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet3", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        //第四个
                        magnetroi = new Rect(GreasePara.Magnet4BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet4BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet4BrightnessRoi_Width, GreasePara.Magnet4BrightnessRoi_Height);
                        Cv2.Rectangle(image, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet4", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        //非debug模式下不显示open的图
                        BinNegativeImage = bin.ToBitmapSource();
                    }
                    //显示相机拍到的图片原图画布
                    ImageObjects[1].DisplayImage = image.ToBitmapSource();
                    //画选中的blob的框
                    for (int i = 0; i < drop3GreaseBlobs.Count; i++)
                    {
                        Cv2.Rectangle(drop3ImageDisplay, drop3GreaseBlobs[i].Rect, Scalar.Red, 1, LineTypes.AntiAlias, 0);//1
                    }
                    //竖线
                    Cv2.Line(drop3ImageDisplay, new Point(GreasePara.Drop3VerticalLine, 0), new Point(GreasePara.Drop3VerticalLine, drop3ImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);//画第三滴红竖线
                    Cv2.Line(drop3ImageDisplay, new Point(GreasePara.Drop3VerticalLine + GreasePara.Drop3GreaseBlobLower_W, 0), new Point(GreasePara.Drop3VerticalLine + GreasePara.Drop3GreaseBlobLower_W, drop3ImageDisplay.Height), Scalar.Red, 1, LineTypes.AntiAlias, 0);
                    //测量出来整个油脂的信息画在图片上
                    Cv2.PutText(drop3ImageDisplay, "L: " + GreasePara.Drop3GreaseBlobLower_W + " < " + Drop3GreaseBlob_W + " < " + GreasePara.Drop3GreaseBlobUpper_W, new Point(5, 15), HersheyFonts.HersheyDuplex, 0.5, Scalar.AntiqueWhite, 0);
                    Cv2.PutText(drop3ImageDisplay, "W: " + GreasePara.Drop3GreaseBlobLower_H + " < " + Drop3GreaseBlob_H + " < " + GreasePara.Drop3GreaseBlobUpper_H, new Point(5, 30), HersheyFonts.HersheyDuplex, 0.5, Scalar.AntiqueWhite, 0);
                    Cv2.PutText(drop3ImageDisplay, "A: " + GreasePara.Drop3GreaseBlobLower_A + " < " + Drop3GreaseBlob_A + " < " + GreasePara.Drop3GreaseBlobUpper_A, new Point(5, 45), HersheyFonts.HersheyDuplex, 0.5, Scalar.AntiqueWhite, 0);
                    Create_dir(drop3_images_path);
                    //小图
                    Cv2.ImWrite(drop3_images_path + Drop3Fixture.ToString() + "_Fixture__" + Drop3OPID + "_OPID__" + DateTime.Now.ToString("yyyy_MM_dd--HH_mm_ss") + ".jpg", drop3ImageDisplay);

                    ImageObjects[4].DisplayImage = drop3ImageDisplay.ToBitmapSource();//小截第三滴图画布
                }
                Run_status[7] = StatusType.end;
            }
            catch (Exception ex)
            {
                Run_status[7] = StatusType.error;
                logger.Error("Occupied_memory:_" + Get_memory());
                logger.Error("Available_memory:_" + Get_available_memory());
                logger.Error("Capacity_memory:_" + Get_sum_memory());
                logger.Error("Drop3GreaseMeasure|  " + ex.Message);
                NegativeJudge = JudgeType.ERROR;

                string grease_error_images_path = Grease_error_images_dir + DateTime.Now.ToString("yyyy_MM_dd") + "\\";
                Create_dir(grease_error_images_path);
                Cv2.ImWrite(grease_error_images_path + "NegativeGreaseError" + Drop3Fixture.ToString() + "_Fixture__" + Drop3OPID + "_OPID__" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".jpg", bitmap.ToMat());
                ImageObjects[1].DisplayImage = bitmap;
                Drop3GreaseBlob_W = -1;
                Drop3GreaseBlob_H = -1;
                Drop3GreaseBlob_A = -1;
                Drop3Judge = Drop3_Abnromal;
            }
        }
        #endregion

        #region 计算棱上油脂，第二滴和第三滴用的
        /// <summary>
        /// 计算棱
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="bin"></param>
        /// <param name="th"></param>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <param name="canvas"></param>
        /// <param name="minibin"></param>
        /// <param name="wha"></param>
        /// <param name="limit_wha"></param>
        /// <param name="judge"></param>
        private void VerticalEdgeSearch(Mat mat, Mat bin, int th, int minor_top, int minor_width, Mat canvas, out Mat minibin, int[] limit_wha, int sub_w, bool isinv_roi, out int judge, out int blob_w, out int blob_h, out int blob_a)
        {
            int roi_x;
            blob_w = 0;
            blob_h = 0;
            blob_a = 0;
            try
            {
                if (isinv_roi)
                {
                    roi_x = mat.Width - sub_w;
                    canvas.Line(new Point(roi_x, 0), new Point(roi_x, mat.Height), Scalar.GreenYellow, 2);//竖线

                }
                else
                {
                    canvas.Line(new Point(sub_w, 0), new Point(sub_w, mat.Height), Scalar.GreenYellow, 2);//竖线
                    roi_x = 0;
                }
                using (Mat avg_h = new Mat())
                using (Mat mat1 = mat.Channels() == 3 ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat)
                using (Mat gray_roi = mat1.SubMat(new Rect(roi_x, 0, sub_w, mat1.Height)))
                {
                    judge = 0;
                    Cv2.Reduce(gray_roi, avg_h, ReduceDimension.Column, ReduceTypes.Avg, -1);//行平均，输出列 ,
                    float mean_h = (float)avg_h.Mean();
                    avg_h.ConvertTo(avg_h, MatType.CV_32FC1);
                    using (Mat projection_v = avg_h / mean_h)// vertical projection  除了平均值比较稳,类似softmax--归一化
                    {
                        int index;


                        if (VerticalUpperEdgeSearch(projection_v, th, 1, 0, out index, canvas))
                        {
                            //projection_v.Dispose();
                            if (GloPara.DebugMode)
                            {
                                canvas.Line(new Point(0, index + minor_top), new Point(canvas.Width, index + minor_top), Scalar.Red, 1);
                                canvas.Line(new Point(0, index + minor_top + minor_width), new Point(canvas.Width, index + minor_top + minor_width), Scalar.Blue, 1);
                            }
                            bin = bin.SubMat(new Rect(0, index + minor_top, bin.Width, minor_width));
                            minibin = bin.Clone();


                            ///这里是直接用，棱上的截图，向下就平均，搜索255的个数，就是棱上油脂的长度，由于中间有何黑点，也会有影响，有点严格，就不用了
                            ///求列平均,
                            //string sss = "";
                            //for (int i = 0; i < bin.Height; i++)
                            //{
                            //    for (int j = 0; j < bin.Width; j++)
                            //    {
                            //        Vec3b color = bin.Get<Vec3b>(i, j); //new Vec3b(); 颜色通道类型 (字节的三元组)，直接视同Get泛型方法返回指定类型
                            //        sss += color.Item0.ToString();
                            //        //color.Item0= m.Get<Vec3b>(i, j).Item0; //R
                            //        //color.Item1 = m.Get<Vec3b>(i, j).Item1; //G
                            //        //color.Item2 = m.Get<Vec3b>(i, j).Item2; //B
                            //    }
                            //    sss += "\n";
                            //}
                            //Write_txt("D:\\OPID\\bin.txt", sss);
                            //logger.Warn(DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss"));
                            //using (Mat bin_avg_h = new Mat())
                            //{
                            //    Cv2.Reduce(bin, bin_avg_h, ReduceDimension.Row, ReduceTypes.Avg, -1);//行平均，输出列 ,
                            //    ushort u16 = 0;
                            //    string ssss = "";

                            //    for (int i = 0; i < bin_avg_h.Height; i++)
                            //    {
                            //        for (int j = 0; j < bin_avg_h.Width; j++)
                            //        {

                            //            Vec3b color = bin_avg_h.Get<Vec3b>(i, j); //new Vec3b(); 颜色通道类型 (字节的三元组)，直接视同Get泛型方法返回指定类型

                            //            ssss += color.Item0.ToString();
                            //            //color.Item0= m.Get<Vec3b>(i, j).Item0; //R
                            //            //color.Item1 = m.Get<Vec3b>(i, j).Item1; //G
                            //            //color.Item2 = m.Get<Vec3b>(i, j).Item2; //B
                            //            ssss += ",";
                            //            if (color.Item0 == 255)
                            //            {
                            //                u16 += 1;
                            //            }
                            //        }
                            //        ssss += "\n";
                            //    }
                            //    logger.Error(u16);

                            //    Write_txt("D:\\OPID\\bin_avg_h.txt", ssss);

                            //}
                            //logger.Warn(DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss"));

                            List<ConnectedComponents.Blob> Drop23MiniBlobs = new List<ConnectedComponents.Blob>();
                            var rr3 = Cv2.ConnectedComponentsEx(bin);
                            foreach (var Recblob in rr3.Blobs.Skip(1))
                            {

                                Drop23MiniBlobs.Add(Recblob);
                            }
                            if (Drop23MiniBlobs.Count > 1)
                            {

                                for (int i = 0; i < Drop23MiniBlobs.Count; i++)
                                {
                                    blob_w += Drop23MiniBlobs[i].Width;
                                    blob_h = Math.Max(blob_h, Drop23MiniBlobs[i].Height);
                                    blob_a += Drop23MiniBlobs[i].Area;
                                }
                                logger.Debug("MimiBlob-Cout>1" + " w:" + blob_w + " h:" + blob_h + " a:" + blob_a);
                            }
                            else if (Drop23MiniBlobs.Count == 1)
                            {

                                blob_w = Drop23MiniBlobs[0].Width;
                                blob_h = Drop23MiniBlobs[0].Height;
                                blob_a = Drop23MiniBlobs[0].Area;

                                logger.Debug("MimiBlob-Cout=1 " + " w:" + Drop23MiniBlobs[0].Width + " h:" + Drop23MiniBlobs[0].Height + " a:" + Drop23MiniBlobs[0].Area);
                            }
                            else
                            {
                                judge = 0;
                                logger.Error("MimiBlob-Cout=0 ");
                            }

                            judge = blob_w > limit_wha[0] &&
                                    blob_h > limit_wha[1] &&
                                    blob_a > limit_wha[2]
                                   ? 1
                                   : 0;
                        }
                        else
                        {
                            judge = -1;
                            minibin = bin.Clone();
                            logger.Error("VerticalUpperEdgeSearch==FALSE ");

                        }
                        canvas.Line(new Point(th, 0), new Point(th, mat.Height), Scalar.Blue, 2);//竖线

                    }
                }
            }
            catch (Exception ex)
            {
                Run_status[4] = StatusType.error;
                Run_status[7] = StatusType.error;

                logger.Error("VerticalEdgeSearch|" + ex.Message);
                judge = 0;
                minibin = bin.Clone();

            }
        }
        #endregion

        #region 检测磁石螺丝
        /// <summary>
        /// 第一二滴面的op是否存在
        /// </summary>
        private void Drop12FaceBrightness()
        {
            double sumbrightness = 0;
            Run_status[5] = StatusType.running;
            SumBrightnessDrop12Judge = brightnessJudge(RotateImageDrop12, DatumPointDrop12_X, DatumPointDrop12_Y, GreasePara.SumBrightnessDrop12Roi_Left, GreasePara.SumBrightnessDrop12Roi_Top, GreasePara.SumBrightnessDrop12Roi_Width, GreasePara.SumBrightnessDrop12Roi_Height, ref sumbrightness, GreasePara.SumBrightnessLimitDrop12, true);
            SumBrightnessDrop12 = sumbrightness;
            Run_status[5] = StatusType.end;


        }

        /// <summary>
        /// 四个磁石中，是否有螺丝
        /// </summary>
        /// <returns></returns>
        private ushort magnetJudge()
        {
            Run_status[8] = StatusType.running;

            double brightness = 0;
            ushort judge = brightnessJudge(RotateImageDrop3, DatumPointDrop3_X, DatumPointDrop3_Y, GreasePara.Magnet1BrightnessRoi_Left, GreasePara.Magnet1BrightnessRoi_Top, GreasePara.Magnet1BrightnessRoi_Width, GreasePara.Magnet1BrightnessRoi_Height, ref brightness, GreasePara.Magnet1BrightnessLimit, false);
            Magnet1Brightness = brightness;
            //或第二个磁石
            judge |= brightnessJudge(RotateImageDrop3, DatumPointDrop3_X, DatumPointDrop3_Y, GreasePara.Magnet2BrightnessRoi_Left, GreasePara.Magnet2BrightnessRoi_Top, GreasePara.Magnet2BrightnessRoi_Width, GreasePara.Magnet2BrightnessRoi_Height, ref brightness, GreasePara.Magnet2BrightnessLimit, false);
            Magnet2Brightness = brightness;
            //或第三个磁石
            judge |= brightnessJudge(RotateImageDrop3, DatumPointDrop3_X, DatumPointDrop3_Y, GreasePara.Magnet3BrightnessRoi_Left, GreasePara.Magnet3BrightnessRoi_Top, GreasePara.Magnet3BrightnessRoi_Width, GreasePara.Magnet3BrightnessRoi_Height, ref brightness, GreasePara.Magnet3BrightnessLimit, false);
            Magnet3Brightness = brightness;
            //或第四个磁石
            judge |= brightnessJudge(RotateImageDrop3, DatumPointDrop3_X, DatumPointDrop3_Y, GreasePara.Magnet4BrightnessRoi_Left, GreasePara.Magnet4BrightnessRoi_Top, GreasePara.Magnet4BrightnessRoi_Width, GreasePara.Magnet4BrightnessRoi_Height, ref brightness, GreasePara.Magnet4BrightnessLimit, false);
            Magnet4Brightness = brightness;
            Run_status[8] = StatusType.end;

            return judge;

        }
        #endregion

        #region 两个面的线程
        /// <summary>
        /// 第一二滴面线程
        /// </summary>
        private void Grease12Start()
        {
            try
            {

                for (int i = 3; i < 6; i++)
                {
                    Run_status[i] = StatusType.wait;

                }
                BitmapSource tempImg;
                if (SpinCtrl.AcquisitionBitmapFromCam(1, out tempImg))//拍照
                {
                    GreaseSourceImage = tempImg;
                    RotateImageDrop12 = Rotate_arbitrarily_angle(GreaseSourceImage, GreasePara.RotateAngleDrop12);//旋转图片
                    ImageObjects[0].DisplayImage = RotateImageDrop12;//第一二滴的大图

                    DatumPointDrop12Capture(RotateImageDrop12);//获取定位点
                    if (DatumPointDrop12_X != 0 && DatumPointDrop12_Y != 0)//定位点不为0
                    {
                        GC.Collect();

                        Task t = new Task(() => Drop12FaceBrightness());//检测是否有OP架，亮度检测
                        t.Start();

                        if (Drop1GreaseMeasure(RotateImageDrop12))//第12滴面，没有报错时
                        {
                            t.Wait();//阻塞线程，等待线程结束后继续
                            ushort write43 = (ushort)(Drop1Judge | Drop2Judge | Drop1LessPosition | SumBrightnessDrop12Judge);
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 43, 1, new ushort[] { write43 });//结果写给43
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 11 });//处理完。
                        }
                        else
                        {
                            Drop1Judge = Drop1_Abnromal;//现在Abnromal状态和多量是相同的
                            Drop2Judge = Drop2_Abnromal;
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 43, 1, new ushort[] { Drop2_Abnromal | Drop1_Abnromal });//结果,出错写多量
                            PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 11 });//处理完
                            logger.Warn("Anchor does not exist");
                            PositiveJudge = JudgeType.ERROR;//冒色
                            Positive2Judge = JudgeType.ERROR;//冒黄色
                        }
                    }
                    else
                    {
                        Drop1Judge = Drop1_Abnromal;
                        Drop2Judge = Drop2_Abnromal;
                        Run_status[3] = StatusType.error;
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 43, 1, new ushort[] { Drop2_Abnromal | Drop1_Abnromal });//结果,没定位写多量
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 11 });//处理完
                        logger.Warn("Anchor does not exist");
                        PositiveJudge = JudgeType.ERROR;//冒黄色
                        Positive2Judge = JudgeType.ERROR;//冒黄色
                    }
                    //判断是否判非OK，记录是否连续NG
                    QueueBoolEnqueueLimit(Drop1GreaseContinuousBadQueue, Drop1Judge != Drop1_OK, GreasePara.GreaseContinuousBadLimit);
                    QueueBoolEnqueueLimit(Drop2GreaseContinuousBadQueue, Drop2Judge != Drop2_OK, GreasePara.GreaseContinuousBadLimit);
                    if (QueueBoolTrueCount(Drop1GreaseContinuousBadQueue) >= GreasePara.GreaseContinuousBadLimit)
                    {
                        GreaseContinuousBadDrop = 1;
                        GreaseContinuousBad = suspendRequested = true;

                    }
                    if (QueueBoolTrueCount(Drop2GreaseContinuousBadQueue) >= GreasePara.GreaseContinuousBadLimit)
                    {
                        GreaseContinuousBadDrop = 2;
                        GreaseContinuousBad = suspendRequested = true;
                    }
                }
                else
                {
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 43, 1, new ushort[] { Drop2_Abnromal | Drop1_Abnromal });//结果
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 11 });//处理完
                    PositiveJudge = JudgeType.ERROR;
                    Positive2Judge = JudgeType.ERROR;
                    throw new ArgumentOutOfRangeException("No image"); // throw
                }
                SaveData(@"Data\\", SaveDataType.drop12);
            }
            catch (Exception ex)
            {
                logger.Error("GreaseStartDrop12|__" + ex.Message);
                if (PlcCtrl.PlcIsConnected)
                {
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 43, 1, new ushort[] { Drop2_Abnromal | Drop1_Abnromal });//结果
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 11 });//处理完
                }
                PositiveJudge = JudgeType.ERROR;
                Positive2Judge = JudgeType.ERROR;
            }
        }


        /// <summary>
        /// 第三滴面线程
        /// </summary>
        private void Grease3Start()
        {
            try
            {

                for (int i = 6; i < 9; i++)
                {
                    Run_status[i] = StatusType.wait;

                }
                BitmapSource tempImg1;
                if (SpinCtrl.AcquisitionBitmapFromCam(2, out tempImg1))//拍照
                {
                    GreaseSourceImage2 = tempImg1;

                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 23 });//23表示拍照完，告诉PLC

                    RotateImageDrop3 = Rotate_arbitrarily_angle(GreaseSourceImage2, GreasePara.RotateAngle);//旋转图片
                    ImageObjects[1].DisplayImage = RotateImageDrop3;//用于显示

                    DatumPointDrop3Capture(RotateImageDrop3);//定位点获取

                    if (DatumPointDrop3_X != 0 && DatumPointDrop3_Y != 0)//定位点不为0时
                    {
                        //GC.Collect();

                        Task t = new Task(() => Drop3GreaseMeasure(RotateImageDrop3));//创建了线程，第3滴检测

                        t.Start();
                        ushort isMagnetScrew = magnetJudge();//检测磁石

                        t.Wait();
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 46, 1, new ushort[] { (ushort)(Drop3Judge | isMagnetScrew) });//写第3滴结果
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 21 });//21表示反面处理完

                    }
                    else
                    {
                        Drop3Judge = Drop3_Abnromal;
                        Run_status[6] = StatusType.error;
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 23 });//23表示拍照完
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 46, 1, new ushort[] { Drop3Judge });//写第3滴结果
                        PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 21 });//21表示反面处理完

                        logger.Warn("Anchor does not exist");
                        NegativeJudge = JudgeType.ERROR;

                    }
                    //判断是否判非OK，检测是否连续非OK状态
                    QueueBoolEnqueueLimit(Drop3GreaseContinuousBadQueue, Drop3Judge != Drop3_OK, GreasePara.GreaseContinuousBadLimit);
                    if (QueueBoolTrueCount(Drop3GreaseContinuousBadQueue) >= GreasePara.GreaseContinuousBadLimit)
                    {
                        GreaseContinuousBadDrop = 3;
                        GreaseContinuousBad = suspendRequested = true;
                    }
                }
                else
                {

                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 23 });//23表示拍照完
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 46, 1, new ushort[] { Drop3_Abnromal });//结果
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 21 });//21表示反面处理完
                    NegativeJudge = JudgeType.ERROR;

                    throw new ArgumentOutOfRangeException("No image"); // throw

                }
                SaveData(@"Data\\", SaveDataType.drop3);//
                                                        //DiskMg.GetDiskSpaceInfo();
            }


            catch (Exception ex)
            {
                logger.Error("GreaseStart|__" + ex.Message);
                if (PlcCtrl.PlcIsConnected)
                {
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 23 });//23表示拍照完
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 46, 1, new ushort[] { Drop3_Abnromal });
                    PlcCtrl.PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 21 });//21表示反面处理完
                }
                NegativeJudge = JudgeType.ERROR;
                //Grease3Start();
            }

        }

        #endregion

        #region 方法


        /// <summary>
        /// Search for upper edge in horizontal projection 
        /// </summary>
        /// <param name="horizon_proj"></param>
        /// <param name="th"></param>
        /// <param name="scan_dir">scan direcetion: 0, left --> right; 1, right --> left</param>
        /// <param name="edgeMode">edge detection mode: 0, rising edge; 1, falling edge</param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool HorizontalUpperEdgeSearch(Mat horizon_proj, float th, int scan_dir, int edgeMode, out int index)
        {
            // left --> right
            if (scan_dir == 0)
            {
                for (int c = 0; c <= horizon_proj.Cols - 1; c++)
                {
                    float val = horizon_proj.At<float>(0, c);
                    //logger.Debug(string.Format("horizontal reverse proj|{0}|{1}", c, val));

                    if (edgeMode == 0)       // rising edge
                    {
                        if (val > th)
                        {
                            index = c;
                            return true;
                        }
                    }
                    else                    // falling edge
                    {
                        if (val < th)
                        {
                            index = c;
                            return true;
                        }
                    }
                }
            }
            // right --> left
            else
            {
                for (int c = horizon_proj.Cols - 1; c >= 0; c--)
                {
                    float val = horizon_proj.At<float>(0, c);
                    //logger.Debug(string.Format("horizontal reverse proj|{0}|{1}", c, val));

                    if (edgeMode == 0)       // rising edge
                    {
                        if (val > th)
                        {
                            index = c;
                            return true;
                        }
                    }
                    else                    // falling edge
                    {
                        if (val < th)
                        {
                            index = c;
                            return true;
                        }
                    }
                }
            }

            index = 0;
            return false;
        }

        /// <summary>
        /// Search for upper edge in vertical projection
        /// </summary>
        /// <param name="vertical_proj"></param>
        /// <param name="th"></param>
        /// <param name="scan_dir">scan direcetion: 0, top --> bottom; 1, bottom --> top</param>
        /// <param name="edgeMode">edge detection mode: 0, rising edge; 1, falling edge</param>
        /// <param name="reverse"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool VerticalUpperEdgeSearch(Mat vertical_proj, float th, int scan_dir, int edgeMode, out int index)
        {
            //  top --> bottom
            if (scan_dir == 0)
            {
                for (int r = 0; r <= vertical_proj.Rows - 1; r++)
                {
                    float val = vertical_proj.At<float>(r, 0);
                    //logger.Debug(string.Format("vertical proj|{0}|{1}", r, val));

                    if (edgeMode == 0)       // rising edge
                    {
                        if (val > th)
                        {
                            index = r;
                            return true;
                        }
                    }
                    else                    // falling edge
                    {
                        if (val < th)
                        {
                            index = r;
                            return true;
                        }
                    }
                }
            }
            // bottom --> top
            else
            {
                for (int r = vertical_proj.Rows - 1; r >= 0; r--)
                {
                    float val = vertical_proj.At<float>(r, 0);
                    //logger.Debug(string.Format("vertical proj|{0}|{1}", r, val));

                    if (edgeMode == 0)       // rising edge
                    {
                        if (val > th)
                        {
                            index = r;
                            return true;
                        }
                    }
                    else                    // falling edge
                    {
                        if (val < th)
                        {
                            index = r;
                            return true;
                        }
                    }
                }
            }

            index = 0;
            return false;
        }




        /// <summary>
        /// 记录连续异常
        /// </summary>
        private void GreaseContinuousBadInit()
        {
            if (Drop1GreaseContinuousBadQueue.Count > 0)
            {
                Drop1GreaseContinuousBadQueue.Clear();
            }
            if (Drop2GreaseContinuousBadQueue.Count > 0)
            {
                Drop2GreaseContinuousBadQueue.Clear();
            }
            if (Drop3GreaseContinuousBadQueue.Count > 0)
            {
                Drop3GreaseContinuousBadQueue.Clear();
            }
            for (int i = 0; i <= GreasePara.GreaseContinuousBadLimit; i++)
            {
                Drop1GreaseContinuousBadQueue.Enqueue(false);
                Drop2GreaseContinuousBadQueue.Enqueue(false);
                Drop3GreaseContinuousBadQueue.Enqueue(false);
            }
        }
        /// <summary>
        /// 检测异常个数
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        private ushort QueueBoolTrueCount(Queue<bool> ts)
        {
            ushort true_count = 0;
            if (ts.Count > 0)
            {
                foreach (bool item in ts)
                {
                    if (item)
                    {
                        true_count += 1;
                    }
                }
            }
            return true_count;
        }
        /// <summary>
        /// 移动限制异常个数
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="enqueue_element"></param>
        /// <param name="limit"></param>
        private void QueueBoolEnqueueLimit(Queue<bool> ts, bool enqueue_element, ushort limit)
        {
            ts.Enqueue(enqueue_element);
            while (ts.Count > limit)
            {
                ts.Dequeue();
            }
        }

        /// <summary>
        /// true时是否有OP在治具上，flas时是否有螺丝在磁石上，
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="datum_x"></param>
        /// <param name="datum_y"></param>
        /// <param name="roi_x"></param>
        /// <param name="roi_y"></param>
        /// <param name="roi_w"></param>
        /// <param name="roi_h"></param>
        /// <param name="brightness"></param>
        /// <param name="brightnesslimit"></param>
        /// <returns></returns>
        private ushort brightnessJudge(BitmapSource bitmap, int datum_x, int datum_y, int roi_x, int roi_y, int roi_w, int roi_h, ref double brightness, double brightnesslimit, bool isupperlimit)
        {
            try
            {

                using (Mat mat = bitmap.ToMat())
                {
                    brightness = 0;
                    Rect roi = new Rect(roi_x + datum_x, roi_y + datum_y, roi_w, roi_h);
                    brightness = getSumGrayBrightness(mat.SubMat(roi).ToBitmapSource());
                    if (isupperlimit)
                    {
                        return brightness <= brightnesslimit ? (ushort)0b0 : (ushort)0b10000000000;
                    }
                    else
                    {
                        return brightness >= brightnesslimit ? (ushort)0b0 : (ushort)0b10000000000;
                    }
                }
            }
            catch (Exception ex)
            {
                Run_status[5] = StatusType.error;
                Run_status[8] = StatusType.error;
                logger.Error("isOpOn|_" + ex.Message);
                return 0b10000000000;
            }

        }
        /// <summary>
        /// 计算图片总亮度
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        private double getSumGrayBrightness(BitmapSource mat)
        {
            try
            {
                using (Mat img = mat.ToMat())
                {
                    if (img.Channels() == 3)
                    {
                        Cv2.CvtColor(img, img, ColorConversionCodes.BGR2GRAY);
                    }
                    Scalar AvgBot = img.Sum();

                    return AvgBot.Val0;
                }
            }
            catch (Exception ex)
            {
                logger.Error("getSumGrayBrightness|_" + ex.Message);
                return 0;
            }

        }
        /// <summary>
        /// 拷贝文件夹到指定文件夹并更改文件夹名称
        /// </summary>
        /// <param name="srcPath">源文件夹</param>
        /// <param name="aimPath">目标文件夹+文件夹名称</param>
        public bool CopyDir(string srcPath, string aimPath)
        {
            try
            {
                // 检查目标目录是否以目录分割字符结束如果不是则添加
                if (aimPath[aimPath.Length - 1] != System.IO.Path.DirectorySeparatorChar)
                {
                    aimPath += System.IO.Path.DirectorySeparatorChar;
                }
                // 判断目标目录是否存在如果不存在则新建

                Create_dir(aimPath);

                // 得到源目录的文件列表，该里面是包含文件以及目录路径的一个数组
                // 如果你指向copy目标文件下面的文件而不包含目录请使用下面的方法
                // string[] fileList = Directory.GetFiles（srcPath）；
                string[] fileList = System.IO.Directory.GetFileSystemEntries(srcPath);
                // 遍历所有的文件和目录
                foreach (string file in fileList)
                {
                    // 先当作目录处理如果存在这个目录就递归Copy该目录下面的文件
                    if (System.IO.Directory.Exists(file))
                    {
                        CopyDir(file, aimPath + System.IO.Path.GetFileName(file));
                    }
                    // 否则直接Copy文件
                    else
                    {
                        System.IO.File.Copy(file, aimPath + System.IO.Path.GetFileName(file), true);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("CopyDir|_" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 旋转图片用,要100多毫秒，+逆时针旋转，-顺时针旋转
        /// </summary>
        /// <param name="src"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        BitmapSource Rotate_arbitrarily_angle(BitmapSource bitmapSource, float angle)
        {
            using (Mat src = bitmapSource.ToMat())
            using (Mat dst = new Mat())
            {
                float radian = (float)(angle / 180.0 * Cv2.PI);

                //填充图像
                int maxBorder = (int)(Math.Max(src.Cols, src.Rows) * 1.414); //即为sqrt(2)*max
                int dx = (maxBorder - src.Cols) / 2;
                int dy = (maxBorder - src.Rows) / 2;
                Cv2.CopyMakeBorder(src, dst, dy, dy, dx, dx, BorderTypes.Constant);

                //旋转
                Point2f center = new Point2f((float)(dst.Cols / 2), (float)(dst.Rows / 2));
                using (Mat affine_matrix = Cv2.GetRotationMatrix2D(center, angle, 1.0))//求得旋转矩阵
                {
                    Cv2.WarpAffine(dst, dst, affine_matrix, dst.Size());

                    //计算图像旋转之后包含图像的最大的矩形
                    float sinVal = (float)Math.Abs(Math.Sin(radian));
                    float cosVal = (float)Math.Abs(Math.Cos(radian));
                    OpenCvSharp.Size targetSize = new OpenCvSharp.Size((int)(src.Cols * cosVal + src.Rows * sinVal),
                             (int)(src.Cols * sinVal + src.Rows * cosVal));
                    //剪掉多余边框
                    int x = (dst.Cols - targetSize.Width) / 2;
                    int y = (dst.Rows - targetSize.Height) / 2;
                    Rect rect = new Rect(x, y, targetSize.Width, targetSize.Height);
                    using (Mat mat = new Mat(dst, rect))
                    {
                        BitmapSource bitmap = mat.ToBitmapSource();
                        return bitmap;
                    }
                }
            }
        }


        /// <summary>
        /// Search for upper edge in vertical projection
        /// </summary>
        /// <param name="vertical_proj"> 列平均</param>
        /// <param name="th"></param>
        /// <param name="scan_dir">scan direcetion: 0, top --> bottom; 1, bottom --> top</param>
        /// <param name="edgeMode">edge detection mode: 0, rising edge; 1, falling edge</param>
        /// <param name="reverse"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool VerticalUpperEdgeSearch(Mat vertical_proj, float th, int scan_dir, int edgeMode, out int index, Mat canvas)
        {
            int maxw = 60;//放大倍数？

            //  top --> bottom
            if (scan_dir == 0)
            {

                for (int r = 0; r <= vertical_proj.Rows - 1; r++)
                {
                    float val = maxw * vertical_proj.At<float>(r, 0);
                    //logger.Debug(string.Format("vertical proj|{0}|{1}", r, val));

                    if (edgeMode == 0)       // rising edge
                    {
                        if (GloPara.DebugMode)
                        {
                            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.Red);//一条一条线地画出来

                        }


                        if (val > th)
                        {
                            index = r;
                            return true;
                        }

                    }
                    else                    // falling edge
                    {
                        if (GloPara.DebugMode)
                        {
                            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.Red);//一条一条线地画出来
                        }

                        if (val < th)
                        {
                            index = r;
                            return true;
                        }
                    }
                }
            }
            // bottom --> top
            else
            {
                for (int r = vertical_proj.Rows - 1; r >= 0; r--)
                {
                    float val = maxw * vertical_proj.At<float>(r, 0);
                    //logger.Debug(string.Format("vertical proj|{0}|{1}", r, val));
                    if (edgeMode == 0)       // rising edge
                    {
                        if (GloPara.DebugMode)
                        {
                            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.Red);//一条一条线地画出来
                        }

                        if (val > th)
                        {
                            index = r;
                            return true;
                        }
                    }
                    else // falling edge
                    {
                        if (GloPara.DebugMode)
                        {
                            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.Red);//一条一条线地画出来

                        }
                        if (val < th)
                        {
                            index = r;
                            return true;
                        }
                    }
                }
            }

            index = 0;
            return false;
        }


        #region 补油脂时，位置判断
        /// <summary>
        /// 补油脂时，检查油脂哪个位置没有,补油脂，用红竖线判断位置
        /// </summary>
        /// <param name="measureLengthStart"></param>
        /// <param name="measureLengthEnd"></param>
        /// <param name="blobs"></param>
        /// <returns></returns>
        private ushort repairGrease(int measureLengthStart, int measureLengthEnd, List<ConnectedComponents.Blob> blobs)
        {
            try
            {
                if (blobs.Count == 1)
                {
                    //只有一点在左边
                    if (blobs[0].Centroid.X <= (measureLengthStart + measureLengthEnd) / 2)
                    {   //位置在左边start前，且尾端超过1/2
                        if (blobs[0].Rect.Left <= measureLengthStart &&
                            blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0110;
                        }
                        //位置在左边start前，且尾端不超过1/2
                        else if (blobs[0].Rect.Left <= measureLengthStart &&
                                 blobs[0].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0100;
                        }
                        //位置左边超过start，且尾端超过1/2，即在中间
                        else if (blobs[0].Rect.Left >= measureLengthStart &&
                                 blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，及靠中间的一小点。
                        else
                        {
                            return 0b0000;
                        }
                    }
                    //只有一点,且在右边
                    else if (blobs[0].Centroid.X > (measureLengthStart + measureLengthEnd) / 2)
                    {
                        //位置在有边end后，且前端不超过1/2
                        if (blobs[0].Rect.Right >= measureLengthEnd &&
                            blobs[0].Rect.Left <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0011;
                        }
                        //位置在右边end后，且前端超过1/2
                        else if (blobs[0].Rect.Right >= measureLengthEnd &&
                                 blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0001;
                        }
                        //位置右边在end前，且前端不超过1/2，即在中间
                        else if (blobs[0].Rect.Right <= measureLengthStart &&
                                 blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，及靠中间的一小点。
                        else
                        {
                            return 0b0000;
                        }
                    }
                    //
                    else
                    {
                        return 0b0000;
                    }
                }
                else if (blobs.Count == 2)
                {
                    //[0]是大坨的，在左边，
                    if (blobs[0].Area >= 2 * blobs[1].Area &&
                        blobs[0].Centroid.X < blobs[1].Centroid.X)
                    {
                        //大坨的比较长的情况下，直接补小坨位置
                        if (blobs[0].Rect.Left <= measureLengthStart &&
                            blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0110;
                        }
                        //大坨的左端在start前，且末端没过中心
                        else if (blobs[0].Rect.Left <= measureLengthStart &&
                                 blobs[0].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0100;
                        }
                        //大坨的左端没过start，且有端多了中心
                        else if (blobs[0].Rect.Left >= measureLengthStart &&
                                 blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，左端过了start又端没过中心
                        else
                        {
                            return 0b0000;
                        }

                    }
                    //[0]是大坨的，在右边
                    else if (blobs[0].Area >= 2 * blobs[1].Area &&
                            blobs[0].Centroid.X > blobs[1].Centroid.X)
                    {
                        //大坨的比较长的情况下，直接补小坨位置
                        if (blobs[0].Rect.Right >= measureLengthEnd &&
                            blobs[0].Rect.Left <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0011;
                        }
                        //大坨的右端在end后，且前端到过中心
                        else if (blobs[0].Rect.Right >= measureLengthEnd &&
                                 blobs[0].Rect.Left >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0001;
                        }
                        //大坨的右端没过end，且左端到了中心
                        else if (blobs[0].Rect.Right <= measureLengthEnd &&
                                 blobs[0].Rect.Left <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，右端过了end左端没过中心
                        else
                        {
                            return 0b0000;
                        }
                    }
                    //[1]是大坨的，在右边
                    else if (blobs[0].Area <= 2 * blobs[1].Area &&
                             blobs[0].Centroid.X < blobs[1].Centroid.X)
                    {
                        //大坨的比较长的情况下，直接补小坨位置
                        if (blobs[1].Rect.Right >= measureLengthEnd &&
                            blobs[1].Rect.Left <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0011;
                        }
                        //大坨的右端在end后，且前端到过中心
                        else if (blobs[1].Rect.Right >= measureLengthEnd &&
                                 blobs[1].Rect.Left >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0001;
                        }
                        //大坨的右端没过end，且左端到了中心
                        else if (blobs[1].Rect.Right <= measureLengthEnd &&
                                 blobs[1].Rect.Left <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，右端过了end左端没过中心
                        else
                        {
                            return 0b0000;
                        }
                    }
                    //[1]是大坨的，在左边
                    else if (blobs[0].Area <= 2 * blobs[1].Area &&
                             blobs[0].Centroid.X > blobs[1].Centroid.X)
                    {
                        //大坨的比较长的情况下，直接补小坨位置
                        if (blobs[1].Rect.Left <= measureLengthStart &&
                            blobs[1].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0110;
                        }
                        //大坨的左端在start前，且末端没过中心
                        else if (blobs[1].Rect.Left <= measureLengthStart &&
                                 blobs[1].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0100;
                        }
                        //大坨的左端没过start，且有端多了中心
                        else if (blobs[1].Rect.Left >= measureLengthStart &&
                                 blobs[1].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2)
                        {
                            return 0b0010;
                        }
                        //其他情况，左端过了start又端没过中心
                        else
                        {
                            return 0b0000;
                        }
                    }
                    //两坨差不多
                    else
                    {
                        //[0]为左边
                        if (blobs[0].Centroid.X < blobs[1].Centroid.X)
                        {
                            if (blobs[0].Rect.Left <= measureLengthStart &&
                                blobs[0].Rect.Right >= measureLengthStart &&
                                blobs[1].Rect.Right >= measureLengthEnd &&
                                blobs[1].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0101;
                            }
                            else if (blobs[0].Rect.Left >= measureLengthStart &&
                                     blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[1].Rect.Right >= measureLengthEnd &&
                                     blobs[1].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0011;
                            }
                            else if (blobs[0].Rect.Left >= measureLengthStart &&
                                     blobs[0].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[1].Rect.Right >= measureLengthEnd &&
                                     blobs[1].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0001;
                            }
                            else if (blobs[0].Rect.Left >= measureLengthStart &&
                                     blobs[0].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[1].Rect.Right <= measureLengthEnd &&
                                     blobs[1].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0010;
                            }
                            else if (blobs[0].Rect.Left >= measureLengthStart &&
                                     blobs[0].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[1].Rect.Right <= measureLengthEnd &&
                                     blobs[1].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0000;
                            }

                            else
                            {
                                return 0b0000;
                            }
                        }
                        else
                        {
                            //[1]为左边
                            if (blobs[1].Rect.Left <= measureLengthStart &&
                                blobs[1].Rect.Right >= measureLengthStart &&
                                blobs[0].Rect.Right >= measureLengthEnd &&
                                blobs[0].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0101;
                            }
                            else if (blobs[1].Rect.Left >= measureLengthStart &&
                                     blobs[1].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[0].Rect.Right >= measureLengthEnd &&
                                     blobs[0].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0011;
                            }
                            else if (blobs[1].Rect.Left >= measureLengthStart &&
                                     blobs[1].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[0].Rect.Right >= measureLengthEnd &&
                                     blobs[0].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0001;
                            }
                            else if (blobs[1].Rect.Left >= measureLengthStart &&
                                     blobs[1].Rect.Right >= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[0].Rect.Right <= measureLengthEnd &&
                                     blobs[0].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0010;
                            }
                            else if (blobs[1].Rect.Left >= measureLengthStart &&
                                     blobs[1].Rect.Right <= (measureLengthStart + measureLengthEnd) / 2 &&
                                     blobs[0].Rect.Right <= measureLengthEnd &&
                                     blobs[0].Rect.Left <= measureLengthEnd)
                            {
                                return 0b0000;
                            }

                            else
                            {
                                return 0b0000;
                            }
                        }
                    }
                }
                //超过两滴，一律重新涂
                else
                {
                    return 0b0000;

                }
            }
            catch (Exception ex)
            {
                logger.Error("repairGrease|  " + ex.Message);
                return 0b0000;
            }
        }

        #endregion


        #endregion

        #endregion

        #region Command






        private ICommand _BackupConfigsCommand;

        public ICommand BackupConfigsCommand
        {
            get
            {
                if (_BackupConfigsCommand == null)
                {
                    _BackupConfigsCommand = new RelayCommand(
                        param => this.UseBackupConfigs(),
                        param => this.CanBackupConfigs()
                    );
                }
                return _BackupConfigsCommand;
            }
        }
        private bool CanBackupConfigs()
        {
            return true;
        }
        private void UseBackupConfigs()
        {
            logger.Info("Occupied_memory:_" + Get_memory());
            logger.Info("Available_memory:_" + Get_available_memory());
            logger.Info("Capacity_memory:_" + Get_sum_memory());
            if (CopyDir(CreateJsonFolder, "ConfigsBackup\\" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss") + "\\" + CreateJsonFolder + "\\"))
            {
                MessageBox.Show("已经备份了，不要再点了！\n(#｀-_ゝ-)");

            }
            GC.Collect();
            //PlcCtrl.BtnConnect();

        }




        public ICommand CleanCountCommand => new RelayCommand(obj => 
        {
            if (MessageBox.Show("确认要清零？\nw(ﾟДﾟ)w\n…(⊙_⊙;)…", "清零", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
            {
                logger.Info("清零取消");

            }
            else
            {
                CleanCount();
                logger.Info("已清零");

            }

        });





        #region 第一二滴面
        /// <summary>
        /// 找本地图片名字用的方法
        /// </summary>
        /// <returns></returns>
        private string UselocalImageFlie()
        {
            try
            {
                Microsoft.Win32.OpenFileDialog openfiledialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.png;*.jpeg;*.bmp;*.gif|所有文件|*.*"
                };

                if ((bool)openfiledialog.ShowDialog())
                {
                    return openfiledialog.FileName;

                }
                else
                {
                    return null;

                }
            }
            catch (Exception ex)
            {
                logger.Error("UseImageOjectDrop3UseImage!" + ex.Message);
                return null;
            }
        }








        private ICommand _ImageOjectDrop12UseImageCommand;

        public ICommand ImageOjectDrop12UseImageCommand
        {
            get
            {
                if (_ImageOjectDrop12UseImageCommand == null)
                {
                    _ImageOjectDrop12UseImageCommand = new RelayCommand(
                        param => this.UseImageOjectDrop12UseImage(),
                        param => this.CanImageOjectDrop12UseImage()
                    );
                }
                return _ImageOjectDrop12UseImageCommand;
            }
        }
        private bool CanImageOjectDrop12UseImage()
        {
            return true;
        }
        private void UseImageOjectDrop12UseImage()
        {


            string imageFileNmae = UselocalImageFlie();
            if (imageFileNmae != null)
            {
                logger.Warn(IsRotate);
                if (IsRotate)
                {
                    using (Mat MatMain = Rotate_arbitrarily_angle(new Mat(imageFileNmae, ImreadModes.Unchanged).ToBitmapSource(), GreasePara.RotateAngleDrop12).ToMat())
                    {
                        ImageObjects[0].DisplayImage = MatMain.ToBitmapSource();
                        ImageObjects[0].SourceImage = MatMain.ToBitmapSource();
                    }
                }
                else
                {
                    using (Mat MatMain = new Mat(imageFileNmae, ImreadModes.Unchanged))
                    {
                        ImageObjects[0].DisplayImage = MatMain.ToBitmapSource();
                        ImageObjects[0].SourceImage = MatMain.ToBitmapSource();
                    }

                }

            }
        }

        /// <summary>
        /// 第一个图的处理按钮
        /// </summary>
        private ICommand _ImageOjectDrop12DatumPointDrop12Command;

        public ICommand ImageOjectDrop12DatumPointDrop12Command
        {
            get
            {
                if (_ImageOjectDrop12DatumPointDrop12Command == null)
                {
                    _ImageOjectDrop12DatumPointDrop12Command = new RelayCommand(
                        param => this.UseImageOjectDrop12DatumPointDrop12(),
                        param => this.CanImageOjectDrop12DatumPointDrop12()
                    );
                }
                return _ImageOjectDrop12DatumPointDrop12Command;
            }
        }
        private bool CanImageOjectDrop12DatumPointDrop12()
        {
            return true;
        }
        private void UseImageOjectDrop12DatumPointDrop12()
        {
            if (ImageObjects[0].SourceImage != null)
            {
                ImageObjects[0].SourceImage = ImageObjects[0].DisplayImage.Clone();
                DatumPointDrop12Capture(ImageObjects[0].SourceImage);
                if (DatumPointDrop12_X == 0 || DatumPointDrop12_Y == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看Minor图层，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    using (Mat mat = ImageObjects[0].SourceImage.ToMat())
                    {
                        Cv2.Line(mat, DatumPointDrop12_X - 200, DatumPointDrop12_Y - 0, DatumPointDrop12_X + 200, DatumPointDrop12_Y + 0, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                        Cv2.Line(mat, DatumPointDrop12_X - 0, DatumPointDrop12_Y - 200, DatumPointDrop12_X + 0, DatumPointDrop12_Y + 200, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                        //画定位点范围
                        Rect roi = new Rect(GreasePara.SumBrightnessDrop12Roi_Left + DatumPointDrop12_X, GreasePara.SumBrightnessDrop12Roi_Top + DatumPointDrop12_Y, GreasePara.SumBrightnessDrop12Roi_Width, GreasePara.SumBrightnessDrop12Roi_Height);
                        Cv2.Rectangle(mat, roi, Scalar.GreenYellow, 20, LineTypes.AntiAlias, 0);//3
                        Cv2.Rectangle(mat, new Rect(GreasePara.DatumPointDrop12_Left, GreasePara.DatumPointDrop12_Top, GreasePara.DatumPointDrop12_Width, GreasePara.DatumPointDrop12_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//1
                        double sumbri = getSumGrayBrightness(mat.SubMat(roi).ToBitmapSource());
                        logger.Info("正面小图总亮度： " + sumbri);
                        SumBrightnessDrop12 = sumbri;
                        ImageObjects[0].DisplayImage = mat.ToBitmapSource();
                    }
                }
            }

        }

        /// <summary>
        /// 画亮度按钮
        /// </summary>
        private ICommand _ImageOjectDrop12SaveMarkCommand;

        public ICommand ImageOjectDrop12SaveMarkCommand
        {
            get
            {
                if (_ImageOjectDrop12SaveMarkCommand == null)
                {
                    _ImageOjectDrop12SaveMarkCommand = new RelayCommand(
                        param => this.UseImageOjectDrop12SaveMark(),
                        param => this.CanImageOjectDrop12SaveMark()
                    );
                }
                return _ImageOjectDrop12SaveMarkCommand;
            }
        }
        private bool CanImageOjectDrop12SaveMark()
        {
            return true;
        }
        private void UseImageOjectDrop12SaveMark()
        {
            try
            {
                if (ImageObjects[0].DisplayImage != null)
                {
                    int[] xywh = RoiPointLimit(ImageObjects[0].MarkLeft, ImageObjects[0].MarkTop, ImageObjects[0].MarkWidth, ImageObjects[0].MarkHeight, ImageObjects[0].DisplayImage);
                    ImageObjects[0].MarkLeft = xywh[0];
                    ImageObjects[0].MarkTop = xywh[1];
                    ImageObjects[0].MarkWidth = xywh[2];
                    ImageObjects[0].MarkHeight = xywh[3];
                    GreasePara.SumBrightnessDrop12Roi_Left = ImageObjects[0].MarkLeft - DatumPointDrop12_X;
                    GreasePara.SumBrightnessDrop12Roi_Top = ImageObjects[0].MarkTop - DatumPointDrop12_Y;
                    GreasePara.SumBrightnessDrop12Roi_Width = ImageObjects[0].MarkWidth;
                    GreasePara.SumBrightnessDrop12Roi_Height = ImageObjects[0].MarkHeight;

                    Rect roi = new Rect(GreasePara.SumBrightnessDrop12Roi_Left + DatumPointDrop12_X, GreasePara.SumBrightnessDrop12Roi_Top + DatumPointDrop12_Y, GreasePara.SumBrightnessDrop12Roi_Width, GreasePara.SumBrightnessDrop12Roi_Height);


                    using (Mat image = ImageObjects[0].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(image, roi, Scalar.IndianRed, 20, LineTypes.AntiAlias, 0);//3
                        ImageObjects[0].DisplayImage = image.ToBitmapSource();
                        double sumbri = getSumGrayBrightness(image.SubMat(roi).ToBitmapSource());
                        logger.Info("正面小图总亮度： " + sumbri);
                        SumBrightnessDrop12 = sumbri;


                    }
                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");

                }
                else
                {
                    MessageBox.Show("先按使用图片，吾该！！！\n(#｀-_ゝ-)");

                }
            }
            catch (Exception ex)
            {
                logger.Error("UseImageOjectDrop12SaveMark!" + ex.Message);
            }

        }





        /// <summary>
        /// 第1滴相对位置
        /// </summary>
        private ICommand _ImageOjectDrop1MinorSavePositionCommand;

        public ICommand ImageOjectDrop1MinorSavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop1MinorSavePositionCommand == null)
                {
                    _ImageOjectDrop1MinorSavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop1MinorSavePosition(),
                        param => this.CanImageOjectDrop1MinorSavePosition()
                    );
                }
                return _ImageOjectDrop1MinorSavePositionCommand;
            }
        }
        private bool CanImageOjectDrop1MinorSavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop1MinorSavePosition()
        {
            if (DatumPointDrop12_X != 0 || DatumPointDrop12_Y != 0)
            {
                if (ImageObjects[0].DisplayImage != null)
                {
                    int[] xywh = RoiPointLimit(ImageObjects[0].MarkLeft, ImageObjects[0].MarkTop, ImageObjects[0].MarkWidth, ImageObjects[0].MarkHeight, ImageObjects[0].DisplayImage);
                    ImageObjects[0].MarkLeft = xywh[0];
                    ImageObjects[0].MarkTop = xywh[1];
                    ImageObjects[0].MarkWidth = xywh[2];
                    ImageObjects[0].MarkHeight = xywh[3];

                    GreasePara.Drop1Roi_L = ImageObjects[0].MarkLeft - DatumPointDrop12_X;
                    GreasePara.Drop1Roi_T = ImageObjects[0].MarkTop - DatumPointDrop12_Y;
                    GreasePara.Drop1Roi_W = ImageObjects[0].MarkWidth;
                    GreasePara.Drop1Roi_H = ImageObjects[0].MarkHeight;

                    using (Mat mat = ImageObjects[0].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(mat, new Rect(GreasePara.Drop1Roi_L + DatumPointDrop12_X, GreasePara.Drop1Roi_T + DatumPointDrop12_Y, GreasePara.Drop1Roi_W, GreasePara.Drop1Roi_H), Scalar.Red, 20, LineTypes.AntiAlias, 0);//1
                        ImageObjects[0].DisplayImage = mat.ToBitmapSource();

                    }

                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("没有基准点！！！\n按下图像处理\n(#｀-_ゝ-)");

            }

        }


        /// <summary>
        /// 第二滴相对位置
        /// </summary>
        private ICommand _ImageOjectDrop2MinorSavePositionCommand;

        public ICommand ImageOjectDrop2MinorSavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop2MinorSavePositionCommand == null)
                {
                    _ImageOjectDrop2MinorSavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop2MinorSavePosition(),
                        param => this.CanImageOjectDrop2MinorSavePosition()
                    );
                }
                return _ImageOjectDrop2MinorSavePositionCommand;
            }
        }
        private bool CanImageOjectDrop2MinorSavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop2MinorSavePosition()
        {
            if (DatumPointDrop12_X != 0 || DatumPointDrop12_Y != 0)
            {
                if (ImageObjects[0].DisplayImage != null)
                {
                    int[] xywh = RoiPointLimit(ImageObjects[0].MarkLeft, ImageObjects[0].MarkTop, ImageObjects[0].MarkWidth, ImageObjects[0].MarkHeight, ImageObjects[0].DisplayImage);
                    ImageObjects[0].MarkLeft = xywh[0];
                    ImageObjects[0].MarkTop = xywh[1];
                    ImageObjects[0].MarkWidth = xywh[2];
                    ImageObjects[0].MarkHeight = xywh[3];

                    GreasePara.Drop2Roi_L = ImageObjects[0].MarkLeft - DatumPointDrop12_X;
                    GreasePara.Drop2Roi_T = ImageObjects[0].MarkTop - DatumPointDrop12_Y;
                    GreasePara.Drop2Roi_W = ImageObjects[0].MarkWidth;
                    GreasePara.Drop2Roi_H = ImageObjects[0].MarkHeight;

                    using (Mat mat = ImageObjects[0].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(mat, new Rect(GreasePara.Drop2Roi_L + DatumPointDrop12_X, GreasePara.Drop2Roi_T + DatumPointDrop12_Y, GreasePara.Drop2Roi_W, GreasePara.Drop2Roi_H), Scalar.Red, 20, LineTypes.AntiAlias, 0);//1
                        ImageObjects[0].DisplayImage = mat.ToBitmapSource();

                    }

                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("没有基准点！！！\n按下图像处理\n(#｀-_ゝ-)");
            }
        }




        /// <summary>
        /// 第一个图，基准点绝对位置保存
        /// </summary>
        private ICommand _ImageOjectDrop12SaveDatumCommand;

        public ICommand ImageOjectDrop12SaveDatumCommand
        {
            get
            {
                if (_ImageOjectDrop12SaveDatumCommand == null)
                {
                    _ImageOjectDrop12SaveDatumCommand = new RelayCommand(
                        param => this.UseImageOjectDrop12SaveDatum(),
                        param => this.CanImageOjectDrop12SaveDatum()
                    );
                }
                return _ImageOjectDrop12SaveDatumCommand;
            }
        }
        private bool CanImageOjectDrop12SaveDatum()
        {
            return true;
        }
        private void UseImageOjectDrop12SaveDatum()
        {
            int[] xywh = RoiPointLimit(ImageObjects[0].MarkLeft, ImageObjects[0].MarkTop, ImageObjects[0].MarkWidth, ImageObjects[0].MarkHeight, ImageObjects[0].DisplayImage);
            GreasePara.DatumPointDrop12_Left = xywh[0];
            GreasePara.DatumPointDrop12_Top = xywh[1];
            GreasePara.DatumPointDrop12_Width = xywh[2];
            GreasePara.DatumPointDrop12_Height = xywh[3];

            //GreasePara.DatumPointDrop12_Left = ImageObjects[0].MarkLeft;
            //GreasePara.DatumPointDrop12_Top = ImageObjects[0].MarkTop;
            //GreasePara.DatumPointDrop12_Width = ImageObjects[0].MarkWidth;
            //GreasePara.DatumPointDrop12_Height = ImageObjects[0].MarkHeight;

            using (Mat mat = ImageObjects[0].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(GreasePara.DatumPointDrop12_Left, GreasePara.DatumPointDrop12_Top, GreasePara.DatumPointDrop12_Width, GreasePara.DatumPointDrop12_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//1
                ImageObjects[0].DisplayImage = mat.ToBitmapSource();
                MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
            }

        }














        #endregion

        #region 第三滴的亮度小图
        /// <summary>
        /// 第三个图的使用图片
        /// </summary>

        private ICommand _ImageOjectDrop3UseImageCommand;

        public ICommand ImageOjectDrop3UseImageCommand
        {
            get
            {
                if (_ImageOjectDrop3UseImageCommand == null)
                {
                    _ImageOjectDrop3UseImageCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3UseImage(),
                        param => this.CanImageOjectDrop3UseImage()
                    );
                }
                return _ImageOjectDrop3UseImageCommand;
            }
        }
        private bool CanImageOjectDrop3UseImage()
        {
            return true;
        }
        private void UseImageOjectDrop3UseImage()
        {
            string imageName = UselocalImageFlie();
            if (imageName != null)
            {
                if (IsRotate)
                {
                    using (Mat MatMain = Rotate_arbitrarily_angle(new Mat(imageName, ImreadModes.Unchanged).ToBitmapSource(), GreasePara.RotateAngle).ToMat())
                    {

                        ImageObjects[1].DisplayImage = MatMain.ToBitmapSource();
                        ImageObjects[1].SourceImage = MatMain.ToBitmapSource();
                    }
                }
                else
                {
                    using (Mat MatMain = new Mat(imageName, ImreadModes.Unchanged))
                    {

                        ImageObjects[1].DisplayImage = MatMain.ToBitmapSource();
                        ImageObjects[1].SourceImage = MatMain.ToBitmapSource();
                    }
                }

            }
        }








        /// <summary>
        /// 原图，第三个图的处理图片
        /// </summary>
        private ICommand _ImageOjectDrop3DatumCommand;

        public ICommand ImageOjectDrop3DatumCommand
        {
            get
            {
                if (_ImageOjectDrop3DatumCommand == null)
                {
                    _ImageOjectDrop3DatumCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3Datum(),
                        param => this.CanImageOjectDrop3Datum()
                    );
                }
                return _ImageOjectDrop3DatumCommand;
            }
        }
        private bool CanImageOjectDrop3Datum()
        {
            return true;
        }
        private void UseImageOjectDrop3Datum()
        {
            if (ImageObjects[1].SourceImage != null)
            {
                ImageObjects[1].SourceImage = ImageObjects[1].DisplayImage.Clone();

                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_Y == 0 && DatumPointDrop3_X == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看原图的Bin，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    using (Mat mat = ImageObjects[1].SourceImage.ToMat())
                    {
                        Cv2.Line(mat, DatumPointDrop3_X - 200, DatumPointDrop3_Y - 0, DatumPointDrop3_X + 200, DatumPointDrop3_Y + 0, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                        Cv2.Line(mat, DatumPointDrop3_X - 0, DatumPointDrop3_Y - 200, DatumPointDrop3_X + 0, DatumPointDrop3_Y + 200, Scalar.Red, 20, LineTypes.AntiAlias, 0);
                        //画定位点范围

                        //画磁石位置
                        Rect magnetroi = new Rect(GreasePara.Magnet1BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet1BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet1BrightnessRoi_Width, GreasePara.Magnet1BrightnessRoi_Height);
                        double magnetbri = getSumGrayBrightness(mat.SubMat(magnetroi).ToBitmapSource());
                        Magnet1Brightness = magnetbri;
                        logger.Info("磁石1总亮度： " + magnetbri);
                        Cv2.Rectangle(mat, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(mat, "Magnet1", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.RandomColor(), 2);
                        //第二个
                        magnetroi = new Rect(GreasePara.Magnet2BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet2BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet2BrightnessRoi_Width, GreasePara.Magnet2BrightnessRoi_Height);
                        magnetbri = getSumGrayBrightness(mat.SubMat(magnetroi).ToBitmapSource());
                        Magnet2Brightness = magnetbri;
                        logger.Info("磁石2总亮度： " + magnetbri);
                        Cv2.Rectangle(mat, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(mat, "Magnet2", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.RandomColor(), 2);
                        //第三个
                        magnetroi = new Rect(GreasePara.Magnet3BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet3BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet3BrightnessRoi_Width, GreasePara.Magnet3BrightnessRoi_Height);
                        magnetbri = getSumGrayBrightness(mat.SubMat(magnetroi).ToBitmapSource());
                        Magnet3Brightness = magnetbri;
                        logger.Info("磁石3总亮度： " + magnetbri);
                        Cv2.Rectangle(mat, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(mat, "Magnet3", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.RandomColor(), 2);
                        //第四个
                        magnetroi = new Rect(GreasePara.Magnet4BrightnessRoi_Left + DatumPointDrop3_X, GreasePara.Magnet4BrightnessRoi_Top + DatumPointDrop3_Y, GreasePara.Magnet4BrightnessRoi_Width, GreasePara.Magnet4BrightnessRoi_Height);
                        magnetbri = getSumGrayBrightness(mat.SubMat(magnetroi).ToBitmapSource());
                        Magnet4Brightness = magnetbri;
                        logger.Info("磁石4总亮度： " + magnetbri);
                        Cv2.Rectangle(mat, magnetroi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(mat, "Magnet4", new Point(magnetroi.X, magnetroi.Y - 15), HersheyFonts.HersheyComplex, 2, Scalar.RandomColor(), 2);

                        //Rect roi = new Rect(GreasePara.SumBrightnessDrop3Roi_Left + DatumPointDrop3_X, GreasePara.SumBrightnessDrop3Roi_Top + DatumPointDrop3_Y, GreasePara.SumBrightnessDrop3Roi_Width, GreasePara.SumBrightnessDrop3Roi_Height);
                        //Cv2.Rectangle(mat, roi, Scalar.GreenYellow, 20, LineTypes.AntiAlias, 0);//3
                        //Cv2.Rectangle(mat, new Rect(GreasePara.DatumPointDrop3_Left, GreasePara.DatumPointDrop3_Top, GreasePara.DatumPointDrop3_Width, GreasePara.DatumPointDrop3_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//1
                        //double sumbri = getSumGrayBrightness(ImageObjects[1].GreaseSourceImage.ToMat().SubMat(roi));


                        ImageObjects[1].DisplayImage = mat.ToBitmapSource();
                    }
                }
            }

        }




        //第一个磁石
        public ICommand Magnet1RoiSaveCommand => new RelayCommand(obj =>
        {
            if (ImageObjects[1].DisplayImage != null)
            {
                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_Y == 0 && DatumPointDrop3_X == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看原图的Bin，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                    ImageObjects[1].MarkLeft = xywh[0];
                    ImageObjects[1].MarkTop = xywh[1];
                    ImageObjects[1].MarkWidth = xywh[2];
                    ImageObjects[1].MarkHeight = xywh[3];
                    //改这里就好了
                    GreasePara.Magnet1BrightnessRoi_Left = ImageObjects[1].MarkLeft - DatumPointDrop3_X;
                    GreasePara.Magnet1BrightnessRoi_Top = ImageObjects[1].MarkTop - DatumPointDrop3_Y;
                    GreasePara.Magnet1BrightnessRoi_Width = ImageObjects[1].MarkWidth;
                    GreasePara.Magnet1BrightnessRoi_Height = ImageObjects[1].MarkHeight;
                    Rect roi = new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
                    using (Mat image = ImageObjects[1].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(image, roi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet1", new Point(xywh[0], xywh[1] - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                        double sumbri = getSumGrayBrightness(image.SubMat(roi).ToBitmapSource());
                        logger.Info("磁石1总亮度： " + sumbri);
                        Magnet1Brightness = sumbri;
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                    }


                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("先按使用图片，吾该！！！\n(#｀-_ゝ-)");

            }
        });
        //第二个磁石
        public ICommand Magnet2RoiSaveCommand => new RelayCommand(obj =>
        {
            if (ImageObjects[1].DisplayImage != null)
            {

                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_Y == 0 && DatumPointDrop3_X == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看原图的Bin，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                    ImageObjects[1].MarkLeft = xywh[0];
                    ImageObjects[1].MarkTop = xywh[1];
                    ImageObjects[1].MarkWidth = xywh[2];
                    ImageObjects[1].MarkHeight = xywh[3];
                    //改这里就好了
                    GreasePara.Magnet2BrightnessRoi_Left = ImageObjects[1].MarkLeft - DatumPointDrop3_X;
                    GreasePara.Magnet2BrightnessRoi_Top = ImageObjects[1].MarkTop - DatumPointDrop3_Y;
                    GreasePara.Magnet2BrightnessRoi_Width = ImageObjects[1].MarkWidth;
                    GreasePara.Magnet2BrightnessRoi_Height = ImageObjects[1].MarkHeight;
                    Rect roi = new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
                    using (Mat image = ImageObjects[1].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(image, roi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet2", new Point(xywh[0], xywh[1] - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                        double sumbri = getSumGrayBrightness(image.SubMat(roi).ToBitmapSource());
                        logger.Info("磁石2总亮度： " + sumbri);
                        Magnet2Brightness = sumbri;
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                    }


                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("先按使用图片，吾该！！！\n(#｀-_ゝ-)");

            }
        });

        //第三个磁石
        public ICommand Magnet3RoiSaveCommand => new RelayCommand(obj =>
        {
            if (ImageObjects[1].DisplayImage != null)
            {
                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_Y == 0 && DatumPointDrop3_X == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看原图的Bin，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                    ImageObjects[1].MarkLeft = xywh[0];
                    ImageObjects[1].MarkTop = xywh[1];
                    ImageObjects[1].MarkWidth = xywh[2];
                    ImageObjects[1].MarkHeight = xywh[3];
                    //改这里就好了
                    GreasePara.Magnet3BrightnessRoi_Left = ImageObjects[1].MarkLeft - DatumPointDrop3_X;
                    GreasePara.Magnet3BrightnessRoi_Top = ImageObjects[1].MarkTop - DatumPointDrop3_Y;
                    GreasePara.Magnet3BrightnessRoi_Width = ImageObjects[1].MarkWidth;
                    GreasePara.Magnet3BrightnessRoi_Height = ImageObjects[1].MarkHeight;
                    Rect roi = new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
                    using (Mat image = ImageObjects[1].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(image, roi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet3", new Point(xywh[0], xywh[1] - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                        double sumbri = getSumGrayBrightness(image.SubMat(roi).ToBitmapSource());
                        logger.Info("磁石3总亮度： " + sumbri);
                        Magnet3Brightness = sumbri;
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                    }
                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("先按使用图片，吾该！！！\n(#｀-_ゝ-)");
            }
        });

        //第三个磁石
        public ICommand Magnet4RoiSaveCommand => new RelayCommand(obj =>
        {
            if (ImageObjects[1].DisplayImage != null)
            {
                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_Y == 0 && DatumPointDrop3_X == 0)
                {
                    MessageBox.Show("没找到基准点！！！\n图片不对？看原图的Bin，去调参数？\n(#｀-_ゝ-)");
                }
                else
                {
                    int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                    ImageObjects[1].MarkLeft = xywh[0];
                    ImageObjects[1].MarkTop = xywh[1];
                    ImageObjects[1].MarkWidth = xywh[2];
                    ImageObjects[1].MarkHeight = xywh[3];
                    //改这里就好了
                    GreasePara.Magnet4BrightnessRoi_Left = ImageObjects[1].MarkLeft - DatumPointDrop3_X;
                    GreasePara.Magnet4BrightnessRoi_Top = ImageObjects[1].MarkTop - DatumPointDrop3_Y;
                    GreasePara.Magnet4BrightnessRoi_Width = ImageObjects[1].MarkWidth;
                    GreasePara.Magnet4BrightnessRoi_Height = ImageObjects[1].MarkHeight;
                    Rect roi = new Rect(xywh[0], xywh[1], xywh[2], xywh[3]);
                    using (Mat image = ImageObjects[1].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(image, roi, Scalar.RandomColor(), 10, LineTypes.AntiAlias, 0);
                        Cv2.PutText(image, "Magnet4", new Point(xywh[0], xywh[1] - 15), HersheyFonts.HersheyComplex, 2, Scalar.Magenta, 2);
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                        double sumbri = getSumGrayBrightness(image.SubMat(roi).ToBitmapSource());
                        logger.Info("磁石4总亮度： " + sumbri);
                        Magnet4Brightness = sumbri;
                        ImageObjects[1].DisplayImage = image.ToBitmapSource();
                    }
                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("先按使用图片，吾该！！！\n(#｀-_ゝ-)");
            }
        });











        private ICommand _ImageOjectDrop3MinorSavePositionCommand;

        public ICommand ImageOjectDrop3MinorSavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop3MinorSavePositionCommand == null)
                {
                    _ImageOjectDrop3MinorSavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3MinorSavePosition(),
                        param => this.CanImageOjectDrop3MinorSavePosition()
                    );
                }
                return _ImageOjectDrop3MinorSavePositionCommand;
            }
        }
        private bool CanImageOjectDrop3MinorSavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop3MinorSavePosition()
        {
            if (DatumPointDrop3_X != 0 && DatumPointDrop3_Y != 0)
            {
                if (ImageObjects[1].DisplayImage != null)
                {
                    int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                    ImageObjects[1].MarkLeft = xywh[0];
                    ImageObjects[1].MarkTop = xywh[1];
                    ImageObjects[1].MarkWidth = xywh[2];
                    ImageObjects[1].MarkHeight = xywh[3];
                    GreasePara.Drop3Roi_L = ImageObjects[1].MarkLeft - DatumPointDrop3_X;
                    GreasePara.Drop3Roi_T = ImageObjects[1].MarkTop - DatumPointDrop3_Y;
                    GreasePara.Drop3Roi_W = ImageObjects[1].MarkWidth;
                    GreasePara.Drop3Roi_H = ImageObjects[1].MarkHeight;

                    using (Mat mat = ImageObjects[1].DisplayImage.ToMat())
                    {
                        Cv2.Rectangle(mat, new Rect(GreasePara.Drop3Roi_L + DatumPointDrop3_X, GreasePara.Drop3Roi_T + DatumPointDrop3_Y, GreasePara.Drop3Roi_W, GreasePara.Drop3Roi_H), Scalar.Red, 20, LineTypes.AntiAlias, 0);//1
                        ImageObjects[1].DisplayImage = mat.ToBitmapSource();

                    }

                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }
            else
            {
                MessageBox.Show("没有基准点！！！\n按下图像处理\n(#｀-_ゝ-)");

            }
        }
















        /// <summary>
        /// 第三个图，基准点保存
        /// </summary>
        private ICommand _ImageOjectDrop3SaveDatumCommand;

        public ICommand ImageOjectDrop3SaveDatumCommand
        {
            get
            {
                if (_ImageOjectDrop3SaveDatumCommand == null)
                {
                    _ImageOjectDrop3SaveDatumCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3SaveDatum(),
                        param => this.CanImageOjectDrop3SaveDatum()
                    );
                }
                return _ImageOjectDrop3SaveDatumCommand;
            }
        }
        private bool CanImageOjectDrop3SaveDatum()
        {
            return true;
        }
        private void UseImageOjectDrop3SaveDatum()
        {

            if (ImageObjects[1].DisplayImage != null)
            {
                int[] xywh = RoiPointLimit(ImageObjects[1].MarkLeft, ImageObjects[1].MarkTop, ImageObjects[1].MarkWidth, ImageObjects[1].MarkHeight, ImageObjects[1].DisplayImage);
                GreasePara.DatumPointDrop3_Left = xywh[0];
                GreasePara.DatumPointDrop3_Top = xywh[1];
                GreasePara.DatumPointDrop3_Width = xywh[2];
                GreasePara.DatumPointDrop3_Height = xywh[3];
                //GreasePara.DatumPointDrop3_Left = ImageObjects[1].MarkLeft;
                //GreasePara.DatumPointDrop3_Top = ImageObjects[1].MarkTop;
                //GreasePara.DatumPointDrop3_Width = ImageObjects[1].MarkWidth;
                //GreasePara.DatumPointDrop3_Height = ImageObjects[1].MarkHeight;
                using (Mat mat = ImageObjects[1].DisplayImage.ToMat())
                {
                    Cv2.Rectangle(mat, new Rect(GreasePara.DatumPointDrop3_Left, GreasePara.DatumPointDrop3_Top, GreasePara.DatumPointDrop3_Width, GreasePara.DatumPointDrop3_Height), Scalar.Blue, 25, LineTypes.AntiAlias, 0);//1
                    ImageObjects[1].DisplayImage = mat.ToBitmapSource();
                    MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");
                }
            }


        }




        /////////////ImageObjects[2]小图






        private ICommand _ImageOjectDrop12DisposeCommand;

        public ICommand ImageOjectDrop12DisposeCommand
        {
            get
            {
                if (_ImageOjectDrop12DisposeCommand == null)
                {
                    _ImageOjectDrop12DisposeCommand = new RelayCommand(
                        param => this.UseImageOjectDrop12Dispose(),
                        param => this.CanImageOjectDrop12Dispose()
                    );
                }
                return _ImageOjectDrop12DisposeCommand;
            }
        }
        private bool CanImageOjectDrop12Dispose()
        {
            return true;
        }
        private void UseImageOjectDrop12Dispose()
        {

            if (ImageObjects[0].SourceImage != null)
            {
                ImageObjects[0].SourceImage = ImageObjects[0].DisplayImage.Clone();
                logger.Info("Drop2StartTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));
                DatumPointDrop12Capture(ImageObjects[0].SourceImage);
                if (DatumPointDrop12_X != 0 && DatumPointDrop12_Y != 0)
                {
                    Drop1GreaseMeasure(ImageObjects[0].SourceImage);
                    double sumbrightness = 0;

                    brightnessJudge(ImageObjects[0].SourceImage, DatumPointDrop12_X, DatumPointDrop12_Y, GreasePara.SumBrightnessDrop12Roi_Left, GreasePara.SumBrightnessDrop12Roi_Top, GreasePara.SumBrightnessDrop12Roi_Width, GreasePara.SumBrightnessDrop12Roi_Height, ref sumbrightness, GreasePara.SumBrightnessLimitDrop12, true);
                    SumBrightnessDrop12 = sumbrightness;
                }
                else
                {
                    MessageBox.Show("没找到定位点哦。\n参数不好？图片弄错？！！！\n(#｀-_ゝ-)");
                }
                logger.Debug("Drop3nakaTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));

                SaveData(@"Data\\", SaveDataType.test);//

                logger.Info("Drop3EndTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));
            }


        }





        private ICommand _ImageOjectDrop1SavePositionCommand;

        public ICommand ImageOjectDrop1SavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop1SavePositionCommand == null)
                {
                    _ImageOjectDrop1SavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop1SavePosition(),
                        param => this.CanImageOjectDrop1SavePosition()
                    );
                }
                return _ImageOjectDrop1SavePositionCommand;
            }
        }
        private bool CanImageOjectDrop1SavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop1SavePosition()
        {
            //GreasePara.Drop1JudgePosition_Left = ImageObjects[2].MarkLeft;
            //GreasePara.Drop1JudgePosition_Top = ImageObjects[2].MarkTop;
            //GreasePara.Drop1JudgePosition_Width = ImageObjects[2].MarkWidth;
            //GreasePara.Drop1JudgePosition_Height = ImageObjects[2].MarkHeight;
            int[] xywh = RoiPointLimit(ImageObjects[2].MarkLeft, ImageObjects[2].MarkTop, ImageObjects[2].MarkWidth, ImageObjects[2].MarkHeight, ImageObjects[2].DisplayImage);

            GreasePara.Drop1JudgePosition_Left = xywh[0];
            GreasePara.Drop1JudgePosition_Top = xywh[1];
            GreasePara.Drop1JudgePosition_Width = xywh[2];
            GreasePara.Drop1JudgePosition_Height = xywh[3];



            using (Mat mat = ImageObjects[2].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(xywh[0], xywh[1], xywh[2], xywh[3]), Scalar.RandomColor(), 1, LineTypes.AntiAlias, 0);//1
                ImageObjects[2].DisplayImage = mat.ToBitmapSource();
            }
            MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");




        }


        /////////////ImageObjects[3]小图

        private ICommand _ImageOjectDrop2SavePositionCommand;

        public ICommand ImageOjectDrop2SavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop2SavePositionCommand == null)
                {
                    _ImageOjectDrop2SavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop2SavePosition(),
                        param => this.CanImageOjectDrop2SavePosition()
                    );
                }
                return _ImageOjectDrop2SavePositionCommand;
            }
        }
        private bool CanImageOjectDrop2SavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop2SavePosition()
        {
            //GreasePara.Drop2JudgePosition_Left = ImageObjects[3].MarkLeft;
            //GreasePara.Drop2JudgePosition_Top = ImageObjects[3].MarkTop;
            //GreasePara.Drop2JudgePosition_Width = ImageObjects[3].MarkWidth;
            //GreasePara.Drop2JudgePosition_Height = ImageObjects[3].MarkHeight;

            int[] xywh = RoiPointLimit(ImageObjects[3].MarkLeft, ImageObjects[3].MarkTop, ImageObjects[3].MarkWidth, ImageObjects[3].MarkHeight, ImageObjects[3].DisplayImage);
            GreasePara.Drop2JudgePosition_Left = xywh[0];
            GreasePara.Drop2JudgePosition_Top = xywh[1];
            GreasePara.Drop2JudgePosition_Width = xywh[2];
            GreasePara.Drop2JudgePosition_Height = xywh[3];


            using (Mat mat = ImageObjects[3].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(GreasePara.Drop2JudgePosition_Left, GreasePara.Drop2JudgePosition_Top, GreasePara.Drop2JudgePosition_Width, GreasePara.Drop2JudgePosition_Height), Scalar.RandomColor(), 1, LineTypes.AntiAlias, 0);//1
                ImageObjects[3].DisplayImage = mat.ToBitmapSource();
            }
            MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");

        }




        private ICommand _ImageOjectDrop3DisposeCommand;

        public ICommand ImageOjectDrop3DisposeCommand
        {
            get
            {
                if (_ImageOjectDrop3DisposeCommand == null)
                {
                    _ImageOjectDrop3DisposeCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3Dispose(),
                        param => this.CanImageOjectDrop3Dispose()
                    );
                }
                return _ImageOjectDrop3DisposeCommand;
            }
        }
        private bool CanImageOjectDrop3Dispose()
        {
            return true;
        }
        private void UseImageOjectDrop3Dispose()
        {

            if (ImageObjects[1].SourceImage != null)
            {
                ImageObjects[1].SourceImage = ImageObjects[1].DisplayImage.Clone();
                logger.Info("Drop3StartTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));
                DatumPointDrop3Capture(ImageObjects[1].SourceImage);
                if (DatumPointDrop3_X != 0 || DatumPointDrop3_Y != 0)
                {
                    Drop3GreaseMeasure(ImageObjects[1].SourceImage);
                    //double sumbrightness = 0;
                    //isOpOn(ImageObjects[1].GreaseSourceImage, DatumPointDrop3_X, DatumPointDrop3_Y, GreasePara.SumBrightnessDrop3Roi_Left, GreasePara.SumBrightnessDrop3Roi_Top, GreasePara.SumBrightnessDrop3Roi_Width, GreasePara.SumBrightnessDrop3Roi_Height, ref sumbrightness, GreasePara.SumBrightnessLimitDrop3);
                    //SumBrightnessDrop3 = sumbrightness;
                }
                else
                {
                    MessageBox.Show("没找到定位点哦。\n参数不好？图片弄错？！！！\n(#｀-_ゝ-)");


                }
                logger.Debug("Drop3nakaTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));

                SaveData(@"Data\\", SaveDataType.test);//

                logger.Info("Drop3EndTime:_" + DateTime.Now.ToString("yyyy_MM_dd-H_mm_ss_FFF"));
            }



        }

        private ICommand _ImageOjectDrop3SavePositionCommand;

        public ICommand ImageOjectDrop3SavePositionCommand
        {
            get
            {
                if (_ImageOjectDrop3SavePositionCommand == null)
                {
                    _ImageOjectDrop3SavePositionCommand = new RelayCommand(
                        param => this.UseImageOjectDrop3SavePosition(),
                        param => this.CanImageOjectDrop3SavePosition()
                    );
                }
                return _ImageOjectDrop3SavePositionCommand;
            }
        }
        private bool CanImageOjectDrop3SavePosition()
        {
            return true;
        }
        private void UseImageOjectDrop3SavePosition()
        {
            //GreasePara.Drop3JudgePosition_Left = ImageObjects[4].MarkLeft;
            //GreasePara.Drop3JudgePosition_Top = ImageObjects[4].MarkTop;
            //GreasePara.Drop3JudgePosition_Width = ImageObjects[4].MarkWidth;
            //GreasePara.Drop3JudgePosition_Height = ImageObjects[4].MarkHeight;

            int[] xywh = RoiPointLimit(ImageObjects[4].MarkLeft, ImageObjects[4].MarkTop, ImageObjects[4].MarkWidth, ImageObjects[4].MarkHeight, ImageObjects[4].DisplayImage);
            GreasePara.Drop3JudgePosition_Left = xywh[0];
            GreasePara.Drop3JudgePosition_Top = xywh[1];
            GreasePara.Drop3JudgePosition_Width = xywh[2];
            GreasePara.Drop3JudgePosition_Height = xywh[3];

            using (Mat mat = ImageObjects[4].DisplayImage.ToMat())
            {
                Cv2.Rectangle(mat, new Rect(GreasePara.Drop3JudgePosition_Left, GreasePara.Drop3JudgePosition_Top, GreasePara.Drop3JudgePosition_Width, GreasePara.Drop3JudgePosition_Height), Scalar.RandomColor(), 1, LineTypes.AntiAlias, 0);//1
                ImageObjects[4].DisplayImage = mat.ToBitmapSource();
            }
            MessageBox.Show("记得按一下存！！！\n(#｀-_ゝ-)");

        }






        //private ICommand _StartProcessCommand;

        //public ICommand StartProcessCommand
        //{
        //    get
        //    {
        //        if (_StartProcessCommand == null)
        //        {
        //            _StartProcessCommand = new RelayCommand(
        //                param => this.UseStartProcess(),
        //                param => this.CanStartProcess()
        //            );
        //        }
        //        return _StartProcessCommand;
        //    }
        //}
        //private bool CanStartProcess()
        //{
        //    return !GreasePara.AutoStart;
        //}
        //private void UseStartProcess()
        //{
        //    GreasePara.AutoStart = true;
        //    Task.Run(() => GreaseStart());
        //    Task.Run(() => GreaseStartDrop12());



        //    SaveGreaseMorParaToJsonData();
        //}


        //测试线程
        public ICommand Test1Command => new RelayCommand(obj =>
        {
            string read_test = Read_txt(GloPara.OPIDFile);
            logger.Info(read_test);

            if (read_test == null || read_test == "")//可以
            {
                logger.Info("kkkkk");
            }
            //foreach (Thread i in thread)
            //{
            //    i.Resume();
            //}

        });

        public ICommand Test2Command => new RelayCommand(obj =>
        {


            Write_txt(GloPara.OPIDFile, "");

            //foreach (Thread i in thread)
            //{
            //    i.Suspend();
            //}

        });






        #endregion




        public ICommand GreaseContinuousBadCloseCommand => new RelayCommand(obj =>
        {
            GreaseContinuousBadInit();
            GreaseContinuousBad = suspendRequested = false;


        });










        ///// <summary>
        ///// 截图用
        ///// </summary>
        ///// <param name="InputPath"></param>
        ///// <param name="OutputPath"></param>
        //void ImagesCut(string InputPath, string OutputPath)
        //{
        //    try
        //    {
        //        Create_dir(OutputPath);
        //        if (Directory.Exists(InputPath))//文件夹 Directory ；文件 File.Exists。
        //        {
        //            DirectoryInfo theFolder = new DirectoryInfo(InputPath);
        //            FileInfo[] fileInfo = theFolder.GetFiles("*.*");//*.PNG为限定PNG；*.*全部；Window不分大小写的
        //            foreach (FileInfo NextFile in fileInfo) //遍历文件夹里的文件
        //            {
        //                using (Mat image = new Mat(NextFile.FullName))
        //                {
        //                    //NextFile.Name.Remove(NextFile.Name.Length - 4) 去掉后缀
        //                    //NextFile.Name为文件名；NextFile.FullName文件地址（带文件名的）。
        //                    //Mat minibin = new Mat();

        //                    ////VerticalEdgeSearch_tt(LocalImage.ToMat(), LocalImage.ToMat(), GreasePara.Drop2VerTh, GreasePara.Drop2MinorRoi_T, GreasePara.Drop2MinorRoi_W, image, out minibin, 60);
        //                    //if (GloPara.DebugMode)
        //                    //{
        //                    //    Drop2MinorCanvas = image.ToBitmapSource();
        //                    //    Drop2MinorBin = minibin.ToBitmapSource();


        //                    //}


        //                    //minibin.Dispose();

        //                }
        //            }

        //            Task.Run(() => MessageBox.Show("截图搞完啦！！！\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "截图", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));


        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("ImagesCut|  " + ex.Message);
        //        Task.Run(() => MessageBox.Show("出错啦！！！\n不会搞就不要搞啦...\n╮(╯▽╰)╭\t╮(╯▽╰)\n(￣_,￣ )", "截图", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly));

        //    }
        //}










        //private void VerticalEdgeSearch_ttttt(Mat mat, int th, Mat canvas, int w)
        //{
        //    try
        //    {

        //        using (Mat avg_h = new Mat())
        //        using (Mat org = mat.Channels() == 3 ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat)
        //        using (Mat gray_roi = org.SubMat(new Rect(org.Width - w, 0, w, org.Height)))
        //        {
        //            Cv2.Reduce(gray_roi, avg_h, ReduceDimension.Column, ReduceTypes.Avg, -1);//行平均，输出列 ,
        //            float mean_h = (float)avg_h.Mean();
        //            avg_h.ConvertTo(avg_h, MatType.CV_32FC1);
        //            using (Mat projection_v = avg_h / mean_h)
        //            {
        //                VerticalUpperEdgeSearch_tt(projection_v, th, canvas);


        //                canvas.Line(new Point(org.Width - w, 0), new Point(org.Width - w, 6660), Scalar.GreenYellow, 2);//竖线


        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("VerticalEdgeSearch_ttttt|" + ex.Message);
        //    }
        //}

        //private void VerticalUpperEdgeSearch_tt(Mat vertical_proj, float th, Mat canvas)
        //{
        //    int maxw = 60;//放大倍数？
        //                  // bottom --> top
        //    float leastval = 9999;
        //    int leastr = 9999;

        //    for (int r = vertical_proj.Rows - 1; r >= 0; r--)
        //    {
        //        float val = maxw * vertical_proj.At<float>(r, 0);
        //        //logger.Debug(string.Format("vertical proj|{0}|{1}", r, val));
        //        if (GloPara.DebugMode)
        //        {
        //            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.Red);//一条一条线地画出来
        //        }
        //        canvas.Line(new Point(th, 0), new Point(th, 6660), Scalar.Blue, 2);//竖线


        //        if (val > th)
        //        {


        //            if (val < leastval)
        //            {
        //                canvas.Line(new Point(0, leastr), new Point((int)leastval, leastr), Scalar.Red);//一条一条线地画出来
        //                Cv2.PutText(canvas, leastval.ToString(), new Point(canvas.Width - 100, 20), HersheyFonts.HersheyComplex, 1, Scalar.Red, 1);

        //            }
        //            canvas.Line(new Point(0, r), new Point((int)val, r), Scalar.GreenYellow);//一条一条线地画出来
        //            leastval = val;
        //            leastr = r;

        //            //return true;
        //        }

        //    }
        //}





        #endregion


    }
}
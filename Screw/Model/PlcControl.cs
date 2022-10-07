using ModbusLib;
using ModbusLib.Protocols;
using NLog;
using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Screw.Model
{
    public class PlcControl : NotifyPropertyChangedBase
    {
        //网上的单例模式 C#特有
        //private PlcControl() { }
        //public static readonly PlcControl GetInstance = new PlcControl();


        ///// <summary>
        ///// DVDLD的  第一种，没有考虑线程安全
        ///// </summary>
        private static PlcControl staticInstance = null;

        public static PlcControl GetInstance()
        {
            if (staticInstance == null)
            {
                staticInstance = new PlcControl();
            }

            return staticInstance;
        }
        ////方法2
        //private static PlcControl instance = new PlcControl();
        //private PlcControl() { }
        //public static PlcControl GetInstance()
        //{
        //    return instance;
        //}
        //方法3
        //private static PlcControl instance = null;
        //private static object obj = new object();
        //public static PlcControl GetInstance()
        //{

        //    if (instance == null)
        //    {

        //        lock (obj)
        //        {
        //            instance = new PlcControl();
        //        }
        //    }
        //    return instance;

        //}




        //public DateTime tag { get; set; }

        //public PlcControl()
        //{
        //    tag = DateTime.Now;
        //}


        #region Field
        NLog.Logger logger = LogManager.GetCurrentClassLogger();

        // 实例化Modbus客户端
        private ModbusClient _driver;
        private ICommClient _portClient;
        private SerialPort _uart;
        private int _transactionId;
        #endregion

        #region Operations

        #region 连接/断开


        PlcControl()
        {
            //stopBits = StopBits.One;
            //parity = Parity.None;
            //dataBits = 8;
            //_uart = new SerialPort();//第二个参数38400为波特率。

        }
        ~PlcControl()
        {
            //_uart.Dispose();
            //_driver = null;
            //_uart = null;

        }
        /// <summary>
        /// 连接ModBus         RTU模式
        /// </summary>
        /// <param name="portName">串口端口号</param>
        /// <param name="slaveId">从机ID</param>
        /// <returns>连接结果</returns>
        public bool Connect()
        {
            try
            {
                //testBool = true;

                _uart = new SerialPort(Port, Baudrate, Parity.None, 8, StopBits.One);//第二个参数38400为波特率。
                //_uart.PortName = Port;
                //_uart.BaudRate = Baudrate;
                //_uart.Parity = parity;
                //_uart.StopBits = stopBits;
                //_uart.DataBits = dataBits;

                _uart.Open();
                _portClient = _uart.GetClient();
                _driver = new ModbusClient(new ModbusRtuCodec()) { Address = SlaveID };
                _driver.OutgoingData += DriverOutgoingData;
                _driver.IncommingData += DriverIncommingData;  //写寄存器后会运行

                logger.Trace("plc INIT!!!");

                if (!_uart.IsOpen)
                {
                    logger.Error("Failed to open PLC Com port: " + Port);
                    PlcIsConnected = _uart.IsOpen;
                    return false;
                }
                else
                {
                    logger.Trace("PLC connected");
                    PlcIsConnected = _uart.IsOpen;

                    return true;
                }
            }
            catch (Exception ex)
            {
                PlcIsConnected = _uart.IsOpen;

                logger.Error("Open modbus error:" + ex.Message);
                return false;
            }
        }



        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>结果</returns>
        public bool Disconnect()
        {

            try
            {
                if (_uart != null)
                {
                    _uart.Close();
                    _uart.Dispose();
                    _uart = null;
                }

                _portClient = null;
                _driver = null;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Closing modbus error:" + ex.Message);
                return false;
            }

        }

        #endregion

        #region 事件响应函数

        /// <summary>
        /// Modbus接收数据事件响应函数
        /// </summary>
        /// <param name="data"></param>
        protected void DriverIncommingData(byte[] data)
        {
            var hex = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
                hex.AppendFormat("{0:x2} ", b);


            //MessageBox.Show(String.Format("Received: {0}", hex));   //写完寄存器会弹出
        }

        /// <summary>
        /// Modbus发送数据事件响应函数
        /// </summary>
        /// <param name="data"></param>
        protected void DriverOutgoingData(byte[] data)
        {
            var hex = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
                hex.AppendFormat("{0:x2} ", b);

        }

        #endregion

        #region 寄存器读取函数

        /// <summary>
        /// 读取从机寄存器
        /// </summary>
        /// <param name="function">功能代码</param>flir 工业相机
        /// <param name="startAddress">起始地址</param>
        /// <param name="dataLength">读取寄存器长度</param>
        /// <param name="data">返回数据</param>
        /// <returns>操作结果</returns>
        private bool ExecuteReadCommand(byte function, ushort startAddress, ushort dataLength, ref ushort[] data)
        {

            try
            {
                var command = new ModbusCommand(function) { Offset = startAddress, Count = dataLength, TransId = _transactionId++ };
                var result = _driver.ExecuteGeneric(_portClient, command);

                // Log status and data
                if (command.Data != null)
                {
                    //  MessageBox.Show("Read Data:" + ushort2String(command.Data));///////读出后弹窗读出的数据
                }
                else
                {
                    logger.Error("result.Status:" + result.Status.ToString());//////没有读出数据弹窗
                }

                // copy received data
                if (result.Status == CommResponse.Ack)
                {
                    command.Data.CopyTo(data, 0);
                    return true;
                }
                else
                {
                    logger.Error(String.Format("PLC read failed|Address:{0} Error code:{1}", startAddress, result.Status));//   logger.Error     logger.Debug

                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(String.Format("PLC Read error|Add:{0} Function code:{1} ER:{2}", startAddress, function, ex.Message));
                PlcIsConnected = _uart.IsOpen;

                return false;
            }


            // Error返回

        }

        /// <summary>
        /// ushort data to 4 digits HEX format string 十六进制传送
        /// </summary>
        /// <param name="orgBytes"></param>
        /// <returns></returns>
        private string ushort2String(ushort[] ushortArray)
        {
            string str = "";
            if (ushortArray.Length > 0)
            {
                foreach (ushort us in ushortArray)
                {
                    str += us.ToString("X4");
                }
            }
            return str;
        }

        ///<summary>
        /// 将byte数组转浮点数（符合IEEE-754标准（32））
        /// </summary>
        /// <paramname="data">byte数组</param>shuz
        /// <returns>浮点数值</returns>
        //private float bytes2Float(byte[] data)
        //{
        //    if (data.Length != 4)
        //    {
        //        //throw new NotEnoughDataInBufferException(data.length(), 8);
        //        throw (new ApplicationException("缓存中的数据不完整，需要长度为4的byte数组。"));//////////没有必要了
        //    }
        //    else
        //    {
        //        byte[] intBuffer = new byte[4];
        //        // 将16进制串按字节逆序化（一个字节2个ASCII码）
        //        for (int i = 0; i < 4; i++)
        //        {
        //            intBuffer[i] = data[3 - i];
        //        }
        //        return BitConverter.ToSingle(intBuffer, 0);
        //    }
        //}

        #endregion

        #region 写寄存器函数

        /// <summary>
        /// Execute PLC Modbus write command
        /// </summary>  可以连续写几个地址
        /// <param name="function"></param>
        /// <param name="StartAddress" 需要十进制></param>
        /// <param name="DataLength" 1则写一个地址，2则写两个连续的地址，写多个寄存器需要注意function  ></param>
        /// <param name="value" 写入值范围0—2^16></param>
        public bool ExecuteWriteCommand(byte function, int StartAddress, int DataLength, ushort[] value)
        {

            try
            {
                var command = new ModbusCommand(function)
                {
                    Offset = StartAddress,
                    Count = DataLength,
                    TransId = _transactionId++,
                    Data = value
                };

                var result = _driver.ExecuteGeneric(_portClient, command);
                if (result.Status == CommResponse.Ack)
                {
                    logger.Trace(String.Format("PLC write success|Address:{0} Value:{1} Function code:{2}", StartAddress, ushort2String(value), function));
                    return true;
                }
                else
                {
                    logger.Error(String.Format("PLC write failed|Address:{0} Value:{1} Error code:{2}", StartAddress, ushort2String(value), result.Status));
                    return false;
                }
            }
            catch (Exception ex)
            {
                PlcIsConnected = _uart.IsOpen;
                logger.Error(String.Format("PLC Write failed|Address:{0} Function code:{1} ER:{2}", StartAddress, function, ex.Message));
                return false;
            }
        }



        #endregion

        #endregion

        #region Method
        /// <summary>
        /// 连接
        /// </summary>
        public void BtnConnect()
        {
            // byte slaveId = 2;   // RTU模式（串口通信方式）下SlaveID无效（仅在TCP/IP模式下使用）   slaveId=0時全局广播   DVDLD也是1

            if (Connect())
            {
                logger.Trace("PLC连接成功!");
                PlcIsConnected = _uart.IsOpen;

            }
            else
            {
                logger.Error("PLC连接失败!");
                PlcIsConnected = _uart.IsOpen;
            }
        }
        /// <summary>
        /// 断开连接
        /// </summary>
        public void BtnDisconnect()
        {
            Disconnect();
            logger.Trace("已断开!");

        }

        /// <summary>
        /// 重复写
        /// </summary>
        /// <param name="function"></param>
        /// <param name="StartAddress"></param>
        /// <param name="DataLength"></param>
        /// <param name="value"></param>
        public void PlcWrite(byte function, int StartAddress, int DataLength, ushort[] value)
        {
            int i = 0;
            while (!ExecuteWriteCommand(function, StartAddress, DataLength, value))
            {
                i++;
                ExecuteWriteCommand(function, StartAddress, DataLength, value);
                logger.Info("PlcWriteRepeat：StartAddress:{0} ,value:{1}", StartAddress, value);
                if (i >= 10)
                {
                    logger.Error("PlcWrite-次数：{0}", i);

                    break;
                }
            }

        }



        public void PlcRead(byte function, ushort StartAddress, ushort DataLength, ref ushort[] value)
        {
            int i = 0;
            // _driver.Address = SlaveID;

            while (!ExecuteReadCommand(function, StartAddress, DataLength, ref value))
            {
                i++;
                ExecuteReadCommand(function, StartAddress, DataLength, ref value);
                logger.Info("PlcReadRepeat：StartAddress:{0} ,value:{1}", StartAddress, value);
                if (i >= 10)
                {
                    logger.Error("PlcRead-次数：{0}", i);
                    break;
                }
            }
        }




        #endregion

        #region Property
        private bool _PlcIsConnected;//是否连接
        [JsonIgnore]
        public bool PlcIsConnected
        {
            get { return _PlcIsConnected; }
            set { if (_PlcIsConnected != value) { _PlcIsConnected = value; RaisePropertyChanged("PlcIsConnected"); } }
        }


        private int _Baudrate;//测试波特率

        public int Baudrate
        {
            get { return _Baudrate; }
            set { if (_Baudrate != value) { _Baudrate = value; RaisePropertyChanged("Baudrate"); } }
        }


        private byte _SlaveID;//测试_TestSlaveID

        public byte SlaveID
        {
            get { return _SlaveID; }
            set { if (_SlaveID != value) { _SlaveID = value; RaisePropertyChanged("SlaveID"); } }
        }


        private StopBits _stopBits;
        public StopBits stopBits
        {
            get { return _stopBits; }
            set { if (_stopBits != value) { _stopBits = value; RaisePropertyChanged("stopBits"); } }
        }
        private ushort _dataBits;
        public ushort dataBits
        {
            get { return _dataBits; }
            set { if (_dataBits != value) { _dataBits = value; RaisePropertyChanged("dataBits"); } }
        }

        private Parity _parity;
        public Parity parity
        {
            get { return _parity; }
            set { if (_parity != value) { _parity = value; RaisePropertyChanged("parity"); } }
        }


        private uint _NextVal;//测试读出
        [JsonIgnore]
        public uint NextVal
        {
            get { return _NextVal; }
            set { if (_NextVal != value) { _NextVal = value; RaisePropertyChanged("NextVal"); } }
        }
        private uint _CurrentVal;//测试读出
        [JsonIgnore]
        public uint CurrentVal
        {
            get { return _CurrentVal; }
            set { if (_CurrentVal != value) { _CurrentVal = value; RaisePropertyChanged("CurrentVal"); } }
        }

        private ushort _Add;//测试写入地址
        public ushort Add
        {
            get { return _Add; }
            set { if (_Add != value) { _Add = value; RaisePropertyChanged("Add"); } }
        }
        private ushort _Val;//测试写入值
        public ushort Val
        {
            get { return _Val; }
            set { if (_Val != value) { _Val = value; RaisePropertyChanged("Val"); } }
        }
        private string _Port;//端口
        public string Port
        {
            get { return _Port; }
            set { if (_Port != value) { _Port = value; RaisePropertyChanged("Port"); } }
        }

        #endregion

        #region Command

        /// <summary>
        /// 连接
        /// </summary>
        private ICommand _BtnConnectCommand;
        [JsonIgnore]
        public ICommand BtnConnectCommand
        {
            get
            {
                if (_BtnConnectCommand == null)
                {
                    _BtnConnectCommand = new RelayCommand(
                        param => this.UseConnect(),
                        param => this.CanConnect()
                    );
                }
                return _BtnConnectCommand;
            }
        }
        private bool CanConnect()
        {
            return !PlcIsConnected;
        }
        private void UseConnect()
        {
            BtnConnect();

        }
        /// <summary>
        /// 断开
        /// </summary>
        private ICommand _BtnDisconnectCommand;
        [JsonIgnore]
        public ICommand BtnDisconnectCommand
        {
            get
            {
                if (_BtnDisconnectCommand == null)
                {
                    _BtnDisconnectCommand = new RelayCommand(
                        param => this.UseDisconnect(),
                        param => this.CanDisconnect()
                    );
                }
                return _BtnDisconnectCommand;
            }
        }
        private bool CanDisconnect()
        {
            return PlcIsConnected;
        }
        private void UseDisconnect()
        {
            BtnDisconnect();
            PlcIsConnected = !PlcIsConnected;
        }

        /// <summary>
        /// 读取
        /// </summary>
        private ICommand _BtnReadCommand;//测试读
        [JsonIgnore]
        public ICommand BtnReadCommand
        {
            get
            {
                if (_BtnReadCommand == null)
                {
                    _BtnReadCommand = new RelayCommand(
                        param => this.UseBtnRead(),
                        param => this.CanBtnRead()
                    );
                }
                return _BtnReadCommand;
            }
        }
        private bool CanBtnRead()
        {
            return PlcIsConnected;
        }
        private void UseBtnRead()
        {
            Task.Run(() => readcommand());



        }
        private void readcommand()
        {
            ushort[] value = new ushort[2];
            value[0] = 0;
            value[1] = 0;
            if (ExecuteReadCommand(ModbusCommand.FuncReadMultipleRegisters, Add, 2, ref value))
            {
                NextVal = value[1];
                CurrentVal = value[0];

            }
        }


        /// <summary>
        /// 写寄存器
        /// </summary>
        private ICommand _BtnWriteCommand;//测试写
        [JsonIgnore]
        public ICommand BtnWriteCommand
        {
            get
            {
                if (_BtnWriteCommand == null)
                {
                    _BtnWriteCommand = new RelayCommand(
                        param => this.UseBtnWrite(),
                        param => this.CanBtnWrite()
                    );
                }
                return _BtnWriteCommand;
            }
        }
        private bool CanBtnWrite()
        {
            return PlcIsConnected;
        }
        private void UseBtnWrite()
        {
            Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, Add, 1, new ushort[] { Val }));

        }



        /// <summary>
        /// 写寄存器
        /// </summary>
        private ICommand _BtnWrite10To50Command;//测试写
        [JsonIgnore]
        public ICommand BtnWrite10To50Command
        {
            get
            {
                if (_BtnWrite10To50Command == null)
                {
                    _BtnWrite10To50Command = new RelayCommand(
                        param => this.UseBtnWrite10To50(),
                        param => this.CanBtnWrite10To50()
                    );
                }
                return _BtnWrite10To50Command;
            }
        }
        private bool CanBtnWrite10To50()
        {
            return PlcIsConnected;
        }
        private void UseBtnWrite10To50()
        {
            Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 10 }));

        }

        private ICommand _BtnWrite20To47Command;//测试写
        [JsonIgnore]
        public ICommand BtnWrite20To47Command
        {
            get
            {
                if (_BtnWrite20To47Command == null)
                {
                    _BtnWrite20To47Command = new RelayCommand(
                        param => this.UseBtnWrite20To47(),
                        param => this.CanBtnWrite20To47()
                    );
                }
                return _BtnWrite20To47Command;
            }
        }
        private bool CanBtnWrite20To47()
        {
            return PlcIsConnected;
        }
        private void UseBtnWrite20To47()
        {
            Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 20 }));

        }

        private ICommand _BtnWrite10To44Command;//测试写
        [JsonIgnore]
        public ICommand BtnWrite10To44Command
        {
            get
            {
                if (_BtnWrite10To44Command == null)
                {
                    _BtnWrite10To44Command = new RelayCommand(
                        param => this.UseBtnWrite10To44(),
                        param => this.CanBtnWrite10To44()
                    );
                }
                return _BtnWrite10To44Command;
            }
        }
        private bool CanBtnWrite10To44()
        {
            return PlcIsConnected;
        }
        private void UseBtnWrite10To44()
        {
            Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 10 }));

        }

        //public ICommand BtnWrite10To50Command => new RelayCommand(obj => 
        //{
        //    Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 50, 1, new ushort[] { 10 }));

        //});
        //public ICommand BtnWrite20To47Command => new RelayCommand(obj =>
        //{
        //    Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 47, 1, new ushort[] { 20 }));

        //});

        //public ICommand BtnWrite10To44Command => new RelayCommand(obj =>
        //{
        //    Task.Run(() => PlcWrite(ModbusCommand.FuncWriteSingleRegister, 44, 1, new ushort[] { 10 }));

        //});





        #endregion


    }
}

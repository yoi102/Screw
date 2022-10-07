using NLog;
using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace Screw.Model
{
    class SerialControl : NotifyPropertyChangedBase
    {
        private AutoResetEvent _waitForResponse;
        private const double ADDITIONAL_WAIT_TIME_BEFORE_SEND = 5.0;
        private const double ADDITIONAL_WAIT_TIME_AFTER_SEND = 50.0;
        private const double REQUIRED_TRANSMISSION_TIME_FOR_BYTE = 9.1525;
        SerialPort mySerialPort;//声明串口类
        //public Socket newclient;
        NLog.Logger logger = LogManager.GetCurrentClassLogger();



        public SerialControl()
        {
            mySerialPort = new SerialPort();
            _waitForResponse = new AutoResetEvent(false);
            //comPort = "COM7";
            //baudRate = 38400;
            //stopBits = StopBits.One;
            //parity = Parity.None;
            //dataBits = 8;
            

        }
        ~SerialControl()
        {
            mySerialPort.Dispose();
            _waitForResponse.Dispose();
            _waitForResponse = null;
            mySerialPort = null;


        }

        private string _comPort;
        public string comPort
        {
            get { return _comPort; }
            set { if (_comPort != value) { _comPort = value; RaisePropertyChanged("comPort"); } }
        }

        private int _baudRate;
        public int baudRate
        {
            get { return _baudRate; }
            set { if (_baudRate != value) { _baudRate = value; RaisePropertyChanged("baudRate"); } }
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

        private string _recvData;
        public string recvData
        {
            get { return _recvData; }
            set { if (_recvData != value) { _recvData = value; RaisePropertyChanged("recvData"); } }
        }

        private string _sendData;
        public string sendData
        {
            get { return _sendData; }
            set { if (_sendData != value) { _sendData = value; RaisePropertyChanged("_sendData"); } }
        }

        private bool _serialIsConnect;
        [JsonIgnore]
        public bool serialIsConnect
        {
            get { return _serialIsConnect; }
            set { if (_serialIsConnect != value) { _serialIsConnect = value; RaisePropertyChanged("serialIsConnect"); } }
        }
        /// <summary>
        /// Serialport Received buffer
        /// </summary>
        private string _ReceivedBuff;
        public string ReceivedBuff
        {
            get => _ReceivedBuff;
            set => _ReceivedBuff = value;
        }

        //mySerialPort.PortName = comPort.Trim();//串口名
        //mySerialPort.BaudRate = Convert.ToInt32(baudRate.Trim());//波特率

        //switch (Convert.ToSingle(stopBits.Trim()))//停止位
        //{
        //    case 0: mySerialPort.StopBits = StopBits.None; break;
        //    case 1.5f: mySerialPort.StopBits = StopBits.OnePointFive; break;
        //    case 1: mySerialPort.StopBits = StopBits.One; break;
        //    case 2: mySerialPort.StopBits = StopBits.Two; break;
        //    default: mySerialPort.StopBits = StopBits.One; break;
        //}
        //mySerialPort.DataBits = Convert.ToInt16(dataBits.Trim());//数据位

        //switch (paritv.Trim())//校验位
        //{
        //    case "无": mySerialPort.Parity = Parity.None; break;
        //    case "奇校验": mySerialPort.Parity = Parity.Odd; ; break;
        //    case "偶校验": mySerialPort.Parity = Parity.Even; break;
        //    default: mySerialPort.Parity = Parity.None; break;
        //}
     


        public bool portInit()//设置串口属性
        {
            try
            {
                serialIsConnect = false;
                mySerialPort.PortName = comPort.Trim();//串口名
                mySerialPort.StopBits = stopBits;
                mySerialPort.Parity = parity;
                mySerialPort.BaudRate = baudRate;
                mySerialPort.DataBits = dataBits;
                mySerialPort.ReadTimeout = -1;//设置超时读取时间
                mySerialPort.RtsEnable = true;


                recvData = "0";
                //baudRate = baudRate1.Trim().Substring(10);
                if (mySerialPort.IsOpen)
                {
                    mySerialPort.Close();
                }
                mySerialPort.Open();
                ReadrecvData();
                serialIsConnect = mySerialPort.IsOpen;
                logger.Info("SerialportInit");
                //定义DataReceived事件，当串口收到数据后触发事件
                // mySerialPort.DataReceived += new SerialDataReceivedEventHandler(sp_DataReceived);
                return true;

            }
            catch (Exception ex)
            {
                logger.Error(String.Format("Serial Port Init error| ", ex.Message));
                serialIsConnect = mySerialPort.IsOpen;
                return false;
            }
        }

        private void sp_DataReceived(object sender, SerialDataReceivedEventArgs e)//串口接收数据事件
        {
            //System.Threading.Thread.Sleep(100);//延时100ms等待接收数据
            System.Threading.Thread.Sleep(100);//延时100ms等待接收数据

            recvData = mySerialPort.ReadLine();
            mySerialPort.DiscardInBuffer();

        }

        public void sp_sendData()
        {
            try
            {
                if (mySerialPort.IsOpen)
                {
                    ReceivedBuff = "";

                    mySerialPort.DataReceived += (sender, args) =>
                    {
                        byte[] receivedBytes = new byte[mySerialPort.BytesToRead];
                        mySerialPort.Read(receivedBytes, 0, receivedBytes.Length);

                        if (receivedBytes.Length > 0)
                        {
                            ReceivedBuff += Chr(receivedBytes);
                            if (ReturnVerify(receivedBytes))
                            {
                                _waitForResponse.Set();
                            }
                        }
                    };
                    DateTime startTime = DateTime.Now;
                    //mySerialPort = new SerialPort();
                    mySerialPort.WriteLine(sendData + "\r\n");
                    System.Threading.Thread.Sleep(100);//延时100ms等待接收数据
                    SleepAfterSend(sendData.Length, startTime);
                    recvData = mySerialPort.ReadLine();
                    mySerialPort.DiscardInBuffer();

                }
                else
                {
                    portInit();
                    sp_sendData();
                }
            }
            catch (Exception ex)
            {
                serialIsConnect = mySerialPort.IsOpen;

                logger.Error(String.Format("sp_sendData| ", ex.Message));
            }

        }



        public void ReadrecvData()
        {
            try
            {
                if (mySerialPort.IsOpen)
                {
                    int i = 0;
                    recvData = SendCommand(sendData + "\r\n");
                    while (recvData.Trim() == "" || recvData.Trim() == null)
                    {
                        recvData = SendCommand(sendData + "\r\n");
                        i++;
                        if (i >= 10)
                        {
                            logger.Error("ReadrecvData-次数：{0}", i);
                            break;
                        }
                    }
                }
                else
                {
                    serialIsConnect = mySerialPort.IsOpen;
                    portInit();
                    ReadrecvData();

                }
            }
            catch(Exception ex)
            {
                serialIsConnect = mySerialPort.IsOpen;
                logger.Error(String.Format("ReadrecvData| ", ex.Message));

            }

        }









        public string SendCommand(string cmd)
        {
            byte[] cmdBytes = AsciiStr2Bytes(cmd);
            string result = SendCommandEx(cmdBytes);
            //if(response != PLC_Response_Code.Command_Completed_Normally)
            //{
            //    byte[] cmdBytes2 = AsciiStr2Bytes(PLC_Cmd_ErrClear + PLC_Delimeter_Send);
            //    PLC_Response_Code tempReCode = PLC_Response_Code.Command_Completed_Normally;
            //    SendCommandEx(cmdBytes2, ref tempReCode);
            //}

            return result;
        }
        /// <summary>
        /// Converting ASCII charactors to bytes
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public byte[] AsciiStr2Bytes(string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
        /// <summary>
        /// Send command execution
        /// </summary>
        /// <param name="bytesToSend"></param>
        /// <param name="waitForReturn"></param>
        /// <returns></returns>
        public string SendCommandEx(byte[] bytesToSend , int timeOutMs = 500, bool waitForReturn = true)//这里
        {

            try
            {
                ReceivedBuff = "";

                mySerialPort.DataReceived += (sender, args) =>
                {
                    byte[] receivedBytes = new byte[mySerialPort.BytesToRead];
                    mySerialPort.Read(receivedBytes, 0, receivedBytes.Length);

                    if (receivedBytes.Length > 0)
                    {
                        ReceivedBuff += Chr(receivedBytes);
                        if (ReturnVerify(receivedBytes))
                        {
                            _waitForResponse.Set();
                        }
                    }
                };

                // Clear buffer and send command
                mySerialPort.DiscardInBuffer();
                mySerialPort.DiscardOutBuffer();
                mySerialPort.DtrEnable = false;
                mySerialPort.RtsEnable = true;
                Thread.Sleep(Convert.ToInt32(ADDITIONAL_WAIT_TIME_BEFORE_SEND));
                DateTime startTime = DateTime.Now;

                mySerialPort.Write(bytesToSend, 0, bytesToSend.Length);

                // sleep after sending
                Thread.Sleep(100);
                SleepAfterSend(bytesToSend.Length, startTime);
                mySerialPort.RtsEnable = false;
                mySerialPort.DtrEnable = true;

                if (waitForReturn)
                {
                    // Awaiting for command result return
                    if (_waitForResponse.WaitOne(TimeSpan.FromMilliseconds(timeOutMs)) != true)
                    {
                        return "";
                    }
                }

                ReceivedBuff = strTrim(ReceivedBuff);
            }
            catch (Exception ex)
            {
                logger.Error(String.Format("SendCommandEx| ", ex.Message));

                return "";
            }

            return ReceivedBuff;
        }





        /// <summary>
        /// String trimmer of CR LF and SPACE
        /// </summary>
        /// <param name="orgStr"></param>
        /// <returns></returns>
        private string strTrim(string orgStr)
        {
            string retStr = orgStr;
            retStr = retStr.Replace(Environment.NewLine, "");
            retStr = retStr.Replace("\r", "");
            retStr = retStr.Replace("\n", "");

            return retStr;
        }

        /// <summary>
        /// byte[] to ASCII Charactors
        /// </summary>
        /// <param name="orgBytes"></param>
        /// <returns></returns>
        public string Chr(byte[] orgBytes)
        {
            ASCIIEncoding asciiEncoding = new System.Text.ASCIIEncoding();
            return asciiEncoding.GetString(orgBytes);
        }

        /// <summary>
        /// Scanner return varification
        /// </summary>
        /// <param name="receivedBytes"></param>
        /// <returns></returns>
        private bool ReturnVerify(byte[] dataBytes)
        {
            bool ret = false;

            if (dataBytes.Length > 0)
            {
                if (dataBytes[dataBytes.Length - 1] == 0x0D) return true;
            }

            if (dataBytes.Length > 1)
            {
                if ((dataBytes[dataBytes.Length - 2] == 0x0D) && (dataBytes[dataBytes.Length - 1] == 0x0A)) return true;
            }

            return ret;
        }

        /// <summary>
        /// Do Sleep after sending serial data
        /// </summary>
        /// <param name="dataLength"></param>
        /// <param name="startTime"></param>
        private static void SleepAfterSend(int dataLength, DateTime startTime)
        {
            TimeSpan waitTime = CalculateWaitTime(dataLength, startTime);

            if (waitTime.Milliseconds > 0)
                Thread.Sleep(waitTime);
        }

        /// <summary>
        /// Calculator for serial wait time
        /// </summary>
        /// <param name="dataLength"></param>
        /// <param name="startTime"></param>
        /// <returns></returns>
        private static TimeSpan CalculateWaitTime(int dataLength, DateTime startTime)
        {
            TimeSpan requiredTransmissionTime = TimeSpan.FromMilliseconds(Convert.ToInt32(REQUIRED_TRANSMISSION_TIME_FOR_BYTE * dataLength + ADDITIONAL_WAIT_TIME_AFTER_SEND));
            return startTime + requiredTransmissionTime - DateTime.Now;
        }






        private ICommand _ConnectCommand;
        [JsonIgnore]

        public ICommand ConnectCommand
        {
            get
            {
                if (_ConnectCommand == null)
                {
                    _ConnectCommand = new RelayCommand(
                        param => this.UseConnect(),
                        param => this.CanConnect()
                    );
                }
                return _ConnectCommand;
            }
        }
        private bool CanConnect()
        {
            return !serialIsConnect;
        }
        private void UseConnect()
        {
            portInit();

        }


        private ICommand _SentCommand;
        [JsonIgnore]
        public ICommand SentCommand
        {
            get
            {
                if (_SentCommand == null)
                {
                    _SentCommand = new RelayCommand(
                        param => this.UseSent(),
                        param => this.CanSent()
                    );
                }
                return _SentCommand;
            }
        }
        private bool CanSent()
        {
            return serialIsConnect;
        }
        private void UseSent()
        {
            Task.Run(() => ReadrecvData());
            logger.Info("sp_sendData");
        }



    }
}

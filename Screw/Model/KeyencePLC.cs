using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Screw.Model
{
    class KeyencePLC
    {
        #region Fields

        // Serial port
        private AutoResetEvent _waitForResponse;
        private const double ADDITIONAL_WAIT_TIME_BEFORE_SEND = 5.0;
        private const double ADDITIONAL_WAIT_TIME_AFTER_SEND = 50.0;
        private const double REQUIRED_TRANSMISSION_TIME_FOR_BYTE = 9.1525;

        // Serial point object
        // Serial Default = 9600, e , 8, 1
        private SerialPort mySerialPort = null;

        // Command/Response strings and delimeters
        private readonly string PLC_Delimeter_Send = "\r";
        //private readonly string PLC_Delimeter_Recv = "\r";
        private readonly string PLC_Cmd_Connect = "CR";
        private readonly string PLC_Cmd_Disconnect = "CQ";
        private readonly string PLC_Cmd_ErrClear = "ER";
        private readonly string PLC_Cmd_ReadAT = "RD ";
        private readonly string PLC_Cmd_SetAT = "ST ";

        private readonly string PLC_Cmd_ResetAT = "RS ";
        private readonly string PLC_Response_Connect_OK = "CC";
        private readonly string PLC_Response_Disconnect_OK = "CF";
        private readonly string PLC_Response_ErrClear_OK = "OK";

        private readonly string PLC_Response_Set_OK = "OK";

        #endregion

        #region Properties

        /// <summary>
        /// Serialport Send data
        /// </summary>
        private string _SendData;
        public string SendData
        {
            get
            {
                return _SendData;
            }
            set
            {
                _SendData = value;
                //RaisePropertyChangedEvent("SendData");
            }
        }

        /// <summary>
        /// Serialport Received buffer
        /// </summary>
        private string _ReceivedBuff;
        public string ReceivedBuff
        {
            get
            {
                return _ReceivedBuff;
            }
            set
            {
                _ReceivedBuff = value;
                //RaisePropertyChangedEvent("ReceivedBuff");
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor with SerialPort object
        /// </summary>
        /// <param name="_serial">SerialPort Object</param>
        public KeyencePLC(SerialPort _serial)
        {
            // set serial object
            this.mySerialPort = _serial;
        }

        #endregion

        #region Utility Functions

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
        /// Bytes data to 2 digits HEX format string
        /// </summary>
        /// <param name="orgBytes"></param>
        /// <returns></returns>
        public string Bytes2String(byte[] orgBytes)
        {
            string str = "";
            if (orgBytes.Length > 0)
            {
                foreach (byte b in orgBytes)
                {
                    str += b.ToString("X2");
                }
            }
            return str;
        }

        /// <summary>
        /// Space splited hex string to bytes array
        /// </summary>
        /// <param name="hexValues"></param>
        /// <returns></returns>
        public byte[] HexStr2Bytes(string hexValues)
        {
            // string hexValues = "48 65 6C 6C 6F 20 57 6F 72 6C 64 21";
            string[] hexValuesSplit = hexValues.Split(' ');
            byte[] ret = new byte[hexValuesSplit.Length];
            int i = 0;
            foreach (String hex in hexValuesSplit)
            {
                // Convert the number expressed in base-16 to an integer.
                int value = Convert.ToInt32(hex, 16);

                ret[i] = (byte)value;
                i++;
                /*
                // Get the character corresponding to the integral value.
                string stringValue = Char.ConvertFromUtf32(value);
                
                char charValue = (char)value;
                Console.WriteLine("hexadecimal value = {0}, int value = {1}, char value = {2} or {3}",
                                    hex, value, stringValue, charValue);
                */
            }
            /* Output:
                hexadecimal value = 48, int value = 72, char value = H or H
                hexadecimal value = 65, int value = 101, char value = e or e
                hexadecimal value = 6C, int value = 108, char value = l or l
                hexadecimal value = 6C, int value = 108, char value = l or l
                hexadecimal value = 6F, int value = 111, char value = o or o
                hexadecimal value = 20, int value = 32, char value =   or
                hexadecimal value = 57, int value = 87, char value = W or W
                hexadecimal value = 6F, int value = 111, char value = o or o
                hexadecimal value = 72, int value = 114, char value = r or r
                hexadecimal value = 6C, int value = 108, char value = l or l
                hexadecimal value = 64, int value = 100, char value = d or d
                hexadecimal value = 21, int value = 33, char value = ! or !
            */

            return ret;
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
        /// delay function
        /// </summary>
        /// <param name="delayTime">millisecond</param>
        /// <returns></returns>
        public static bool Delay(int delayTime)
        {
            DateTime dtStart = DateTime.Now;

            while (true)
            {
                if (DateTime.Now - dtStart >= TimeSpan.FromMilliseconds(delayTime)) break;
                Application.DoEvents();
            }

            return true;
        }

        /// <summary>
        /// Enum Parser
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        #endregion

        #region Serial Operations

        /// <summary>
        /// Send command execution
        /// </summary>
        /// <param name="bytesToSend"></param>
        /// <param name="waitForReturn"></param>
        /// <returns></returns>
        public string SendCommandEx(byte[] bytesToSend, ref PLC_Response_Code response, int timeOutMs = 500, bool waitForReturn = true)
        {
            response = PLC_Response_Code.Command_Completed_Normally;

            try
            {
                _waitForResponse = new AutoResetEvent(false);
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
                        response = PLC_Response_Code.Command_Recv_Timeout;
                        return "";
                    }
                }

                ReceivedBuff = strTrim(ReceivedBuff);
            }
            catch (Exception ex)
            {
               
                response = PLC_Response_Code.Command_Send_Error;
                return "";
            }

            return ReceivedBuff;
        }

        /// <summary>
        /// Send command   发送  返回发送值？
        /// </summary>
        /// <param name="cmd">Commad string</param>
        /// <returns></returns>
        public string SendCommand(string cmd, ref PLC_Response_Code response)
        {
            byte[] cmdBytes = AsciiStr2Bytes(cmd);
            string result = SendCommandEx(cmdBytes, ref response);
            //if(response != PLC_Response_Code.Command_Completed_Normally)
            //{
            //    byte[] cmdBytes2 = AsciiStr2Bytes(PLC_Cmd_ErrClear + PLC_Delimeter_Send);
            //    PLC_Response_Code tempReCode = PLC_Response_Code.Command_Completed_Normally;
            //    SendCommandEx(cmdBytes2, ref tempReCode);
            //}

            return result;
        }

        #endregion

        #region PLC Operation Methods

        /// <summary>
        /// Read AT
        /// </summary>
        /// <param name="Num">AT Number</param>
        /// <param name="response">reference for response code</param>
        /// <returns></returns>
        public PointState ReadAT(int Num, ref PLC_Response_Code response)
        {
            string cmd = PLC_Cmd_ReadAT + Num.ToString().PadLeft(4, '0') + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                int tempInt = 0;
                if (int.TryParse(result, out tempInt))
                {
                    return (PointState)tempInt;
                }
                else
                {
                    response = PLC_Response_Code.ReadAT_Return_Not_Numeric;
                    return PointState.OFF;
                }
            }
            else
            {
                return PointState.OFF;
            }
        }

        /// <summary>
        /// Set AT
        /// </summary>
        /// <param name="Num">AT Number</param>
        /// <param name="response">reference for response code</param>
        /// <returns></returns>
        public void SetAT(int Num, ref PLC_Response_Code response)
        {
            string cmd = PLC_Cmd_SetAT + Num.ToString().PadLeft(4, '0') + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                if (result == PLC_Response_Set_OK)
                {
                    return;
                }
                else
                {
                    response = PLC_Response_Code.Set_ResetAT_Response_Failure;
                }
            }
        }

        /// <summary>
        /// Reset AT
        /// </summary>
        /// <param name="Num">AT Number</param>
        /// <param name="response">reference for response code</param>
        public void ResetAT(int Num, ref PLC_Response_Code response)
        {
            string cmd = PLC_Cmd_ResetAT + Num.ToString().PadLeft(4, '0') + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                if (result == PLC_Response_Set_OK)
                {
                    return;
                }
                else
                {
                    response = PLC_Response_Code.Set_ResetAT_Response_Failure;
                }
            }
        }

        /// <summary>
        /// Open SerialPort
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            try
            {
                // Close before reopen
                if (mySerialPort.IsOpen)
                {
                    mySerialPort.Close();
                }

                // Open
                mySerialPort.Open();

                if (mySerialPort.IsOpen)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        /// <summary>
        /// Connect To PLC
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            PLC_Response_Code response = PLC_Response_Code.Unknown;
            string cmd = PLC_Cmd_Connect + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                if (result == PLC_Response_Connect_OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Disconnect with PLC
        /// </summary>
        /// <returns></returns>
        public bool Disconnect()
        {
            PLC_Response_Code response = PLC_Response_Code.Unknown;
            string cmd = PLC_Cmd_Disconnect + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                if (result == PLC_Response_Disconnect_OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Clear the Error On PLC
        /// </summary>
        /// <returns></returns>
        public bool ErrorClear()
        {
            PLC_Response_Code response = PLC_Response_Code.Unknown;
            string cmd = PLC_Cmd_ErrClear + PLC_Delimeter_Send;
            string result = SendCommand(cmd, ref response);
            if (response == PLC_Response_Code.Command_Completed_Normally)
            {
                if (result == PLC_Response_ErrClear_OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        #endregion
    }
}

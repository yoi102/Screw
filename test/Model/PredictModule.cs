using Microsoft.Win32;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using test.BaseClasses;
using ZeroMQ;
using ZeroMQ.Monitoring;

namespace test.Model
{
    public class PredictModule : NotifyPropertyChangedBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        [JsonIgnore]
        private ZContext context;   // ZMQ Context
        [JsonIgnore]
        public ZSocket requester;   // ZMQ socket requester
        [JsonIgnore]
        private ZMonitor monitor;   // ZMQ socket event monitor
        [JsonIgnore]
        Process m_Process;

        private string pyEnvPathCache = "";
        private string pyEnvNameCache = "";

        public PredictModule()
        {
            TimeOut = 3;
            PyArguments = "";
        }

        #region Porperty
        /// <summary>
        /// Predictor Name
        /// </summary>
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

        /// <summary>
        /// connection flag
        /// </summary>
        private bool _IsConnected;
        public bool IsConnected
        {
            get
            {
                return _IsConnected;
            }

            set
            {
                if (value != _IsConnected)
                {
                    _IsConnected = value;
                    RaisePropertyChanged("IsConnected");
                }
            }
        }

        /// <summary>
        /// Image resizeing switch
        /// </summary>
        private bool _DoResizing;
        public bool DoResizing
        {
            get
            {
                return _DoResizing;
            }

            set
            {
                if (value != _DoResizing)
                {
                    _DoResizing = value;
                    RaisePropertyChanged("DoResizing");
                }
            }
        }

        /// <summary>
        /// Required Resize width
        /// </summary>
        private int _ResizeWidth;
        public int ResizeWidth
        {
            get
            {
                return _ResizeWidth;
            }

            set
            {
                if (value != _ResizeWidth)
                {
                    _ResizeWidth = value;
                    RaisePropertyChanged("ResizeWidth");
                }
            }
        }

        /// <summary>
        /// Required Resize height
        /// </summary>
        private int _ResizeHeight;
        public int ResizeHeight
        {
            get
            {
                return _ResizeHeight;
            }

            set
            {
                if (value != _ResizeHeight)
                {
                    _ResizeHeight = value;
                    RaisePropertyChanged("ResizeHeight");
                }
            }
        }

        /// <summary>
        /// receive timeout
        /// </summary>
        private int _TimeOut;
        public int TimeOut
        {
            get
            {
                return _TimeOut;
            }

            set
            {
                if (value != _TimeOut)
                {
                    _TimeOut = value;
                    RaisePropertyChanged("TimeOut");
                }
            }
        }

        /// <summary>
        /// ZMQ server URL
        /// </summary>
        private string _ServerUrl;
        public string ServerUrl
        {
            get
            {
                return _ServerUrl;
            }

            set
            {
                if (value != _ServerUrl)
                {
                    _ServerUrl = value;
                    RaisePropertyChanged("ServerUrl");
                }
            }
        }

        /// <summary>
        /// Python exe path
        /// </summary>
        private string _PyExePath;
        public string PyExePath
        {
            get
            {
                return _PyExePath;
            }

            set
            {
                if (value != _PyExePath)
                {
                    _PyExePath = value;
                    RaisePropertyChanged("PyExePath");
                }
            }
        }

        /// <summary>
        /// Python script file name
        /// </summary>
        private string _PyScript;
        public string PyScript
        {
            get
            {
                return _PyScript;
            }

            set
            {
                if (value != _PyScript)
                {
                    _PyScript = value;
                    RaisePropertyChanged("PyScript");
                }
            }
        }

        /// <summary>
        /// arguments when starting Python script 
        /// </summary>
        private string _PyArguments;
        public string PyArguments
        {
            get
            {
                return _PyArguments;
            }

            set
            {
                if (value != _PyArguments)
                {
                    _PyArguments = value;
                    RaisePropertyChanged("PyArguments");
                }
            }
        }

        /// <summary>
        /// busy flag
        /// </summary>
        private bool _IsBusy;
        [JsonIgnore]
        public bool IsBusy
        {
            get
            {
                return _IsBusy;
            }

            set
            {
                if (value != _IsBusy)
                {
                    _IsBusy = value;
                    RaisePropertyChanged("IsBusy");
                }
            }
        }

        /// <summary>
        /// error occured flag
        /// </summary>
        private bool _ErrorFlag;
        [JsonIgnore]
        public bool ErrorFlag
        {
            get
            {
                return _ErrorFlag;
            }

            set
            {
                if (value != _ErrorFlag)
                {
                    _ErrorFlag = value;
                    RaisePropertyChanged("ErrorFlag");
                }
            }
        }


        /// <summary>
        /// offline time
        /// </summary>
        private DateTime _OfflineTime;
        [JsonIgnore]
        public DateTime OfflineTime
        {
            get
            {
                return _OfflineTime;
            }

            set
            {
                if (value != _OfflineTime)
                {
                    _OfflineTime = value;
                    RaisePropertyChanged("OfflineTime");
                }
            }
        }


        /// <summary>
        /// Composed message for sending
        /// </summary>
        private ComposedMessage _SendMsg = new ComposedMessage();
        [JsonIgnore]
        public ComposedMessage SendMsg
        {
            get
            {
                return _SendMsg;
            }

            set
            {
                if (value != _SendMsg)
                {
                    _SendMsg = value;
                    RaisePropertyChanged("SendMsg");
                }
            }
        }

        /// <summary>
        /// Composed message for sending
        /// </summary>
        private ComposedMessage _RecvMsg = new ComposedMessage();
        [JsonIgnore]
        public ComposedMessage RecvMsg
        {
            get
            {
                return _RecvMsg;
            }

            set
            {
                if (value != _RecvMsg)
                {
                    _RecvMsg = value;
                    RaisePropertyChanged("RecvMsg");
                }
            }
        }

        /// <summary>
        /// Recv
        /// </summary>
        private string _Recv;
        [JsonIgnore]
        public string Recv
        {
            get
            {
                return _Recv;
            }

            set
            {
                if (value != _Recv)
                {
                    _Recv = value;
                    RaisePropertyChanged("Recv");
                }
            }
        }

        /// <summary>
        /// request response time
        /// </summary>
        private int _ResponseTime;
        [JsonIgnore]
        public int ResponseTime
        {
            get
            {
                return _ResponseTime;
            }

            set
            {
                if (value != _ResponseTime)
                {
                    _ResponseTime = value;
                    RaisePropertyChanged("ResponseTime");
                }
            }
        }

        /// <summary>
        /// message
        /// </summary>
        private string _Message;
        [JsonIgnore]
        public string Message
        {
            get
            {
                return _Message;
            }

            set
            {
                if (value != _Message)
                {
                    _Message = value;
                    RaisePropertyChanged("Message");
                }
            }
        }

        /// <summary>
        /// Python process ID
        /// </summary>
        private string _PythonProcessID;
        [JsonIgnore]
        public string PythonProcessID
        {
            get
            {
                return _PythonProcessID;
            }

            set
            {
                if (value != _PythonProcessID)
                {
                    _PythonProcessID = value;
                    RaisePropertyChanged("PythonProcessID");
                }
            }
        }

        /// <summary>
        /// to set the python image display to on/off
        /// in interactive mode, the image plot will hold python process and need a key press to continue
        /// </summary>
        private bool _DisplayOnOff;
        [JsonIgnore]
        public bool DisplayOnOff
        {
            get
            {
                return _DisplayOnOff;
            }

            set
            {
                if (value != _DisplayOnOff)
                {
                    _DisplayOnOff = value;
                    RaisePropertyChanged("DisplayOnOff");
                }
            }
        }

        /// <summary>
        /// to set the python image display to be interactive of not
        /// in interactive mode, the image plot will hold python process and need a key press to continue
        /// </summary>
        private bool _InteractiveDisplay;
        [JsonIgnore]
        public bool InteractiveDisplay
        {
            get
            {
                return _InteractiveDisplay;
            }

            set
            {
                if (value != _InteractiveDisplay)
                {
                    _InteractiveDisplay = value;
                    RaisePropertyChanged("InteractiveDisplay");
                }
            }
        }

        /*
        /// <summary>
        /// ZMQ Events
        /// </summary>
        private string _ZmqEvent;
        [JsonIgnore]
        public string ZmqEvent
        {
            get
            {
                return _ZmqEvent;
            }

            set
            {
                if (value != _ZmqEvent)
                {
                    _ZmqEvent = value;
                    RaisePropertyChanged("ZmqEvent");
                }
            }
        }
        */
        #endregion

        #region Method
        /// <summary>
        /// create ZMQ context and client
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            try
            {
                if (ServerUrl != null)
                {
                    context = new ZContext();
                    //////////////////// INITIALIZE REQUESTER ///////////////////
                    requester = new ZSocket(context, ZSocketType.REQ);
                    // set timeout
                    requester.ReceiveTimeout = TimeSpan.FromSeconds(TimeOut);
                    // By default, a REQ socket does not allow initiating a new request with zmq_send(3) until the reply to the previous one has been received.
                    // When set to 1, sending another message is allowed and previous replies will be discarded if any.
                    // The request - reply state machine is reset and a new request is sent to the next available peer.
                    // If set to 1, also enable ZMQ_REQ_CORRELATE to ensure correct matching of requests and replies.
                    // Otherwise a late reply to an aborted request can be reported as the reply to the superseding request.
                    requester.SetOption(ZSocketOption.REQ_RELAXED, 1);

                    //////////////////// BIND EVENT MONITOR /////////////////////
                    // Socket monitoring only works over inproc://
                    // Monitor all events on client
                    bool ret = ZeroMQ.Monitoring.ZMonitors.Monitor(requester, @"inproc://monitor-client", ZeroMQ.Monitoring.ZMonitorEvents.AllEvents);
                    if (!ret) Console.WriteLine("Monitor requester error");
                    // Connect these to the inproc endpoints so they'll get events
                    monitor = ZeroMQ.Monitoring.ZMonitor.Create(context, @"inproc://monitor-client");
                    if (monitor == null) Console.WriteLine("Create monitor socket error");
                    monitor.AllEvents += Monitor_AllEvents;
                    monitor.Start();

                    /////////////////////// START CONNECTION ////////////////////
                    // Connect
                    requester.Connect(ServerUrl);

                    //IsConnected = true;
                    return true;
                }
                else
                {
                    //IsConnected = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                //IsConnected = false;
                logger.Error(string.Format("Predictor {0} connect error|{1}", Name, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// ZMQ Events handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Monitor_AllEvents(object sender, ZMonitorEventArgs e)
        {
            switch (e.Event.Event)
            {
                case ZMonitorEvents.Connected:
                    IsConnected = true;
                    ErrorFlag = false;
                    // get python pid when connected
                    GetPythonProcessID();
                    break;
                case ZMonitorEvents.Closed:
                    IsConnected = false;
                    break;
                default:
                    break;
            }

            Message = e.Event.Event.ToString();
        }

        /// <summary>
        /// release monitor
        /// </summary>
        public void Release()
        {
            if (monitor != null)
            {
                monitor.Stop();
            }
        }

        /// <summary>
        /// clear flag and messages
        /// </summary>
        public void ClearFlagAndMsg()
        {
            ErrorFlag = false;
            Recv = "";
            ResponseTime = 0;
            Message = "";
        }

        /// <summary>
        /// Start predictor python script
        /// </summary>
        /// <returns></returns>
        public bool StartPyScript(string pyEnvPath, string envName = "ai")
        {
            logger.Info(string.Format("Starting Predictor {0} ...", Name));
            if (PyScript == "")
            {
                logger.Error("No available python script!");
                return false;
            }

            if (pyEnvPath == "")
            {
                logger.Error("No available python environment!");
                return false;
            }
            // store python env path to cache
            if (pyEnvPathCache == "") pyEnvPathCache = pyEnvPath;
            if (pyEnvNameCache == "") pyEnvNameCache = envName;

            logger.Info(string.Format("Python Evn Path = {0}, env = {1}, PyScript = {2}", pyEnvPath, envName, PyScript));
            // Set working directory and create process
            var workingDirectory = Path.GetFullPath("Scripts");
            m_Process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false, // if this is set to be true, will cause error 'Resource temperarily unavailable'
                    RedirectStandardError = true,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true
                }
            };
            m_Process.Start();

            // Pass multiple commands to cmd.exe
            using (var sw = m_Process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    // Vital to activate Anaconda
                    //sw.WriteLine(@"C:\PathToAnaconda\anaconda3\Scripts\activate.bat");
                    sw.WriteLine(pyEnvPath + @"\Scripts\activate.bat");

                    // Activate your environment
                    sw.WriteLine("conda activate " + envName);

                    // Any other commands you want to run
                    //sw.WriteLine("set KERAS_BACKEND=tensorflow");

                    // run your script. You can also pass in arguments
                    sw.WriteLine(string.Format("python {0} {1}", PyScript, PyArguments));
                }
            }

            Console.WriteLine(m_Process.Id);
            /*
            // read multiple output lines
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }
            */
            logger.Info(string.Format("Starting Predictor {0} ... OK", Name));
            return true;
        }

        /// <summary>
        /// Terminate python process
        /// </summary>
        public void TerminatePyProcess()
        {
            try
            {
                logger.Info(string.Format("Terminating Predictor {0} ... ", Name));
                if (m_Process != null)
                {
                    m_Process.Close();
                    m_Process.Dispose();
                    m_Process = null;
                }

                // kill python process
                killPythonProcess();

                logger.Info(string.Format("Terminating Predictor {0} ... OK", Name));
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                logger.Error(string.Format("Terminating Predictor {0} ... Failed|{1}", Name, ex.Message));
            }
        }

        /// <summary>
        /// Kill python process by PID
        /// </summary>
        private void killPythonProcess()
        {
            try
            {
                if (PythonProcessID != "")
                {
                    Process ps = Process.GetProcessById(int.Parse(PythonProcessID));
                    if (ps != null)
                    {
                        ps.Kill();
                        PythonProcessID = "";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(string.Format("KillPythonProcess Error|{0}|{1}", Name, ex.Message));
            }
        }

        /// <summary>
        /// restart python process
        /// </summary>
        public void RestartPyProcess()
        {
            try
            {
                logger.Info(string.Format("Try to restart python process|{0}...", Name));
                // terminate current
                TerminatePyProcess();
            }
            catch (Exception ex)
            {
                logger.Error("RestartPyProcess|TerminatePyProcess|" + ex.Message);
            }

            // restart 
            try
            {
                if (pyEnvPathCache != "")
                {
                    StartPyScript(pyEnvPathCache, pyEnvNameCache);
                }
                else
                {
                    string pyExePath = GetPythonExePath();
                    if (PyExePath != "")
                    {
                        StartPyScript(pyExePath);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("RestartPyProcess|StartPyScript|" + ex.Message);
            }
        }

        /// <summary>
        /// Transfering image to AI module via ZMQ and retreive predict result
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool ZmqRequest()
        {
            bool ret = false;
            try
            {
                if (IsConnected)
                {
                    DateTime dtStart = DateTime.Now;
                    string recvStr = "";
                    // clear predictor
                    ClearFlagAndMsg();
                    RecvMsg.Clear();
                    IsBusy = true;

                    // Send frame
                    requester.Send(SerializeMsg(SendMsg));

                    // Receive
                    ZError err;
                    using (ZFrame reply = requester.ReceiveFrame(out err))
                    {
                        // Interrupted
                        //if (err == ZError.ETERM || err == ZError.EAGAIN)
                        if (err != ZError.None)
                        {
                            ErrorFlag = true;
                            OfflineTime = DateTime.Now;
                            Message = err.Text;
                            logger.Error(string.Format("ZmqRequest Error|{0}|{1}", Name, err.Text));
                            ret = false;
                        }
                        else
                        {
                            if (reply != null)
                            {
                                recvStr = reply.ReadString();
                                RecvMsg = DeserializeMsg(recvStr);
                                //Console.WriteLine(" Received: {0}", result);
                                ret = true;
                                ErrorFlag = false;
                            }
                        }
                    }

                    ResponseTime = (int)(DateTime.Now - dtStart).TotalMilliseconds;
                }
                else
                {
                    logger.Error(string.Format("ZmqRequest Error|{0} not connected", Name));
                    ret = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(string.Format("ZmqRequest Error|{0}|{1}", Name, ex.Message));
                ErrorFlag = true;
                Message = ex.Message;
                ret = false;
            }
            finally
            {
                IsBusy = false;
            }

            return ret;
        }

        /// <summary>
        /// serialize composed message to ZFrame
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private ZFrame SerializeMsg(ComposedMessage msg)
        {
            var message = JsonConvert.SerializeObject(msg);
            return new ZFrame(Encoding.UTF8.GetBytes(message));
        }

        /// <summary>
        /// deserialize frame string to Composed message
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private ComposedMessage DeserializeMsg(string msgStr)
        {
            ComposedMessage msg = JsonConvert.DeserializeObject<ComposedMessage>(msgStr);
            return msg;
        }

        /// <summary>
        /// Request for Process ID
        /// </summary>
        private void GetPythonProcessID()
        {
            SendMsg.cmd = "getpid";
            if (ZmqRequest())
            {
                PythonProcessID = RecvMsg.para;
            }
        }

        /// <summary>
        /// reverse interactive display flag and send to python script 
        /// </summary>
        private void ReverseDisplayOnOff()
        {
            try
            {
                SendMsg.cmd = "display";
                SendMsg.para = (!DisplayOnOff).ToString();
                if (ZmqRequest())
                {
                    DisplayOnOff = bool.Parse(RecvMsg.para);
                }
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// reverse interactive display flag and send to python script 
        /// </summary>
        private void ReverseInteractiveDisplay()
        {
            try
            {
                SendMsg.cmd = "interactive";
                SendMsg.para = (!InteractiveDisplay).ToString();
                if (ZmqRequest())
                {
                    InteractiveDisplay = bool.Parse(RecvMsg.para);
                }
            }
            catch (Exception ex)
            {

            }
        }

        /// <summary>
        /// Get Python Executable path from register
        /// </summary>
        private string GetPythonExePath()
        {
            string pythonPath = "";
            string registData = "";

            try
            {
                // RegistryKey hkml = Registry.LocalMachine;
                // *** In 64 bit system the hkml will be automatically convert into "HKLM\SOFTWARE\Wow6432Node"
                // *** then the subkey "Python\PythonCore" can never be found. 
                // *** So here we use "OpenBaseKey" to avoid the auto convertion
                var hkml = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

                bool key36Exists = false;
                using (RegistryKey key = hkml.OpenSubKey(@"Software\Python\PythonCore\3.6\InstallPath"))
                {
                    if (key != null)
                    {
                        Object val = key.GetValue("");
                        if (val != null)
                        {
                            registData = (string)val;
                            //pythonPath = registData + @"\python.exe";
                            pythonPath = registData;
                            key36Exists = true;
                        }
                    }
                }

                // try to find python3.7
                if (!key36Exists)
                {
                    using (RegistryKey key = hkml.OpenSubKey(@"Software\Python\PythonCore\3.7\InstallPath"))
                    {
                        if (key != null)
                        {
                            Object val = key.GetValue("");
                            if (val != null)
                            {
                                registData = (string)val;
                                //pythonPath = registData + @"\python.exe";
                                pythonPath = registData;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Searching for Python path error.\r\nMake sure Python3.6 is correctly installed.\r\n" + ex.Message);
            }

            return pythonPath;
        }

        #endregion

        #region Commands
        /// <summary>
        /// StartPythonProcess command
        /// </summary>
        private ICommand _StartPythonProcessCommand;
        [JsonIgnore]
        public ICommand StartPythonProcessCommand
        {
            get
            {
                if (_StartPythonProcessCommand == null)
                {
                    _StartPythonProcessCommand = new RelayCommand(
                        param => this.StartPythonProcessExecute(),
                        param => this.CanStartPythonProcess()
                    );
                }
                return _StartPythonProcessCommand;
            }
        }
        private bool CanStartPythonProcess()
        {
            return true;
        }
        private void StartPythonProcessExecute()
        {
            if (pyEnvPathCache != "")
            {
                StartPyScript(pyEnvPathCache, pyEnvNameCache);
            }
            else
            {
                string pyExePath = GetPythonExePath();
                if (PyExePath != "")
                {
                    StartPyScript(pyExePath);
                }
            }
        }

        /// <summary>
        /// Terminate Python Process command
        /// </summary>
        private ICommand _TerminatePythonProcessCommand;
        [JsonIgnore]
        public ICommand TerminatePythonProcessCommand
        {
            get
            {
                if (_TerminatePythonProcessCommand == null)
                {
                    _TerminatePythonProcessCommand = new RelayCommand(
                        param => this.TerminatePythonProcessExecute(),
                        param => this.CanTerminatePythonProcess()
                    );
                }
                return _TerminatePythonProcessCommand;
            }
        }
        private bool CanTerminatePythonProcess()
        {
            return true;
        }
        private void TerminatePythonProcessExecute()
        {
            TerminatePyProcess();
        }

        /// <summary>
        /// switch display on/off state command
        /// </summary>
        private ICommand _SwitchDisplayCommand;
        [JsonIgnore]
        public ICommand SwitchDisplayCommand
        {
            get
            {
                if (_SwitchDisplayCommand == null)
                {
                    _SwitchDisplayCommand = new RelayCommand(
                        param => this.SwitchDisplayExecute(),
                        param => this.CanSwitchDisplay()
                    );
                }
                return _SwitchDisplayCommand;
            }
        }
        private bool CanSwitchDisplay()
        {
            return true;
        }
        private void SwitchDisplayExecute()
        {
            ReverseDisplayOnOff();
        }

        /// <summary>
        /// switch interactive display state command
        /// </summary>
        private ICommand _SwitchInteractiveCommand;
        [JsonIgnore]
        public ICommand SwitchInteractiveCommand
        {
            get
            {
                if (_SwitchInteractiveCommand == null)
                {
                    _SwitchInteractiveCommand = new RelayCommand(
                        param => this.SwitchInteractiveExecute(),
                        param => this.CanSwitchInteractive()
                    );
                }
                return _SwitchInteractiveCommand;
            }
        }
        private bool CanSwitchInteractive()
        {
            return true;
        }
        private void SwitchInteractiveExecute()
        {
            ReverseInteractiveDisplay();
        }
        #endregion
    }
}
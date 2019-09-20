using log4net.Config;
using RabbitMQ.Client;
using service_performance;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService1
{
    public partial class Service1 : ServiceBase
    {
        static Service1()
        {
            DOMConfigurator.Configure();
        }

        public Service1()
        {
            InitializeComponent();
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private PerformanceCounter _cpuUsage = default(PerformanceCounter);
        private InfoData _dto = default(InfoData);
        private List<InfoData> _lstDTO = new List<InfoData>();
        private int _secondCheck = 5;
        private double _secondHighCheck = 5;
        private double _totalPhysicalMemory = 0;
        private float _cpuHighPercent = 5;
        private float _ramHighPercent = 5;
        private float _hddHighPercent = 5;
        private System.Timers.Timer _timerGet = null;
        private System.Timers.Timer _timerGetReset = null;
        private System.Timers.Timer _timerSend = null;
        private System.Timers.Timer _timerSendReset = null;
        private int _reset = 0;
        private bool _islog = false;
        private bool _isSendHigh = false;
        private string _servername = string.Empty;
        private string _rabbitHost = string.Empty;
        private int _rabbitPort = 0;
        private string _rabbitUserName = string.Empty;
        private string _rabbitPassword = string.Empty;
        private string _rabbitPerformanceDataKey = string.Empty;
        private string _rabbitPerformanceHighKey = string.Empty;
        private string _batchFileInit = string.Empty;
        private string _batchFileCheck = string.Empty;
        private string _batchFileClear = string.Empty;
        private bool _batchClear = false;
        private bool _batchRun = false;
        private bool _batchComplete = false;
        private string _batchLastOutput = string.Empty;

        protected override void OnStart(string[] args)
        {
            try
            {
                var timersend = System.Configuration.ConfigurationManager.AppSettings.Get("TimerSend");
                var logdata = System.Configuration.ConfigurationManager.AppSettings.Get("LogData");
                _servername = System.Configuration.ConfigurationManager.AppSettings.Get("ServerName");

                var rabbitHost = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitHost");
                var rabbitPort = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitPort");
                var rabbitUserName = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitUserName");
                var rabbitPassword = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitPassword");
                var rabbitPerformanceDataKey = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitPerformanceDataKey");
                var rabbitPerformanceHighKey = System.Configuration.ConfigurationManager.AppSettings.Get("RabbitPerformanceHighKey");

                var cpuHighPercent = System.Configuration.ConfigurationManager.AppSettings.Get("CPUHighPercent");
                var ramHighPercent = System.Configuration.ConfigurationManager.AppSettings.Get("RAMHighPercent");
                var hddHighPercent = System.Configuration.ConfigurationManager.AppSettings.Get("HDDHighPercent");
                var sendHigh = System.Configuration.ConfigurationManager.AppSettings.Get("SendHigh");
                var secondHighCheck = System.Configuration.ConfigurationManager.AppSettings.Get("SecondHighCheck");
                var secondCheck = System.Configuration.ConfigurationManager.AppSettings.Get("SecondCheck");
                _batchFileInit = System.Configuration.ConfigurationManager.AppSettings.Get("BatchInit");
                _batchFileCheck = System.Configuration.ConfigurationManager.AppSettings.Get("BatchCheck");
                _batchFileClear = System.Configuration.ConfigurationManager.AppSettings.Get("BatchClear");
                Console.WriteLine(rabbitUserName);

                if (!string.IsNullOrEmpty(timersend) && !string.IsNullOrEmpty(logdata) && !string.IsNullOrEmpty(_servername) && !string.IsNullOrEmpty(sendHigh) && !string.IsNullOrEmpty(secondCheck))
                {
                    int i = Convert.ToInt32(timersend);
                    _secondCheck = Convert.ToInt32(secondCheck);
                    _secondHighCheck = Convert.ToDouble(secondHighCheck);
                    if (i > 0 && _secondCheck > 5)
                    {
                        _lstDTO = new List<InfoData>();
                        _islog = logdata == "true";
                        _isSendHigh = sendHigh == "true";
                        if (!string.IsNullOrEmpty(rabbitPort))
                            _rabbitPort = Convert.ToInt32(rabbitPort);
                        _rabbitHost = rabbitHost;
                        _rabbitUserName = rabbitUserName;
                        _rabbitPassword = rabbitPassword;
                        _rabbitPerformanceDataKey = rabbitPerformanceDataKey;
                        _rabbitPerformanceHighKey = rabbitPerformanceHighKey;

                        _batchClear = false;
                        _batchRun = false;
                        _batchComplete = false;

                        if (!string.IsNullOrEmpty(cpuHighPercent))
                            _cpuHighPercent = Convert.ToSingle(cpuHighPercent);
                        if (!string.IsNullOrEmpty(ramHighPercent))
                            _ramHighPercent = Convert.ToSingle(ramHighPercent);
                        if (!string.IsNullOrEmpty(hddHighPercent))
                            _hddHighPercent = Convert.ToSingle(hddHighPercent);
                        if (_cpuHighPercent < 1 || _ramHighPercent < 1 || _hddHighPercent < 1)
                            throw new Exception("HighPercent fail");

                        HelperProcess.Init();
                        Microsoft.VisualBasic.Devices.ComputerInfo ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                        _totalPhysicalMemory = (ci.TotalPhysicalMemory / 1024) * 0.001;
                        _cpuUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                        _dto = GetInfo();

                        LogInfo("Start service (" + _servername + ")");
                        _timerGet = new System.Timers.Timer(1000);//1s
                        _timerGet.Elapsed += TimerGet_Elapsed;
                        _timerGet.Enabled = true;
                        _timerGetReset = new System.Timers.Timer(600000);//10p reset
                        _timerGetReset.Enabled = false;
                        _timerGetReset.Elapsed += TimerGetReset_Elapsed;

                        _timerSend = new System.Timers.Timer(i);
                        _timerSend.Elapsed += TimerSend_Elapsed;
                        _timerSend.Enabled = true;
                        _timerSendReset = new System.Timers.Timer(600000);//10p reset
                        _timerSendReset.Enabled = false;
                        _timerSendReset.Elapsed += TimerSendReset_Elapsed;
                    }
                    else
                        throw new Exception("TimerSend fail");
                }
                else
                    throw new Exception("Config fail");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private DateTime? _dtHighNext = null;

        protected void TimerGet_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                _timerGet.Enabled = false;

                _dto = GetInfo();
                if (_islog)
                    LogInfo("TimerGet: " + Newtonsoft.Json.JsonConvert.SerializeObject(_dto));

                if (!string.IsNullOrEmpty(_rabbitHost) && _rabbitPort > 0 && !string.IsNullOrEmpty(_rabbitPerformanceHighKey))
                {
                    if (_dto != null && (_dto.CPUHigh || _dto.RAMHigh || _dto.HDDHigh))
                    {
                        if (_dtHighNext != null && DateTime.Now.CompareTo(_dtHighNext.Value) > 0)
                        {
                            var factory = new ConnectionFactory() { HostName = _rabbitHost, Port = _rabbitPort, UserName = _rabbitUserName, Password = _rabbitPassword };
                            using (var connection = factory.CreateConnection())
                            using (var channel = connection.CreateModel())
                            {
                                channel.QueueDeclare(queue: _rabbitPerformanceHighKey, durable: false, exclusive: false, autoDelete: false, arguments: null);
                                string str = Newtonsoft.Json.JsonConvert.SerializeObject(_dto);
                                channel.BasicPublish("", _rabbitPerformanceHighKey, null, Encoding.Unicode.GetBytes(str));

                                if (_islog)
                                    LogInfo("TimerGet send rabbit");
                            }

                            _dtHighNext = DateTime.Now.AddSeconds(_secondHighCheck);
                        }
                    }
                }



                _timerGet.Enabled = true;
            }
            catch (Exception ex)
            {
                LogError(ex);
                _timerGetReset.Enabled = true;
            }
        }

        protected void TimerGetReset_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                _timerGetReset.Enabled = false;

                if (_islog)
                    LogInfo("Reset service (" + _reset + ")");
                _reset++;

                _timerGet.Enabled = true;
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        protected void TimerSend_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                _timerSend.Enabled = false;

                if (_islog)
                    LogInfo("TimerSend start");

                if (_islog)
                    LogInfo("TimerSend data: " + Newtonsoft.Json.JsonConvert.SerializeObject(_dto));

                if (!string.IsNullOrEmpty(_rabbitHost) && _rabbitPort > 0 && !string.IsNullOrEmpty(_rabbitPerformanceDataKey))
                {
                    var factory = new ConnectionFactory() { HostName = _rabbitHost, Port = _rabbitPort, UserName = _rabbitUserName, Password = _rabbitPassword };
                    using (var connection = factory.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        channel.QueueDeclare(queue: _rabbitPerformanceDataKey, durable: false, exclusive: false, autoDelete: false, arguments: null);
                        string str = Newtonsoft.Json.JsonConvert.SerializeObject(_dto);
                        channel.BasicPublish("", _rabbitPerformanceDataKey, null, Encoding.Unicode.GetBytes(str));

                        if (_islog)
                            LogInfo("TimerSend send rabbit");
                    }
                }

                if (!string.IsNullOrEmpty(_batchFileInit) && !string.IsNullOrEmpty(_batchFileCheck) && !string.IsNullOrEmpty(_batchFileClear))
                {
                    if (!_batchClear)
                    {
                        if (!string.IsNullOrEmpty(_batchFileClear))
                        {
                            if (_islog)
                                LogInfo("batch clear");

                            ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", "/c " + _batchFileClear);
                            processInfo.CreateNoWindow = true;
                            processInfo.UseShellExecute = false;
                            processInfo.RedirectStandardError = true;
                            processInfo.RedirectStandardOutput = true;

                            var process = Process.Start(processInfo);
                            process.OutputDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                            {
                                if (_islog)
                                    LogInfo("output>>" + eprocess.Data);
                            };
                            process.BeginOutputReadLine();

                            process.ErrorDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                            {
                                if (_islog)
                                    LogInfo("error>>" + eprocess.Data);
                            };
                            process.BeginErrorReadLine();
                            process.Start();
                            process.Close();

                            _batchClear = true;
                        }
                    }
                    else if (!_batchComplete)
                    {
                        if (_islog)
                            LogInfo("_batchLastOutput: " + _batchLastOutput);

                        if (!_batchRun)
                        {
                            if (_islog)
                                LogInfo("batch init");

                            if (!string.IsNullOrEmpty(_batchFileInit) && !string.IsNullOrEmpty(_batchFileCheck))
                            {
                                ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", "/c " + _batchFileInit);
                                processInfo.CreateNoWindow = true;
                                processInfo.UseShellExecute = false;
                                processInfo.RedirectStandardError = true;
                                processInfo.RedirectStandardOutput = true;

                                var process = Process.Start(processInfo);
                                process.OutputDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                                {
                                    if (_islog)
                                        LogInfo("output>>" + eprocess.Data);
                                };
                                process.BeginOutputReadLine();

                                process.ErrorDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                                {
                                    if (_islog)
                                        LogInfo("error>>" + eprocess.Data);
                                };
                                process.BeginErrorReadLine();
                                process.Start();
                                process.Close();
                            }

                            _batchLastOutput = string.Empty;
                            _batchRun = true;
                        }
                        else if (!string.IsNullOrEmpty(_batchLastOutput))
                        {
                            if (_islog)
                                LogInfo("batch complete");

                            if (_batchLastOutput == "true")
                            {
                                _batchComplete = true;
                            }
                            else
                                throw new Exception("fail batch file");
                        }
                        else
                        {
                            if (_islog)
                                LogInfo("batch check");

                            ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", "/c " + _batchFileCheck);
                            processInfo.CreateNoWindow = true;
                            processInfo.UseShellExecute = false;
                            processInfo.RedirectStandardError = true;
                            processInfo.RedirectStandardOutput = true;

                            var process = Process.Start(processInfo);
                            process.OutputDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                            {
                                if (_islog)
                                    LogInfo("output>>" + eprocess.Data);
                                if (_batchLastOutput != "true")
                                    _batchLastOutput = eprocess.Data;
                                if (!string.IsNullOrEmpty(_batchLastOutput))
                                    _batchLastOutput = _batchLastOutput.ToLower().Trim();
                            };
                            process.BeginOutputReadLine();

                            process.ErrorDataReceived += (object senderprocess, DataReceivedEventArgs eprocess) =>
                            {
                                if (_islog)
                                    LogInfo("error>>" + eprocess.Data);
                            };
                            process.BeginErrorReadLine();
                            process.Start();
                            process.Close();
                        }
                    }
                }

                _timerSend.Enabled = true;
            }
            catch (Exception ex)
            {
                LogError(ex);
                _timerSendReset.Enabled = true;
            }
        }

        protected void TimerSendReset_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                _timerSendReset.Enabled = false;

                if (_islog)
                    LogInfo("Reset service (" + _reset + ")");
                _reset++;

                _timerSend.Enabled = true;
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        protected override void OnStop()
        {
            try
            {
                if (_timerGet != null)
                    _timerGet.Enabled = false;
                if (_timerGetReset != null)
                    _timerGetReset.Enabled = false;
                if (_timerSend != null)
                    _timerSend.Enabled = false;
                if (_timerSendReset != null)
                    _timerSendReset.Enabled = false;

                _timerGet = null;
                _timerGetReset = null;
                _timerSend = null;
                _timerSendReset = null;
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void LogInfo(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                log4net.LogicalThreadContext.Properties["Reset"] = _reset + "";
                log4net.LogicalThreadContext.Properties["Date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                log4net.LogicalThreadContext.Properties["DateTicks"] = DateTime.Now.Ticks.ToString();
                log4net.LogicalThreadContext.Properties["StackTrace"] = "";
                log.Info(message);
            }
        }

        private void LogError(Exception ex)
        {
            if (ex != null)
            {
                log4net.LogicalThreadContext.Properties["Reset"] = _reset + "";
                log4net.LogicalThreadContext.Properties["Date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                log4net.LogicalThreadContext.Properties["DateTicks"] = DateTime.Now.Ticks.ToString();
                log4net.LogicalThreadContext.Properties["StackTrace"] = Newtonsoft.Json.JsonConvert.SerializeObject(ex);
                log.Error(ex.Message);
            }
        }

        private InfoData GetInfo()
        {
            var result = new InfoData();
            var ramMBUsage = new PerformanceCounter("Memory", "Available MBytes");
            var hddPerUsage = new PerformanceCounter("LogicalDisk", "% Free Space", "_Total", true);
            var hddMBUsage = new PerformanceCounter("LogicalDisk", "Free Megabytes", "_Total", true);

            result.ServerName = _servername;
            result.RabbitDate = DateTime.Now;
            result.CPUUsagePercent = _cpuUsage.NextValue();
            result.RAMFreeMB = ramMBUsage.NextValue();
            result.HDDFreePercent = hddPerUsage.NextValue();
            result.HDDFreeMB = hddMBUsage.NextValue();
            result.HDDHigh = false;
            result.RAMHigh = false;
            result.CPUHigh = false;
            PerformanceCounter.CloseSharedResources();
            result.ListDetails = HelperProcess.GetProcessTop();

            if (_lstDTO != null)
            {
                if (_lstDTO.Count >= _secondCheck)
                {
                    int cpu = 0;
                    int ram = 0;
                    int hdd = 0;

                    foreach (var item in _lstDTO)
                    {
                        if (item.CPUUsagePercent > _cpuHighPercent)
                            cpu += 2;
                        else if (item.CPUUsagePercent > _cpuHighPercent - 5)
                            cpu++;
                        if (item.RAMFreeMB / (_totalPhysicalMemory / 100) < _ramHighPercent)
                            ram += 2;
                        else if (item.RAMFreeMB / (_totalPhysicalMemory / 100) < _ramHighPercent + 5)
                            ram++;
                        if (item.HDDFreePercent < _hddHighPercent)
                            hdd += 2;
                        else if (item.HDDFreePercent < _hddHighPercent + 5)
                            hdd++;
                    }
                    if (result.CPUUsagePercent > _cpuHighPercent)
                        cpu += 2;
                    else if (result.CPUUsagePercent > _cpuHighPercent - 5)
                        cpu++;
                    if (result.RAMFreeMB / (_totalPhysicalMemory / 100) < _ramHighPercent)
                        ram += 2;
                    else if (result.RAMFreeMB / (_totalPhysicalMemory / 100) < _ramHighPercent + 5)
                        ram++;
                    if (result.HDDFreePercent < _hddHighPercent)
                        hdd += 2;
                    else if (result.HDDFreePercent < _hddHighPercent + 5)
                        hdd++;

                    var check = ((_secondCheck + 1) * 2) - ((_secondCheck + 1) / 4);
                    result.CPUHigh = cpu >= check;
                    result.RAMHigh = ram >= check;
                    result.HDDHigh = hdd >= check;

                    if (_islog)
                        LogInfo(string.Format("CPU:{1}>={0} RAM:{2}>={0} HDD:{3}>={0}", check, cpu, ram, hdd));

                    _lstDTO.RemoveAt(_lstDTO.Count - 1);
                }

                _lstDTO.Insert(0, new InfoData
                {
                    ServerName = result.ServerName,
                    RabbitDate = result.RabbitDate,
                    CPUUsagePercent = result.CPUUsagePercent,
                    RAMFreeMB = result.RAMFreeMB,
                    HDDFreePercent = result.HDDFreePercent,
                    HDDFreeMB = result.HDDFreeMB,
                    HDDHigh = result.HDDHigh,
                    RAMHigh = result.RAMHigh,
                    CPUHigh = result.CPUHigh
                });
            }
            WriteToFile(result.ToString());

            return result;
        }

        public void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to.   
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }
    }
}
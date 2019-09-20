using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;

namespace service_performance
{
    public class HelperProcess
    {
        const Process CLOSED_PROCESS = null;
        const ProcessInfo PROCESS_INFO_NOT_FOUND = null;

        private static int _detailTop = 0;
        private static double _detailMinCPU = 0;
        private static double _detailMinRAM = 0;

        private static PerformanceCounter TotalCpuUsage = new PerformanceCounter("Process", "% Processor Time", "Idle");
        private static float TotalCpuUsageValue;
        private static ProcessInfo[] ProcessList;
        private static double CpuUsagePercent;
        private static int ProcessIndex;
        private static CultureInfo ValueFormat = new CultureInfo("en-US");

        public static void Init()
        {
            var detailTop = System.Configuration.ConfigurationManager.AppSettings.Get("DetailTop");
            var detailMinCPU = System.Configuration.ConfigurationManager.AppSettings.Get("DetailMinCPU");
            var detailMinRAM = System.Configuration.ConfigurationManager.AppSettings.Get("DetailMinRAM");

            if (!string.IsNullOrEmpty(detailTop) && !string.IsNullOrEmpty(detailMinCPU) && !string.IsNullOrEmpty(detailMinRAM))
            {
                _detailTop = Convert.ToInt32(detailTop);
                _detailMinCPU = Convert.ToDouble(detailMinCPU);
                _detailMinRAM = Convert.ToDouble(detailMinRAM);
            }
        }

        public static List<ProcessInfo> GetProcessTop()
        {
            if (_detailTop > 0 && _detailMinCPU >= 0 && _detailMinRAM >= 0)
            {
                Process[] NewProcessList = Process.GetProcesses().Where(p =>
                {
                    bool hasException = false;
                    try { IntPtr x = p.Handle; }
                    catch { hasException = true; }
                    return !hasException;
                }).ToArray();
                UpdateCpuUsagePercent(NewProcessList);
                UpdateExistingProcesses(NewProcessList);
                AddNewProcesses(NewProcessList);

                var lstCPU = ProcessList.Where(c => c.CpuUsage >= _detailMinCPU).OrderByDescending(c => c.CpuUsage).Take(_detailTop).ToList();
                var lstRAM = ProcessList.Where(c => c.PrivateMemorySize64 >= _detailMinRAM).OrderByDescending(c => c.PrivateMemorySize64).Take(_detailTop).ToList();
                foreach (var item in lstRAM)
                {
                    if (lstCPU.Where(c => c.Name == item.Name).Count() == 0)
                    {
                        lstCPU.Add(item);
                    }
                }

                return lstCPU;
            }
            else
                return new List<ProcessInfo>();
        }

        private static void UpdateCpuUsagePercent(Process[] NewProcessList)
        {
            // total the cpu usage then divide to get the usage of 1%
            double Total = 0;
            ProcessInfo TempProcessInfo;
            TotalCpuUsageValue = TotalCpuUsage.NextValue();

            foreach (Process TempProcess in NewProcessList)
            {
                if (TempProcess.Id == 0) continue;

                TempProcessInfo = ProcessInfoByID(TempProcess.Id);
                if (TempProcessInfo == PROCESS_INFO_NOT_FOUND)
                    Total += TempProcess.TotalProcessorTime.TotalMilliseconds;
                else
                    Total += TempProcess.TotalProcessorTime.TotalMilliseconds - TempProcessInfo.OldCpuUsage;
            }
            CpuUsagePercent = Total / (100 - TotalCpuUsageValue);
        }

        private static void UpdateExistingProcesses(Process[] NewProcessList)
        {
            // updates the cpu usage of already loaded processes
            if (ProcessList == null)
            {
                ProcessList = new ProcessInfo[NewProcessList.Length];
                return;
            }

            ProcessInfo[] TempProcessList = new ProcessInfo[NewProcessList.Length];
            ProcessIndex = 0;

            foreach (ProcessInfo TempProcess in ProcessList)
            {
                Process CurrentProcess = ProcessExists(NewProcessList, TempProcess.ID);

                if (CurrentProcess == CLOSED_PROCESS)
                {

                }
                else
                {
                    TempProcessList[ProcessIndex++] = GetProcessInfo(TempProcess, CurrentProcess);
                }
            }

            ProcessList = TempProcessList;
        }

        private static void AddNewProcesses(Process[] NewProcessList)
        {
            // loads a new processes
            foreach (Process NewProcess in NewProcessList)
                if (!ProcessInfoExists(NewProcess))
                    AddNewProcess(NewProcess);
        }

        private static ProcessInfo ProcessInfoByID(int ID)
        {
            // gets the process info by it's id
            if (ProcessList == null) return PROCESS_INFO_NOT_FOUND;

            for (int i = 0; i < ProcessList.Length; i++)
                if (ProcessList[i] != PROCESS_INFO_NOT_FOUND && ProcessList[i].ID == ID)
                    return ProcessList[i];

            return PROCESS_INFO_NOT_FOUND;
        }

        private static Process ProcessExists(Process[] NewProcessList, int ID)
        {
            // checks to see if we already loaded the process
            foreach (Process TempProcess in NewProcessList)
                if (TempProcess.Id == ID)
                    return TempProcess;

            return CLOSED_PROCESS;
        }

        private static ProcessInfo GetProcessInfo(ProcessInfo TempProcess, Process CurrentProcess)
        {
            // gets the process name , id, and cpu usage
            if (CurrentProcess.Id == 0)
                TempProcess.CpuUsage = (double)TotalCpuUsageValue;
            else
            {
                long NewCpuUsage = (long)CurrentProcess.TotalProcessorTime.TotalMilliseconds;

                double cpu = ((NewCpuUsage - TempProcess.OldCpuUsage) / CpuUsagePercent);
                if (cpu < 0)
                {
                    cpu = -(cpu / 10);
                }
                TempProcess.CpuUsage = cpu;
                TempProcess.OldCpuUsage = NewCpuUsage;
                long ram = CurrentProcess.PrivateMemorySize64;
                //ram = ram / (int)Math.Pow(1024, 1); //KB
                ram = ram / (int)Math.Pow(1024, 2); //MB
                TempProcess.PrivateMemorySize64 = ram;
            }

            return TempProcess;
        }

        private static bool ProcessInfoExists(Process NewProcess)
        {
            // checks if the process info is already loaded
            if (ProcessList == null) return false;

            foreach (ProcessInfo TempProcess in ProcessList)
                if (TempProcess != PROCESS_INFO_NOT_FOUND && TempProcess.ID == NewProcess.Id)
                    return true;

            return false;
        }

        private static void AddNewProcess(Process NewProcess)
        {
            // loads a new process
            ProcessInfo NewProcessInfo = new ProcessInfo();

            NewProcessInfo.Name = NewProcess.ProcessName;
            NewProcessInfo.ID = NewProcess.Id;

            ProcessList[ProcessIndex++] = GetProcessInfo(NewProcessInfo, NewProcess);
        }
    }

    public class ProcessInfo
    {
        public string Name { get; set; }
        public double CpuUsage { get; set; }
        public int ID { get; set; }
        public long OldCpuUsage { get; set; }
        public long PrivateMemorySize64 { get; set; }
    }
}

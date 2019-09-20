using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace service_performance
{
    public class InfoData
    {
        public string ServerName { get; set; }
        public float HDDFreePercent { get; set; }
        public float HDDFreeMB { get; set; }
        public bool HDDHigh { get; set; }
        public float RAMFreeMB { get; set; }
        public bool RAMHigh { get; set; }
        public float CPUUsagePercent { get; set; }
        public bool CPUHigh { get; set; }
        public DateTime? RabbitDate { get; set; }

        public List<ProcessInfo> ListDetails { get; set; }
    }
}

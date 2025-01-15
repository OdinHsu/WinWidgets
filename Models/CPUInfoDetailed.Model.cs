using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WidgetsDotNet.Models
{
    internal class CPUInfoDetailed
    {
        public string Name { get; set; }
        public float CPUUsage { get; set; }
        public float MaxCoreUsage { get; set; }
        public int Cores { get; set; }
        public int Threads { get; set; }
        public float MaxTemperature { get; set; }
        public float PackageTemperature { get; set; }
        public float AverageTemperature { get; set; }
        public float CPUVoltage { get; set; }
        public float PackagePower { get; set; }
        public float CoresPower { get; set; }
        public float? BusSpeed { get; set; }
        public Dictionary<string, float> CoreLoad { get; set; }
        public Dictionary<string, float> CoreTemperature { get; set; }
        public Dictionary<string, float> CoreVoltage { get; set; }
        public Dictionary<string, float> CoreClock { get; set; }
    }

}

using System.IO;
using System.Windows.Forms;
using HardwareInfoDll;  // 引用 C++/CLI DLL

namespace Services
{
    internal class HardwareService
    {
        /// <summary>
        /// Get battery level in percentage
        /// </summary>
        /// <returns>Battery level in percent</returns>
        public string GetBatteryLevelPercentage()
        {
            PowerStatus powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
            return powerStatus.BatteryLifePercent.ToString("P0"); // 格式化為百分比
        }

        /// <summary>
        /// Get CPU information (including usage, cores, threads, etc.)
        /// </summary>
        /// <returns>CPU info in JSON format</returns>
        public string GetCPUInfo(HardwareInfo hardwareInfo)
        {
            return hardwareInfo.GetCPUInfo();
        }

        /// <summary>
        /// Get all GPU information (including usage, memory, temperature, etc.)
        /// </summary>
        /// <returns>GPU info in JSON format</returns>
        public string GetAllGPUInfo(HardwareInfo hardwareInfo)
        {
            return hardwareInfo.GetGPUInfo();
        }

        /// <summary>
        /// Get all Ram information (including usage, memory, etc.)
        /// </summary>
        /// <return>RAM size in bytes</return>
        public string GetRAMInfo(HardwareInfo hardwareInfo)
        {
            return hardwareInfo.GetMemoryInfo();
        }

        /// <summary>
        /// Get disk information (including usage, memory, etc.)
        /// </summary>
        /// <return>Disk size in bytes</return>
        public string GetDiskInfo(HardwareInfo hardwareInfo)
        {
            return hardwareInfo.GetStorageInfo();
        }

        /// <summary>
        /// Get network information (including usage, memory, etc.)
        /// </summary>
        /// <return>Disk size in bytes</return>
        public string GetNetworkInfo(HardwareInfo hardwareInfo)
        {
            return hardwareInfo.GetNetworkInfo();
        }
    }
}
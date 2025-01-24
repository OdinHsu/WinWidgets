using System;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using NativeWifi; // 確保引用 NativeWifi 套件
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
        /// 取得目前正在使用的網路名稱。
        /// </summary>
        /// <returns>返回正在使用的網路名稱，若無則返回空字串。</returns>
        static private string SpeedInit() // 此方法為自動取得正在連線中網路介面
        {
            NetworkInterface[] netInterfaceAry = NetworkInterface.GetAllNetworkInterfaces();
            WlanClient wlanClient = new WlanClient();
            int count = 0; // 初始化計數器

            foreach (NetworkInterface netInterface in netInterfaceAry)
            {
                if (netInterface.Name == wlanClient.Interfaces[0].InterfaceName)
                {
                    return netInterface.Name;
                }
                count++;
            }

            return null;
        }

        /// <summary>
        /// Get network information (including usage, memory, etc.)
        /// </summary>
        /// <return>Disk size in bytes</return>
        public string GetNetworkInfo(HardwareInfo hardwareInfo)
        {
            string networkInfoJson = hardwareInfo.GetNetworkInfo();
            //string networkName = SpeedInit();

            // 解析 JSON 字串
            JObject json = JObject.Parse(networkInfoJson);

            //json["currentNetwork"] = networkName;

            // 返回更新後的 JSON 字串
            return json.ToString();
        }
    }
}
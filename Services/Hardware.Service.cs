using System;
using System.Net.NetworkInformation;
using Newtonsoft.Json.Linq;
using NativeWifi; // 確保引用 NativeWifi 套件
using System.Windows.Forms;
using System.Linq;
using HardwareInfoDll;  // 引用 C++/CLI DLL

namespace Services
{
    internal class HardwareService
    {
        private static string _cachedInterfaceName;
        private static DateTime _lastUpdated;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(5);

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
        /// 智能識別主要網路介面卡
        /// </summary>
        private static string GetActiveNetworkInterface()
        {
            if (DateTime.Now - _lastUpdated < CacheExpiry && !string.IsNullOrEmpty(_cachedInterfaceName))
            {
                return _cachedInterfaceName;
            }

            string result = null;

            // 方案 1: 優先識別有實際流量的介面
            var activeByTraffic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .OrderByDescending(ni => ni.GetIPv4Statistics().BytesReceived + ni.GetIPv4Statistics().BytesSent)
                .FirstOrDefault();

            // 方案 2: 確認無線介面連接狀態
            if (activeByTraffic?.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                try
                {
                    var wlanClient = new WlanClient(); // 不需要 using
                    foreach (var wlanInterface in wlanClient.Interfaces)
                    {
                        if (wlanInterface.InterfaceState == Wlan.WlanInterfaceState.Connected)
                        {
                            result = activeByTraffic.Name;
                            break;
                        }
                    }
                }
                catch { /* 忽略例外 */ }
            }

            // 方案 3: 最終回退
            if (result == null)
            {
                result = activeByTraffic?.Name;
            }

            // 更新快取
            _cachedInterfaceName = result ?? "No active interface";
            _lastUpdated = DateTime.Now;

            return _cachedInterfaceName;
        }

        /// <summary>
        /// 獲取網路資訊（包含介面卡名稱）
        /// </summary>
        public string GetNetworkInfo(HardwareInfo hardwareInfo)
        {
            string networkInfoJson = hardwareInfo.GetNetworkInfo();
            string interfaceName = GetActiveNetworkInterface();

            JObject json = JObject.Parse(networkInfoJson);
            json["currentNetwork"] = interfaceName;

            return json.ToString();
        }
    }
}
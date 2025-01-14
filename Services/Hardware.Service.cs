using Newtonsoft.Json;
using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using OpenHardwareMonitor.Hardware;
using System.Collections.Generic;

namespace Services
{
    public class CPUInfo
    {
        public string Name { get; set; }
        public int Cores { get; set; }
        public int Threads { get; set; }
        public string ClockSpeed { get; set; }
        public float CPUUsage { get; set; }
        public float[] CoreUsages { get; set; }
    }

    internal class HardwareService
    {
        private readonly Computer computer;

        public HardwareService()
        {
            computer = new Computer
            {
                CPUEnabled = true
            };
            computer.Open();
        }

        ~HardwareService()
        {
            computer.Close();
        }

        /// <summary>
        /// Get battery level in percentage
        /// </summary>
        /// <returns>Battery level in percent</returns>
        public string GetBatteryLevelPercentage()
        {
            PowerStatus powerStatus = SystemInformation.PowerStatus;
            return powerStatus.BatteryLifePercent.ToString();
        }

        /// <summary>
        /// Gets the amount of free space available in a specific drive
        /// </summary>
        /// <param name="driveLetter">Letter(s) of the desired drive</param>
        /// <returns>Space represented in bytes</returns>
        public long GetFreeSpaceAvailableInDrive(string driveLetter)
        {
            return (new DriveInfo(driveLetter)).AvailableFreeSpace;
        }

        /// <summary>
        /// Calls the WidgetsDotNetCore DLL and checks if any application using DirectX is fullscreen
        /// </summary>
        /// <returns>Whether any DirectX application is fullscreen</returns>
        [DllImport(@"WidgetsDotNetCore.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool isAnyApplicationFullscreen();

        /// <summary>
        /// Get CPU information (including usage, cores, threads, etc.)
        /// </summary>
        /// <returns>CPU info in JSON format</returns>
        public string GetCPUInfo()
        {
            var cpuInfo = new CPUInfo();

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    hardware.Update();

                    // 獲取 CPU 名稱
                    cpuInfo.Name = hardware.Name;

                    // 儲存核心使用率的臨時列表
                    var coreUsageList = new List<float>();

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        // 獲取 CPU 總使用率
                        if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                        {
                            cpuInfo.CPUUsage = sensor.Value.GetValueOrDefault();
                        }

                        // 獲取每個核心的使用率
                        if (sensor.SensorType == SensorType.Load && sensor.Name.StartsWith("CPU Core"))
                        {
                            coreUsageList.Add(sensor.Value.GetValueOrDefault());
                        }
                    }

                    // 更新核心使用率和核心數
                    cpuInfo.CoreUsages = coreUsageList.ToArray();
                    cpuInfo.Cores = coreUsageList.Count;

                    // 更新執行緒數（假設每個核心有兩個執行緒，這是超執行緒技術的常見情況）
                    cpuInfo.Threads = cpuInfo.Cores * 2;

                    // 設定假定的時脈速度（可以透過其他方式進一步查詢）
                    cpuInfo.ClockSpeed = "Unknown";
                }
            }

            return JsonConvert.SerializeObject(cpuInfo);
        }

    }
}

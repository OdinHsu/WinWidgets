using Newtonsoft.Json;
using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using Models;
using WidgetsDotNet.Models;
using System.Linq;
using System.Text.RegularExpressions;

namespace Services
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this); // 遍歷所有硬體
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update(); // 更新硬體資訊
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this); // 也遍歷子硬體
            }
        }

        public void VisitSensor(ISensor sensor) { } // 無需處理感測器本身
        public void VisitParameter(IParameter parameter) { } // 無需處理參數
    }

    internal class HardwareService
    {
        private readonly Computer computer;

        public HardwareService()
        {
            // 初始化硬體監控工具，啟用 CPU 和 GPU 資訊
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true
            };
            computer.Open();
            computer.Accept(new UpdateVisitor());
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
            PowerStatus powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
            return powerStatus.BatteryLifePercent.ToString("P0"); // 格式化為百分比
        }

        /// <summary>
        /// Gets the amount of free space available in a specific drive
        /// </summary>
        /// <param name="driveLetter">Letter(s) of the desired drive</param>
        /// <returns>Space represented in bytes</returns>
        public long GetFreeSpaceAvailableInDrive(string driveLetter)
        {
            DriveInfo drive = new DriveInfo(driveLetter);
            return drive.AvailableFreeSpace;
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
            var cpuInfo = new CPUInfoDetailed
            {
                CoreLoad = new Dictionary<string, float>(),
                CoreTemperature = new Dictionary<string, float>(),
                CoreVoltage = new Dictionary<string, float>(),
                CoreClock = new Dictionary<string, float>()
            };

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();

                    // 設置 CPU 名稱
                    cpuInfo.Name = hardware.Name;

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        var sensorName = sensor.Name;
                        var sensorValue = sensor.Value;

                        if (!sensorValue.HasValue) continue;

                        // 根據傳感器類型進行分類記錄
                        switch (sensor.SensorType)
                        {
                            case SensorType.Load:
                                if (sensorName == "CPU Total")
                                    cpuInfo.CPUUsage = sensorValue.Value;
                                else if (sensorName.StartsWith("CPU Core Max"))
                                    cpuInfo.MaxCoreUsage = sensorValue.Value;
                                else if (sensorName.StartsWith("CPU Core"))
                                    cpuInfo.CoreLoad[sensorName] = sensorValue.Value;
                                break;

                            case SensorType.Temperature:
                                if (sensorName == "Core Max")
                                    cpuInfo.MaxTemperature = sensorValue.Value;
                                else if (sensorName == "CPU Package")
                                    cpuInfo.PackageTemperature = sensorValue.Value;
                                else if (sensorName == "Core Average")
                                    cpuInfo.AverageTemperature = sensorValue.Value;
                                else if (sensorName.StartsWith("CPU Core"))  // 核心溫度包含TjMax
                                    cpuInfo.CoreTemperature[sensorName] = sensorValue.Value;
                                break;

                            case SensorType.Clock:
                                if (sensorName.StartsWith("CPU Core"))
                                    cpuInfo.CoreClock[sensorName] = sensorValue.Value;
                                else if (sensorName == "Bus Speed")
                                    cpuInfo.BusSpeed = sensorValue.Value;
                                break;

                            case SensorType.Voltage:
                                if (sensorName.StartsWith("CPU Core #"))
                                    cpuInfo.CoreVoltage[sensorName] = sensorValue.Value;
                                else if (sensorName == "CPU Core")
                                    cpuInfo.CPUVoltage = sensorValue.Value;
                                break;

                            case SensorType.Power:
                                if (sensorName == "CPU Package")
                                    cpuInfo.PackagePower = sensorValue.Value;
                                else if (sensorName == "CPU Cores")
                                    cpuInfo.CoresPower = sensorValue.Value;
                                break;
                        }
                    }

                    // 核心與執行緒數計算（假設每核心2執行緒）
                    var filteredKeys = cpuInfo.CoreLoad.Keys
                        .Where(key => key.StartsWith("CPU Core"))
                        .ToList();

                    var uniqueCores = new HashSet<int>();

                    foreach (var key in filteredKeys)
                    {
                        // 從鍵中提取核心編號（例如 "CPU Core #1 Thread #2" 提取 1）
                        var coreNumberMatch = Regex.Match(key, @"CPU Core #(\d+)");
                        if (coreNumberMatch.Success)
                        {
                            int coreNumber = int.Parse(coreNumberMatch.Groups[1].Value);

                            // 添加到集合中以確保唯一性
                            uniqueCores.Add(coreNumber);
                        }
                    }

                    // 設定核心數和執行緒數
                    cpuInfo.Cores = uniqueCores.Count; // 唯一核心數
                    cpuInfo.Threads = filteredKeys.Count; // 鍵的總數（即執行緒數）
                }
            }

            string result = JsonConvert.SerializeObject(cpuInfo, Formatting.Indented);
            return result;
        }

        /// <summary>
        /// Get all GPU information (including usage, memory, temperature, etc.)
        /// </summary>
        /// <returns>GPU info in JSON format</returns>
        public string GetAllGPUInfo()
        {
            var gpuInfoList = new List<Dictionary<string, object>>();

            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuAmd ||
                    hardware.HardwareType == HardwareType.GpuIntel)
                {
                    hardware.Update();

                    var hardwareInfo = new Dictionary<string, object>
                    {
                        { "Hardware", hardware.Name },
                        { "Sensors", new List<Dictionary<string, object>>() }
                    };

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        var sensorInfo = new Dictionary<string, object>
                        {
                            { "SensorName", sensor.Name },
                            { "Value", sensor.Value.GetValueOrDefault() },
                            { "Type", sensor.SensorType.ToString() }
                        };

                        ((List<Dictionary<string, object>>)hardwareInfo["Sensors"]).Add(sensorInfo);
                    }

                    gpuInfoList.Add(hardwareInfo);
                }
            }

            // Convert the result list into JSON format and return
            string result = JsonConvert.SerializeObject(gpuInfoList, Formatting.Indented);
            return result;
        }
    }
}
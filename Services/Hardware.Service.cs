using Newtonsoft.Json;
using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;
using Models;
using WidgetsDotNet.Models;
using System.Linq;
using System.Text.RegularExpressions;
using HardwareInfoDll;  // 引用 C++/CLI DLL

namespace Services
{
    internal class HardwareService
    {
        // 建立 HardwareInfo 物件
        HardwareInfo hardwareInfo;

        public HardwareService()
        {
            // 建立 HardwareInfo 物件
            hardwareInfo = new HardwareInfo();
        }

        ~HardwareService()
        {
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
            return hardwareInfo.GetCPUInfo();
        }

        /// <summary>
        /// Get all GPU information (including usage, memory, temperature, etc.)
        /// </summary>
        /// <returns>GPU info in JSON format</returns>
        public string GetAllGPUInfo()
        {
            return null; // 這裡不需要實作
            var gpuInfoList = new List<Dictionary<string, object>>();

            //foreach (IHardware hardware in computer.Hardware)
            //{
            //    if (hardware.HardwareType == HardwareType.GpuNvidia ||
            //        hardware.HardwareType == HardwareType.GpuAmd ||
            //        hardware.HardwareType == HardwareType.GpuIntel)
            //    {
            //        hardware.Update();

            //        var hardwareInfo = new Dictionary<string, object>
            //        {
            //            { "Hardware", hardware.Name },
            //            { "Sensors", new List<Dictionary<string, object>>() }
            //        };

            //        foreach (ISensor sensor in hardware.Sensors)
            //        {
            //            var sensorInfo = new Dictionary<string, object>
            //            {
            //                { "SensorName", sensor.Name },
            //                { "Value", sensor.Value.GetValueOrDefault() },
            //                { "Type", sensor.SensorType.ToString() }
            //            };

            //            ((List<Dictionary<string, object>>)hardwareInfo["Sensors"]).Add(sensorInfo);
            //        }

            //        gpuInfoList.Add(hardwareInfo);
            //    }
            //}

            //// Convert the result list into JSON format and return
            //string result = JsonConvert.SerializeObject(gpuInfoList, Formatting.Indented);
            //return result;
        }
    }
}
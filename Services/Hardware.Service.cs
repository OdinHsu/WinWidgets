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
            string result = hardwareInfo.GetGPUInfo();
            return result;
        }
    }
}
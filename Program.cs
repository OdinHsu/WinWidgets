using Components;
using Services;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using System.IO;

internal class Program
{
    // 引用必要的 Windows API 函数
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowProc lpEnumFunc, int lParam);

    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lptrString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, ref LPRECT rect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // 枚举窗口时的回调委托
    public delegate bool EnumWindowProc(IntPtr hWnd, int lParam);

    // 窗口矩形结构
    [StructLayout(LayoutKind.Sequential)]
    public struct LPRECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // 用于隐藏或显示窗口的常量
    private const int SW_HIDE = 0; // 隐藏
    private const int SW_RESTORE = 9; // 恢复显示

    [STAThread]
    private static void Main(string[] args)
    {
        bool processExists = System.Diagnostics.Process.GetProcessesByName(
            System.IO.Path.GetFileNameWithoutExtension(
                System.Reflection.Assembly.GetEntryAssembly().Location))
                    .Count() > 1;

        if (!processExists)
        {
            // JSON 檔案路徑
            string json = File.ReadAllText("appsettings.json"); // 讀取 JSON 檔案
            var config = JsonConvert.DeserializeObject<dynamic>(json); // 解析成動態物件
            if (ScreenSettingAPI.GetScreenBounds(config.ScreenDescription.ToString()) != null)
                EnumWindows(OnEnumWindow, 0);  // 隱藏任務欄
            new WidgetsManagerComponent();
        }
    }

    // 遍历系统窗口的回调方法
    private static bool OnEnumWindow(IntPtr hWnd, int lParam)
    {
        StringBuilder className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);

        // 筛选出任务栏窗口（类名包含 "TrayWnd"）
        if (className.ToString().Contains("TrayWnd"))
        {
            // 获取窗口的边界
            LPRECT rect = new LPRECT();
            GetWindowRect(hWnd, ref rect);

            // 打印窗口类名和边界（调试信息）
            Console.WriteLine($"Taskbar Found: hWnd = {hWnd}, Class = {className.ToString()}, Bounds = ({rect.Left}, {rect.Top}, {rect.Right}, {rect.Bottom})");

            // JSON 檔案路徑
            string json = File.ReadAllText("appsettings.json"); // 讀取 JSON 檔案
            var config = JsonConvert.DeserializeObject<dynamic>(json); // 解析成動態物件

            // 获取当前窗口所处的屏幕边界
            var screenBounds = ScreenSettingAPI.GetScreenBounds(config.ScreenDescription.ToString());  // 返回的是元組 (left, top, right, bottom)

            // 判断任务栏是否位于指定屏幕内
            if (rect.Left >= screenBounds.Item1 && rect.Top >= screenBounds.Item2 &&
                rect.Right <= screenBounds.Item3 && rect.Bottom <= screenBounds.Item4)
            {
                // 控制任务栏的显示与隐藏
                Console.WriteLine("Taskbar on the specified screen detected. Hiding taskbar.");
                ShowWindow(hWnd, SW_HIDE);  // 隐藏任务栏
            }
            //ShowWindow(hWnd, SW_RESTORE);  // 可用此行来显示任务栏
        }
        return true;
    }
}

//using System;
//using System.Collections.Generic;
//using LibreHardwareMonitor.Hardware;
//using Newtonsoft.Json;
//using Services;

//public class UpdateVisitor : IVisitor
//{
//    public void VisitComputer(IComputer computer)
//    {
//        computer.Traverse(this);
//    }

//    public void VisitHardware(IHardware hardware)
//    {
//        hardware.Update();
//        foreach (IHardware subHardware in hardware.SubHardware)
//        {
//            subHardware.Accept(this);
//        }
//    }

//    public void VisitSensor(ISensor sensor) { }
//    public void VisitParameter(IParameter parameter) { }
//}

//public class Program
//{
//    private readonly Computer computer;

//    public Program()
//    {
//        computer = new Computer
//        {
//            IsCpuEnabled = true,
//            IsGpuEnabled = true
//        };
//        computer.Open();
//        computer.Accept(new UpdateVisitor());
//    }

//    ~Program()
//    {
//        computer.Close();
//    }

//    public class GPUInfo
//    {
//        public string Name { get; set; }
//        public float GPUUsage { get; set; }
//        public float MemoryUsage { get; set; }
//        public float MemoryTotal { get; set; }
//        public float MemoryFree { get; set; }
//        public float Temperature { get; set; }
//    }

//    public string GetCPUInfo()
//    {
//        var cpuInfo = new CPUInfo();

//        foreach (IHardware hardware in computer.Hardware)
//        {
//            if (hardware.HardwareType == HardwareType.Cpu)
//            {
//                hardware.Update();

//                // 獲取 CPU 名稱
//                cpuInfo.Name = hardware.Name;

//                var coreUsageList = new List<float>();

//                foreach (ISensor sensor in hardware.Sensors)
//                {
//                    var sensorName = sensor.Name;
//                    var sensorValue = sensor.Value.GetValueOrDefault();
//                    var sensorType = sensor.SensorType;

//                    //Console.WriteLine($"Sensor Name: {sensorName}, Sensor Value: {sensorValue}, Sensor Type: {sensorType}");

//                    // 獲取 CPU 總使用率
//                    if (sensor.SensorType == SensorType.Load && sensorName == "CPU Total")
//                    {
//                        cpuInfo.CPUUsage = sensor.Value.GetValueOrDefault();
//                    }

//                    // 獲取每個核心的使用率
//                    if (sensor.SensorType == SensorType.Load && sensorName.StartsWith("CPU Core"))
//                    {
//                        coreUsageList.Add(sensor.Value.GetValueOrDefault());
//                    }
//                }

//                // 更新核心使用率和核心數
//                cpuInfo.CoreUsages = coreUsageList.ToArray();
//                cpuInfo.Cores = coreUsageList.Count;

//                // 更新執行緒數（假設每個核心有兩個執行緒，這是超執行緒技術的常見情況）
//                cpuInfo.Threads = cpuInfo.Cores * 2;

//                // 設定假定的時脈速度（可以透過其他方式進一步查詢）
//                cpuInfo.ClockSpeed = "Unknown";
//            }
//        }

//        foreach (IHardware hardware in computer.Hardware)
//        {
//            Console.WriteLine("Hardware: {0}", hardware.Name);

//            foreach (IHardware subhardware in hardware.SubHardware)
//            {
//                Console.WriteLine("\tSubhardware: {0}", subhardware.Name);

//                foreach (ISensor sensor in subhardware.Sensors)
//                {
//                    Console.WriteLine("\t\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
//                }
//            }

//            foreach (ISensor sensor in hardware.Sensors)
//            {
//                Console.WriteLine("\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
//            }
//        }

//        string result = JsonConvert.SerializeObject(cpuInfo, Formatting.Indented);
//        return result;
//    }

//    public static void Main()
//    {
//        var program = new Program();
//        var result = program.GetCPUInfo();
//        Console.WriteLine(result);
//    }
//}



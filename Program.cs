using Components;
using Services;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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
            // 调用 EnumWindows 函数，遍历系统窗口
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

            // 获取当前窗口所处的屏幕边界
            var screenBounds = ScreenSettingAPI.GetScreenBounds("Racer-Tech USB Display Device");  // 返回的是元組 (left, top, right, bottom)

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


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class ScreenPositionAPI
{
    // 定義 DISPLAYDEVICE 結構
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYDEVICE
    {
        [MarshalAs(UnmanagedType.U4)] public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        [MarshalAs(UnmanagedType.U4)] public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string StateDeviceName;
    }

    // 宣告 EnumDisplayDevices API 函數
    [DllImport("user32.dll")]
    public static extern int EnumDisplayDevices(string lpDevice, int iDevNum, ref DISPLAYDEVICE lpDisplayDevice, int dwFlags);

    /// <summary>
    /// 根據指定的螢幕描述過濾條件，取得其座標範圍。
    /// </summary>
    /// <param name="deviceDescription">指定螢幕的描述 (可為部分關鍵字匹配)。</param>
    /// <returns>螢幕範圍 (左上角和右下角座標) 或 null 如果未找到。</returns>
    public static Tuple<int, int, int, int> GetScreenBounds(string deviceDescription)
    {
        DISPLAYDEVICE displayDevice = new DISPLAYDEVICE();
        displayDevice.cb = Marshal.SizeOf(displayDevice);

        int deviceIndex = 0;
        while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0) != 0)
        {
            // 比較描述是否匹配
            if (displayDevice.DeviceString != null &&
                displayDevice.DeviceString.IndexOf(deviceDescription, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Screen screen = GetScreen(displayDevice.DeviceName);
                if (screen != null)
                {
                    return Tuple.Create(
                        screen.Bounds.Left,
                        screen.Bounds.Top,
                        screen.Bounds.Right,
                        screen.Bounds.Bottom
                    );
                }
            }
            deviceIndex++;
        }
        return null;
    }

    /// <summary>
    /// 列出所有顯示器的資訊，包括名稱、描述和範圍。
    /// </summary>
    /// <returns>顯示器的列表，每個包含名稱、描述與邊界資訊。</returns>
    public static List<Tuple<string, string, int, int, int, int>> ListAllScreens()
    {
        List<Tuple<string, string, int, int, int, int>> screens = new List<Tuple<string, string, int, int, int, int>>();
        DISPLAYDEVICE displayDevice = new DISPLAYDEVICE();
        displayDevice.cb = Marshal.SizeOf(displayDevice);

        int deviceIndex = 0;
        while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0) != 0)
        {
            Screen screen = GetScreen(displayDevice.DeviceName);
            if (screen != null)
            {
                screens.Add(Tuple.Create(
                    displayDevice.DeviceName,
                    displayDevice.DeviceString,
                    screen.Bounds.Left,
                    screen.Bounds.Top,
                    screen.Bounds.Right,
                    screen.Bounds.Bottom
                ));
            }
            deviceIndex++;
        }
        return screens;
    }

    // 根據設備名稱獲取對應的螢幕
    private static Screen GetScreen(string deviceName)
    {
        foreach (Screen screen in Screen.AllScreens)
        {
            if (screen.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return screen;
            }
        }
        return null;
    }
}

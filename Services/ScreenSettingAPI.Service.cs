using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Services
{
    public class ScreenSettingAPI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYDEVICE
        {
            [MarshalAs(UnmanagedType.U4)] public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            [MarshalAs(UnmanagedType.U4)] public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string StateDeviceName;
        }

        [DllImport("user32.dll")]
        public static extern int EnumDisplayDevices(string lpDevice, int iDevNum, ref DISPLAYDEVICE lpDisplayDevice, int dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public static Tuple<int, int, int, int> GetScreenBounds(string deviceDescription)
        {
            DISPLAYDEVICE displayDevice = new DISPLAYDEVICE();
            displayDevice.cb = Marshal.SizeOf(displayDevice);

            int deviceIndex = 0;
            while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, 0) != 0)
            {
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
}
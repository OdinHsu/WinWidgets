using CefSharp;
using CefSharp.WinForms;
using Models;
using Modules;
using Newtonsoft.Json;
using Services;
using System;
using System.Drawing;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;

namespace Components
{
    internal class WidgetComponent : WidgetModel
    {
        public bool moveModeEnabled = false;
        private bool isTopMost = false;

        private IntPtr _handle;
        private string _widgetPath;
        private WidgetForm _window;
        private ChromiumWebBrowser _browser;
        private Configuration _configuration;
        private int width;
        private int height;
        private HTMLDocService htmlDocService = new HTMLDocService();
        private WindowService windowService = new WindowService();
        private WidgetService widgetService = new WidgetService();
        private TimerService timerService = new TimerService();

        private DateTime _lastSaveTime = DateTime.MinValue;

        private Tuple<int, int, int, int> screenBounds;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOACTIVATE = 0x0010;
        public string attribute { get; set; }
        public int id {  get; set; }
        public override IntPtr handle
        {
            get { return _handle; }
            set { _handle = value; }
        }

        public override string htmlPath
        {
            get { return _widgetPath; }
            set { _widgetPath = value; }
        }

        public override WidgetForm window
        {
            get { return _window; }
            set { _window = value; }
        }

        public override ChromiumWebBrowser browser
        {
            get { return _browser; }
            set { _browser = value; }
        }

        public override Configuration configuration
        {
            get { return _configuration; }
            set { _configuration = value; }
        }

        public override void AppendWidget(Form window, string path)
        {
            browser = new ChromiumWebBrowser(path);
            browser.IsBrowserInitializedChanged += OnBrowserInitialized;
            window.Controls.Add(browser);
        }

        // (快取配置)
        private static Lazy<dynamic> _cachedConfig = new Lazy<dynamic>(() =>
        {
            string json = File.ReadAllText("appsettings.json");
            return JsonConvert.DeserializeObject<dynamic>(json);
        });

        public WidgetComponent()
        {
            var config = _cachedConfig.Value; // 解析成動態物件
            screenBounds = ScreenSettingAPI.GetScreenBounds(config.ScreenDescription.ToString());
            if (screenBounds == null)
            {
                // 获取主屏幕的边界
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                var bounds = screen.Bounds;
                // 转换为 (x1, y1, x2, y2) 格式
                int x1 = bounds.Left;    // 左上角 X 坐标
                int y1 = bounds.Top;     // 左上角 Y 坐标
                int x2 = bounds.Right;   // 右下角 X 坐标
                int y2 = bounds.Bottom;  // 右下角 Y 坐标
                // 存储为 Tuple 或自定义结构
                screenBounds = new Tuple<int, int, int, int>(x1, y1, x2, y2);
            }
        }

        public override void CreateWindow(int width, int height, string title, bool save, Point position = default(Point), bool? alwaysOnTop = null)
        {
            new Thread(() =>
            {
                POINT mousePos;
                GetCursorPos(out mousePos);


                var metaTags = htmlDocService.GetAllMetaTags(htmlPath);
                string appTitle = metaTags.TryGetValue("applicationTitle", out var titles) ? titles : null;

                attribute = appTitle;

                string sizeString = metaTags.TryGetValue("windowSize", out var size) ? size : null;
                string radiusString = metaTags.TryGetValue("windowBorderRadius", out var radius) ? radius : null;
                string locationString = metaTags.TryGetValue("windowLocation", out var location) ? location : null;
                string topMostString = metaTags.TryGetValue("topMost", out var topMostSt) ? topMostSt : null;
                string opacityString = metaTags.TryGetValue("windowOpacity", out var opacitySt) ? opacitySt : null;
                int roundess = radiusString != null ? int.Parse(radiusString) : 0;
                this.width = sizeString != null ? int.Parse(sizeString.Split(' ')[0]) : width;
                this.height = sizeString != null ? int.Parse(sizeString.Split(' ')[1]) : height;
                int locationX = locationString != null ? int.Parse(locationString.Split(' ')[0]) : mousePos.X;
                int locationY = locationString != null ? int.Parse(locationString.Split(' ')[1]) : mousePos.Y;
                byte opacity = (byte)(opacityString != null ? byte.Parse(opacityString.Split(' ')[0]) : 255);
                bool topMost = topMostString != null ? bool.Parse(topMostString.Split(' ')[0]) : false;
                topMost = alwaysOnTop.HasValue ? (bool)alwaysOnTop : topMost;

                var bounds = screenBounds;

                if (position == default(Point) && bounds != null)
                    position = new Point(bounds.Item1, bounds.Item2);

                if (appTitle == "background")
                {
                    position = new Point(bounds.Item1, bounds.Item2);
                    this.width = bounds.Item3 - bounds.Item1 + 1;
                    this.height = bounds.Item4 - bounds.Item2 + 1;
                }

                window = new WidgetForm();
                window.Size = new Size(this.width, this.height);
                window.StartPosition = FormStartPosition.Manual;
                window.Location = locationString == null ? new Point(position.X, position.Y) : new Point(locationX, locationY);
                window.Text = title;
                // window.TopMost = topMost; delayed, because it causes a FormActivate event to be dispatched prematurely
                window.FormBorderStyle = FormBorderStyle.None;
                window.ShowInTaskbar = false;
                window.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, this.width, this.height, roundess, roundess)); // Border radius
                window.Activated += OnFormActivated;
                window.BackColor = Color.Black;

                //this.windowService.SetWindowTransparency(window.Handle, opacity);  // 透明度
                this.windowService.HideWindowFromProgramSwitcher(window.Handle);
                this.configuration = this.widgetService.GetConfiguration(this);
                Configuration config = AssetService.GetConfigurationFile();

                id = config.lastSessionWidgets.Count;

                if (save)
                {
                    this.widgetService.AddOrUpdateSession(htmlPath, new Point(position.X, position.Y), topMost, this.width, this.height, this.id);
                    AssetService.OverwriteConfigurationFile(AssetService.GetConfigurationFile());
                }

                AppendWidget(window, htmlPath);
                window.TopMost = topMost;
                window.isTopMost = topMost;
                window.ShowDialog();
            }).Start();
        }

        private void OnBrowserInitialized(object sender, EventArgs e)
        {
            this.timerService.CreateTimer(33, OnBrowserUpdateTick, true, true);
            if (attribute == "background")
            {
                SetWindowPos(window.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                //this.window.TopMost = true;
            }
            this.widgetService.InjectJavascript(
                this, 
                $"if (typeof onGetConfiguration === 'function') onGetConfiguration({JsonConvert.SerializeObject(configuration.settings)});",
                true
            );
            this.browser.JavascriptMessageReceived += OnBrowserMessageReceived;
        }

        private void UpdateSession()
        {
            this.widgetService.AddOrUpdateSession(this.htmlPath, window.Location, window.TopMost, window.Size.Width, window.Size.Height, this.id);
        }

        private void OnBrowserUpdateTick(object sender, ElapsedEventArgs e)
        {
            // 檢查是否處於移動模式
            if (!this.moveModeEnabled || attribute == "background") return;  // background 不可移動

            // 獲取滑鼠當前位置
            if (GetCursorPos(out POINT pos))
            {
                // 使用 BeginInvoke 確保 UI 操作執行在正確執行緒，使用 BeginInvoke，避免阻塞 UI 執行緒
                window.BeginInvoke(new Action(() =>
                {
                    // 更新視窗位置
                    window.Location = new Point(pos.X - width / 2, pos.Y - height / 2);

                    UpdateSession();
                }));
            }
        }

        private void OnFormActivated(object sender, EventArgs e)
        {
            handle = window.Handle;
            if (browser != null)
            {
                browser.MenuHandler = new MenuHandlerComponent(this);
            }
        }

        private void OnBrowserMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            string message = e.Message.ToString();

            if (message == "disableTopMost")
            {
                window.isTopMost = window.TopMost;  // 紀錄原本的
                window.Invoke(new Action(() => window.TopMost = false));
            }
            else if (message == "enableTopMost")
            {
                POINT mousePos;
                GetCursorPos(out mousePos);

                double windowLeft = window.Left;
                double windowTop = window.Top;
                double windowRight = windowLeft + window.Width;
                double windowBottom = windowTop + window.Height;

                // 判斷滑鼠是否在視窗範圍內
                if (mousePos.X >= windowLeft && mousePos.X <= windowRight &&
                    mousePos.Y >= windowTop && mousePos.Y <= windowBottom)
                {
                    // 滑鼠在範圍內，執行操作
                    window.Invoke(new Action(() => window.TopMost = window.isTopMost));
                }
            }
            else if (message == "background")
            {
                SetWindowPos(window.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                // test
                Console.WriteLine(message);

                // 處理其他消息，例如更新配置
                this.widgetService.SetConfiguration(this, message);
            }
        }
    }
}
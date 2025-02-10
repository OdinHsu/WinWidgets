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
using System.Collections.Generic;
using System.Linq;

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
        private Configuration tempConfig;
        private int width;
        private int height;
        private HTMLDocService htmlDocService = new HTMLDocService();
        private WindowService windowService = new WindowService();
        private WidgetService widgetService = new WidgetService();
        private TimerService timerService = new TimerService();

        private Point previousPosition;

        private DateTime _lastSaveTime = DateTime.MinValue;

        private Tuple<int, int, int, int> screenBounds;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOACTIVATE = 0x0010;
        public string attribute { get; set; }
        public int id { get; set; }

        private int _nextWidgetId = 0;

        private List<ExistingRect> cachedExistingRects;

        private struct ExistingRect
        {
            public Rectangle Rect;
            public int Right;
            public int Bottom;
        }

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

        public override void CreateWindow(int width, int height, string title, bool save, Point position = default(Point), bool? alwaysOnTop = null, int id = -1)
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

                if (id == -1)  // 新創見的widget都是-1
                {
                    foreach (var widget in config.lastSessionWidgets)
                    {
                        // 這裡假設 widget.id 是已分配的唯一值
                        if (widget.id >= _nextWidgetId)
                            _nextWidgetId = widget.id + 1;
                    }
                    this.id = _nextWidgetId++;
                }
                else
                {
                    this.id = id;
                }

                if (save)
                {
                    if (!OverLapHandle() && attribute != "background") return;
                    this.widgetService.AddOrUpdateSession(htmlPath, window.Location, topMost, this.width, this.height, this.id);
                    AssetService.OverwriteConfigurationFile(AssetService.GetConfigurationFile());
                }

                previousPosition = window.Location;

                AppendWidget(window, htmlPath);
                window.TopMost = topMost;
                window.isTopMost = topMost;
                window.ShowDialog();
            }).Start();
        }

        bool OverLapHandle()
        {
            const int OFFSET = 10;  // 统一间距常量
            Configuration config = AssetService.GetConfigurationFile();
            var bounds = screenBounds;
            int left = bounds.Item1, top = bounds.Item2,
                right = bounds.Item3, bottom = bounds.Item4;

            // 预处理：按X坐标排序并缓存矩形右边界
            var existingRects = config.lastSessionWidgets
                .Select(w => new
                {
                    rect = new Rectangle(w.position.X, w.position.Y, w.width, w.height),
                    right = w.position.X + w.width
                })
                .OrderBy(x => x.rect.X)
                .ThenBy(x => x.rect.Y)
                .ToList();

            Rectangle newRect = new Rectangle(
                window.Location.X,
                window.Location.Y,
                this.window.Width,
                this.window.Height
            );

            bool foundPosition = false;
            int maxAttempts = 100;
            int currentRowMaxHeight = newRect.Height;
            int baseLineY = newRect.Y;  // 当前行的基准Y坐标

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 使用预缓存数据加速碰撞检测
                var collision = existingRects
                    .Where(x => x.rect.Y < newRect.Bottom && x.rect.Bottom > newRect.Y)
                    .FirstOrDefault(x => x.right > newRect.X && x.rect.X < newRect.Right);

                if (collision != null)
                {
                    // 找到同一行所有可能碰撞的矩形
                    var sameRowRects = existingRects
                        .Where(x => x.rect.Bottom > baseLineY && x.rect.Y < (baseLineY + currentRowMaxHeight))
                        .ToList();

                    // 计算应移动到的X位置
                    newRect.X = sameRowRects.Max(r => r.right) + OFFSET;

                    // 更新行高（考虑当前行所有矩形）
                    currentRowMaxHeight = sameRowRects.Max(r => r.rect.Height);
                    currentRowMaxHeight = Math.Max(currentRowMaxHeight, newRect.Height);

                    // 换行处理
                    if (newRect.Right > right)
                    {
                        newRect.X = left;
                        baseLineY += currentRowMaxHeight + OFFSET;  // 按实际行高换行
                        newRect.Y = baseLineY;
                        currentRowMaxHeight = newRect.Height;  // 重置行高
                    }
                }
                else
                {
                    // 边界检查优化
                    newRect.Y = Math.Min(newRect.Y, bottom - newRect.Height);
                    if (newRect.Y < top) newRect.Y = top;

                    // 最终确认位置有效性
                    bool validPosition = existingRects.All(x => !x.rect.IntersectsWith(newRect)) &&
                                        newRect.Bottom <= bottom &&
                                        newRect.Right <= right;

                    if (validPosition)
                    {
                        foundPosition = true;
                        break;
                    }
                    else
                    {
                        // 若失败则强制换行
                        baseLineY += currentRowMaxHeight + OFFSET;
                        newRect.X = left;
                        newRect.Y = baseLineY;
                        currentRowMaxHeight = newRect.Height;
                    }
                }
            }

            if (!foundPosition)
            {
                return false;
            }

            window.Location = new Point(newRect.X, newRect.Y);
            return true;
        }

        private void OnBrowserInitialized(object sender, EventArgs e)
        {
            this.timerService.CreateTimer(16, OnBrowserUpdateTick, true, true);
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

        private void OnBrowserUpdateTick(object sender, ElapsedEventArgs e)
        {
            if (!this.moveModeEnabled || attribute == "background") return;

            if (GetCursorPos(out POINT pos))
            {
                window.BeginInvoke(new Action(() =>
                {
                    Rectangle newRect = new Rectangle(pos.X - window.Width / 2, pos.Y - window.Height / 2, window.Width, window.Height);
                    bool collisionHandled = false;

                    foreach (WidgetComponent widget in AssetService.widgets.Widgets)
                    {
                        if (this.id == widget.id || widget.htmlPath.Contains("background.html"))
                            continue;

                        int Xpos = widget.window.Location.X;
                        int Ypos = widget.window.Location.Y;
                        Rectangle otherRect = new Rectangle(Xpos, Ypos, widget.width, widget.height);

                        if (newRect.IntersectsWith(otherRect))
                        {
                            Point previousCenter = new Point(previousPosition.X + window.Width / 2, previousPosition.Y + window.Height / 2);
                            Point widgetCenter = new Point(Xpos + widget.width / 2, Ypos + widget.height / 2);

                            bool isHorizontal = Math.Abs(previousCenter.X - widgetCenter.X) > Math.Abs(previousCenter.Y - widgetCenter.Y);
                            bool pushOther = false;
                            Rectangle overlap = Rectangle.Intersect(newRect, otherRect);

                            if (isHorizontal)
                            {
                                // 水平碰撞處理
                                if (overlap.Width >= widget.width * 0.8)
                                {
                                    pushOther = true;
                                    if (previousCenter.X < widgetCenter.X)
                                    { // 從左側推開
                                        widget.window.Location = new Point(Xpos - widget.window.Width, Ypos);
                                        window.Location = new Point(otherRect.X, window.Location.Y);
                                    }
                                    else
                                    {                                  // 從右側推開
                                        widget.window.Location = new Point(otherRect.Right, Ypos);
                                        window.Location = new Point(otherRect.X, window.Location.Y);
                                    }
                                }
                                else  // 不足一半時調整自己
                                {
                                    newRect.X = previousCenter.X < widgetCenter.X
                                        ? Xpos - newRect.Width
                                        : Xpos + widget.width;
                                }
                            }
                            else
                            {
                                // 垂直碰撞處理
                                if (overlap.Height >= widget.height * 0.8)
                                {
                                    pushOther = true;
                                    if (previousCenter.Y < widgetCenter.Y)
                                    { // 從上方推開
                                        widget.window.Location = new Point(Xpos, otherRect.Top - otherRect.Height);
                                        window.Location = new Point(window.Location.X, Ypos);
                                    }
                                    else
                                    {                                  // 從下方推開
                                        widget.window.Location = new Point(Xpos, otherRect.Bottom);
                                        window.Location = new Point(window.Location.X, Ypos);
                                    }
                                }
                                else  // 不足一半時調整自己
                                {
                                    newRect.Y = previousCenter.Y < widgetCenter.Y
                                        ? Ypos - newRect.Height
                                        : Ypos + widget.height;
                                }
                            }

                            if (pushOther)
                            {
                                // 更新被推開的widget狀態
                                widget.previousPosition = widget.window.Location;
                                widget.widgetService.AddOrUpdateSession(
                                    widget.htmlPath,
                                    widget.window.Location,
                                    widget.window.TopMost,
                                    widget.width,
                                    widget.height,
                                    widget.id
                                );
                                collisionHandled = true;
                            }
                            else
                            {
                                // 更新自身位置並退出
                                window.Location = new Point(newRect.X, newRect.Y);
                                previousPosition = window.Location;
                                this.widgetService.AddOrUpdateSession(
                                    this.htmlPath,
                                    window.Location,
                                    window.TopMost,
                                    window.Width,
                                    window.Height,
                                    this.id
                                );
                                return;
                            }
                        }
                    }

                    // 若發生碰撞且已處理，或完全無碰撞時更新位置
                    if (!collisionHandled)
                    {
                        window.Location = new Point(pos.X - window.Width / 2, pos.Y - window.Height / 2);
                        previousPosition = window.Location;
                        this.widgetService.AddOrUpdateSession(
                            this.htmlPath,
                            window.Location,
                            window.TopMost,
                            window.Width,
                            window.Height,
                            this.id
                        );
                    }
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
                // 處理其他消息，例如更新配置
                this.widgetService.SetConfiguration(this, message);
            }
        }
    }
}
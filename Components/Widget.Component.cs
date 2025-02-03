﻿using CefSharp;
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

        public override void CreateWindow(int width, int height, string title, bool save, Point position = default(Point), bool? alwaysOnTop = null)
        {
            new Thread(() =>
            {
                POINT mousePos;
                GetCursorPos(out mousePos);

                // 使用時直接取用
                var config = _cachedConfig.Value;

                var bounds = ScreenSettingAPI.GetScreenBounds(config.ScreenDescription.ToString());  // 返回的是元組 (left, top, right, bottom)
                if (position == default(Point) && bounds != null)
                {
                    position = new Point(bounds.Item1+50, bounds.Item2+50);
                }

                var metaTags = htmlDocService.GetAllMetaTags(htmlPath);
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

                this.windowService.SetWindowTransparency(window.Handle, opacity);
                this.windowService.HideWindowFromProgramSwitcher(window.Handle);
                this.configuration = this.widgetService.GetConfiguration(this);

                if (save)
                {
                    this.widgetService.AddOrUpdateSession(htmlPath, new Point(locationX, locationY), topMost);
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
            this.widgetService.InjectJavascript(
                this, 
                $"if (typeof onGetConfiguration === 'function') onGetConfiguration({JsonConvert.SerializeObject(configuration.settings)});",
                true
            );
            this.browser.JavascriptMessageReceived += OnBrowserMessageReceived;
        }

        private void UpdateSession()
        {
            if ((DateTime.Now - _lastSaveTime).TotalMilliseconds < 500) return;
            _lastSaveTime = DateTime.Now;
            // 更新當前視窗的 session 配置
            this.widgetService.AddOrUpdateSession(this.htmlPath, window.Location, window.TopMost);
        }

        private void OnBrowserUpdateTick(object sender, ElapsedEventArgs e)
        {
            // 檢查是否處於移動模式
            if (!this.moveModeEnabled) return;

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
            else
            {
                // 處理其他消息，例如更新配置
                this.widgetService.SetConfiguration(this, message);
            }
        }
    }
}
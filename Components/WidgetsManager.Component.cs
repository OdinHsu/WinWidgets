using CefSharp;
using CefSharp.WinForms;
using Hooks;
using Microsoft.Win32;
using Models;
using Modules;
using Newtonsoft.Json;
using Service;
using Services;
using Snippets;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using WidgetsDotNet.Properties;
using WidgetsDotNet.Services;

namespace Components
{
    internal class WidgetsManagerComponent : WidgetManagerModel
    {
        private string _htmlPath = String.Empty;
        private WidgetForm _window;
        private ChromiumWebBrowser _browser;
        private IntPtr _handle;
        private Configuration _configuration;
        private string managerUIPath = AssetService.assetsPath + "/index.html";
        private RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        private NotifyIcon notifyIcon;
        private FormService formService = new FormService();
        private HTMLDocService HTMLDocService = new HTMLDocService();
        private TemplateService templateService = new TemplateService();
        private WidgetService widgetService = new WidgetService();
        private TimerService timerService = new TimerService();
        private WidgetManager widgetManager = new WidgetManager();

        private Tuple<int, int, int, int> screenBounds;

        // 使用 Timer 合併短時間內的多次文件變更事件，避免頻繁觸發 ReloadWidgets
        private System.Timers.Timer _debounceTimer;

        public override string htmlPath 
        { 
            get { return _htmlPath; }
            set { _htmlPath = value; }
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

        public override IntPtr handle
        {
            get { return _handle; }
            set { _handle = value; }
        }

        public override Configuration configuration
        {
            get { return _configuration; }
            set { _configuration = value; }
        }

        public WidgetsManagerComponent()
        {
            CefSettings options = new CefSettings();

            // 允许本地文件访问
            options.CefCommandLineArgs.Add("allow-file-access-from-files", "1");
            options.CefCommandLineArgs.Add("allow-universal-access-from-files");

            // 禁用安全策略
            options.CefCommandLineArgs.Add("disable-web-security", "1");

            options.CefCommandLineArgs.Add("enable-media-stream", "1");
            options.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";
            Cef.Initialize(options);

            string json = File.ReadAllText("appsettings.json"); // 讀取 JSON 檔案
            var config = JsonConvert.DeserializeObject<dynamic>(json); // 解析成動態物件
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

            AssetService.CreateHTMLFilesDirectory();

            this.templateService.MoveTemplatesToWidgetsPath();
            this.configuration = AssetService.GetConfigurationFile();

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Resources.icon64;
            notifyIcon.Text = "WinWidgets";
            notifyIcon.Visible = true;
            notifyIcon.ContextMenu = new ContextMenu(new MenuItem[]
            { new MenuItem("Open Manager", OnOpenApplication),
              new MenuItem("Stop All Widgets", OnStopAllWidgets),
              new MenuItem("-"),
              new MenuItem("Quit", delegate { Application.Exit(); })
            });
            notifyIcon.MouseDoubleClick += NotifyIconDoubleClick;

            Rectangle screenResolution = Screen.PrimaryScreen.Bounds;
            Console.WriteLine(screenResolution.Width + " " + screenResolution.Height);
            int width = (int)(screenResolution.Width * 1);
            int height = (int)(screenResolution.Height * 1);

            CreateWindow(width, height, "WinWidgets", false);
        }

        public override void CreateWindow(int width, int height, string title, bool save, Point position = default(Point), bool? alwaysOnTop = null)
        {
            window = new WidgetForm(false);
            window.Size = new Size(width, height);
            window.StartPosition = FormStartPosition.CenterScreen;
            window.Text = title;
            window.Activated += delegate { handle = window.Handle; };
            window.Icon = Resources.icon64;
            window.Resize += OnFormResized;
            window.ShowInTaskbar = false;
            AppendWidget(window, managerUIPath);
            window.ShowDialog();
        }

        public override void AppendWidget(Form f, string path)
        {
            browser = new ChromiumWebBrowser(path);
            browser.JavascriptMessageReceived += OnBrowserMessageReceived;
            browser.IsBrowserInitializedChanged += OnBrowserInitialized;
            browser.MenuHandler = new WidgetManagerMenuHandler();
            f.Controls.Add(browser);
        }

        private void ReloadWidgets()
        {
            var templateBuilder = new StringBuilder(1024);
            templateBuilder.AppendLine("var container = document.getElementById('widgets');");
            templateBuilder.AppendLine("container.innerHTML = '';");
            templateBuilder.AppendLine("setVersion('" + this.configuration.version + "');");
            templateBuilder.AppendLine("document.getElementById('folder').onclick = () => CefSharp.PostMessage('widgetsFolder');");
            templateBuilder.AppendLine("var switches = document.getElementsByClassName('switch');");
            templateBuilder.AppendLine("for (let s of switches) {");
            templateBuilder.AppendLine("const setting = s.getAttribute('setting');");
            templateBuilder.AppendLine("if (setting == 'startup') {");
            templateBuilder.AppendLine(registryKey.GetValue("WinWidgets") != null ? "s.classList.add('switchon');" : "");
            templateBuilder.AppendLine("}");
            templateBuilder.AppendLine("else if (setting == 'widgetStartup') {");
            templateBuilder.AppendLine(AssetService.GetConfigurationFile().isWidgetAutostartEnabled ? "s.classList.add('switchon');" : "");
            templateBuilder.AppendLine("}");
            templateBuilder.AppendLine("else if (setting == 'widgetHideOnFullscreenApplication') {");
            templateBuilder.AppendLine(AssetService.GetConfigurationFile().isWidgetFullscreenHideEnabled ? "s.classList.add('switchon');" : "");
            templateBuilder.AppendLine("}");
            templateBuilder.AppendLine("else if (setting == 'managerHideOnStart') {");
            templateBuilder.AppendLine(AssetService.GetConfigurationFile().hideWidgetManagerOnStartup ? "s.classList.add('switchon');" : "");
            templateBuilder.AppendLine("}}");

            string[] files = AssetService.GetPathToHTMLFiles(AssetService.widgetsPath);

            for (int i = 0; i < files.Length; i++)
            {
                _htmlPath = files[i];
                string localWidgetPath = _htmlPath.Replace('\\', '/');
                templateBuilder.AppendLine($@"
                    var e{i} = document.createElement('div');");
                templateBuilder.AppendLine($"e{i}.classList.add('widget');");
                templateBuilder.AppendLine($"e{i}.classList.add('flex-row');");
                templateBuilder.AppendLine($"e{i}.style.width = '{(this.HTMLDocService.GetMetaTagValue("previewSize", _htmlPath) != null ? this.HTMLDocService.GetMetaTagValue("previewSize", _htmlPath).Split(' ')[0] : null)}px';");
                templateBuilder.AppendLine($"e{i}.style.minHeight = '{(this.HTMLDocService.GetMetaTagValue("previewSize", _htmlPath) != null ? this.HTMLDocService.GetMetaTagValue("previewSize", _htmlPath).Split(' ')[1] : null)}px';");
                templateBuilder.AppendLine($"e{i}.setAttribute('name', '{this.HTMLDocService.GetMetaTagValue("applicationTitle", _htmlPath)}');");
                templateBuilder.AppendLine($"e{i}.innerHTML = `<p>{this.HTMLDocService.GetMetaTagValue("applicationTitle", _htmlPath)}</p> <iframe src='file:///{localWidgetPath}'></iframe>`;");
                templateBuilder.AppendLine($"var l{i} = document.createElement('span');");
                templateBuilder.AppendLine($"e{i}.appendChild(l{i});");
                templateBuilder.AppendLine($"l{i}.classList.add('label');");
                templateBuilder.AppendLine($"l{i}.innerText = '{this.HTMLDocService.GetMetaTagValue("applicationTitle", _htmlPath)}';");
                templateBuilder.AppendLine($"document.getElementById('widgets').appendChild(e{i});");
                templateBuilder.AppendLine($"e{i}.onclick = () => CefSharp.PostMessage('{i}');");
            }

            browser.ExecuteScriptAsyncWhenPageLoaded(templateBuilder.ToString());
        }

        public override void OpenWidgets()
        {
            foreach (WidgetConfiguration widgetConfiguration in this.configuration.lastSessionWidgets)
            {
                OpenWidget(widgetConfiguration.path, new Point(
                    widgetConfiguration.position.X, 
                    widgetConfiguration.position.Y
                ),
                widgetConfiguration.alwaysOnTop);
            }
        }

        public override void OpenWidget(int id)
        {
            WidgetComponent widget = new WidgetComponent();
            AssetService.widgets.AddWidget(widget);

            widget.htmlPath = AssetService.GetPathToHTMLFiles(AssetService.widgetsPath)[id];
            widget.CreateWindow(300, 300, $"Widget{id}", true);
        }

        public override void OpenWidget(string path, Point position, bool? alwaysOnTop)
        {
            WidgetComponent widget = new WidgetComponent();
            AssetService.widgets.AddWidget(widget);

            widget.htmlPath = path;
            widget.CreateWindow(300, 300, $"Widget{path}", false, position, alwaysOnTop);
        }

        private void OnStopAllWidgets(object sender, EventArgs e)
        {
            this.widgetService.CloseAllWidgets(true);
        }

        private void OnFormResized(object sender, EventArgs e)
        {
            if (this.formService.FormStateAssert(window, FormWindowState.Minimized))
            {
                this.formService.SetWindowOpacity(this.window, 0);
            }
        }

        private void OnOpenApplication(object sender, EventArgs e)
        {
            this.formService.WakeWindow(this.window);
        }

        private void NotifyIconDoubleClick(object sender, MouseEventArgs e)
        {
            this.formService.WakeWindow(this.window);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _debounceTimer.Stop();  // 先停止 Timer
            _debounceTimer.Start(); // 重新計時，確保最後一次變更後 500ms 再執行
        }

        private void OnBrowserInitialized(object sender, EventArgs e)
        {
            FileSystemWatcher fileWatcher = new FileSystemWatcher(AssetService.widgetsPath);
            fileWatcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            _debounceTimer = new System.Timers.Timer(500); // 500ms 去抖動
            _debounceTimer.AutoReset = false; // 確保 Timer 只觸發一次
            _debounceTimer.Elapsed += (s, et) => ReloadWidgets();

            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Created += OnFileChanged;
            fileWatcher.Deleted += OnFileChanged;
            fileWatcher.Renamed += OnFileChanged;

            fileWatcher.Filter = "*.html";
            fileWatcher.IncludeSubdirectories = true;
            fileWatcher.EnableRaisingEvents = true;

            this.timerService.CreateTimer(1500, OnInitializeHooks, false, true);

            this.browser.BeginInvoke(new Action(delegate
            {
                if (this.configuration.isWidgetAutostartEnabled)
                {
                    OpenWidgets();
                }

                if (this.configuration.hideWidgetManagerOnStartup)
                {
                    this.widgetManager.MinimizeWidgetManager(window);
                }
            }));

            ReloadWidgets();
        }

        private void OnInitializeHooks(object sender, ElapsedEventArgs e)
        {
            browser.BeginInvoke(new Action(delegate
            {
                UserActivityHook userActivityHook = new UserActivityHook();
                userActivityHook.KeyDown += new KeyEventHandler(OnKeyDown);
                userActivityHook.KeyUp += new KeyEventHandler(OnKeyUp);
                userActivityHook.OnMouseActivity += new MouseEventHandler(OnMouseActivity);

                HardwareActivityHook hardwareActivityHook = new HardwareActivityHook();
                hardwareActivityHook.OnCPUInfo += OnCPUInfoChanged;
                hardwareActivityHook.OnGPUInfo += OnGPUInfoChanged;
                hardwareActivityHook.OnRAMInfo += OnRamInfoChanged;
                hardwareActivityHook.OnDiskInfo += OnDiskInfoChanged;
                hardwareActivityHook.OnNetworkInfo += OnNetworkInfoChanged;
            }));
        }

        private void CallJavaScriptFunction(string data, HardwareEvent hardwareEvent)
        {
            for (int i = 0; i < AssetService.widgets.Widgets.Count; i++)
            {
                WidgetComponent widget = (WidgetComponent)AssetService.widgets.Widgets[i];

                switch (hardwareEvent)
                {
                    case HardwareEvent.NativeKeys:
                        if (JsonConvert.DeserializeObject<PeripheralAction>(data).buttonPressed == "Left")
                        {
                            widget.moveModeEnabled = false;
                        }

                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeKeyEvents === 'function') { onNativeKeyEvents(" + data + "); }");
                        break;

                    case HardwareEvent.CPUInfo:
                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeCPUInfoEvent === 'function') { onNativeCPUInfoEvent(" + data + "); }");
                        break;
                    case HardwareEvent.GPUInfo:
                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeGPUInfoEvent === 'function') { onNativeGPUInfoEvent(" + data + "); }");
                        break;
                    case HardwareEvent.RAMInfo:
                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeRAMInfoEvent === 'function') { onNativeRAMInfoEvent(" + data + "); }");
                        break;
                    case HardwareEvent.DiskInfo:
                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeDiskInfoEvent === 'function') { onNativeDiskInfoEvent(" + data + "); }");
                        break;
                    case HardwareEvent.NetworkInfo:
                        this.widgetService.InjectJavascript(widget, "if (typeof onNativeNetworkInfoEvent === 'function') { onNativeNetworkInfoEvent(" + data + "); }");
                        break;
                }
            }
        }

        private void OnNetworkInfoChanged(string networkInfo)
        {
            CallJavaScriptFunction(networkInfo, HardwareEvent.NetworkInfo);
        }

        private void OnDiskInfoChanged(string diskInfo)
        {
            CallJavaScriptFunction(diskInfo, HardwareEvent.DiskInfo);
        }

        private void OnRamInfoChanged(string ramInfo)
        {
            CallJavaScriptFunction(ramInfo, HardwareEvent.RAMInfo);
        }

        private void OnGPUInfoChanged(string gpuInfo)
        {
            CallJavaScriptFunction(gpuInfo, HardwareEvent.GPUInfo);
        }

        private void OnCPUInfoChanged(string cpuInfo)
        {
            CallJavaScriptFunction(cpuInfo, HardwareEvent.CPUInfo);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            PeripheralAction action = new PeripheralAction()
            {
                keycode = e.KeyValue.ToString(),
                state = "keyDown",
                eventType = "keyboardEvent",
            };

            CallJavaScriptFunction(JsonConvert.SerializeObject(action), HardwareEvent.NativeKeys);
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            PeripheralAction action = new PeripheralAction()
            {
                keycode = e.KeyValue.ToString(),
                state = "keyUp",
                eventType = "keyboardEvent",
            };

            CallJavaScriptFunction(JsonConvert.SerializeObject(action), HardwareEvent.NativeKeys);
        }

        private void OnMouseActivity(object sender, MouseEventArgs e)
        {
            PeripheralAction action = new PeripheralAction()
            {
                xPosition = e.X.ToString(),
                yPosition = e.Y.ToString(),
                buttonPressed = e.Button.ToString(),
                buttonPressCount = e.Clicks.ToString(),
                mouseWheelOffset = e.Delta.ToString(),
                eventType = "mouseEvent",
            };

            CallJavaScriptFunction(JsonConvert.SerializeObject(action), HardwareEvent.NativeKeys);
        }

        private void OnBrowserMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            switch (e.Message)
            {
                case "widgetsFolder":
                    Process.Start(AssetService.widgetsPath);
                    break;

                case "startup":
                    if (registryKey.GetValue("WinWidgets") == null)
                    {
                        registryKey.SetValue("WinWidgets", Application.ExecutablePath);
                    }
                    else
                    {
                        registryKey.DeleteValue("WinWidgets");
                    }
                    break;

                case "widgetStartup":
                    {
                        Configuration configuration = AssetService.GetConfigurationFile();
                        configuration.isWidgetAutostartEnabled = !configuration.isWidgetAutostartEnabled;
                        AssetService.OverwriteConfigurationFile(configuration);
                    }
                    break;

                case "managerHideOnStart":
                    {
                        Configuration configuration = AssetService.GetConfigurationFile();
                        configuration.hideWidgetManagerOnStartup = !configuration.hideWidgetManagerOnStartup;
                        AssetService.OverwriteConfigurationFile(configuration);
                    }
                    break;

                default:
                    OpenWidget(int.Parse((string)e.Message));
                    break;
            }
        }
    }
}
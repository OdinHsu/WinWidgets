using CefSharp;
using Models;
using Services;
using System;
using System.Runtime.InteropServices;

namespace Components
{
    internal class MenuHandlerComponent : WindowModel, IContextMenuHandler
    {
        private WidgetComponent widgetComponent;
        private MenuHandlerService menuHandlerService = new MenuHandlerService();
        private WidgetService widgetService = new WidgetService();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOACTIVATE = 0x0010;

        public MenuHandlerComponent(WidgetComponent widget)
        {
            this.widgetComponent = widget;
        }

        public void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            this.menuHandlerService.ClearModel(model);
            this.menuHandlerService.AddOption("Move", 0, false, model);
            this.menuHandlerService.AddOption("Always on Top", 1, widgetComponent.window.TopMost, model);
            this.menuHandlerService.AddOption("Close Widget", 2, false, model);
        }

        public bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            if (this.widgetComponent.attribute == "background")
            {
                SetWindowPos(this.widgetComponent.window.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            switch (commandId)
            {
                case 0:
                    this.widgetService.ToggleMove(widgetComponent);
                    return true;

                case (CefMenuCommand)1:
                    if (this.widgetComponent.attribute != "background")
                        this.widgetService.ToggleTopMost(widgetComponent);
                    return true;

                case (CefMenuCommand)2:
                    this.widgetService.CloseWidget(widgetComponent);
                    return true;
            }

            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        { }

        public bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }
    }
}
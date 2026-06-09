using System;
using System.Net.Http;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace APConfigManager.Desktop
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(1280, 800));

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();

                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                WebView.CoreWebView2.NavigationCompleted += (_, _) =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                };

                var devUrl = "http://localhost:5173";
                var prodUrl = "http://localhost:5000";

                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(2);
                    await client.GetAsync(devUrl);
                    WebView.Source = new Uri(devUrl);
                }
                catch
                {
                    WebView.Source = new Uri(prodUrl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView init failed: {ex.Message}");
            }
        }
    }
}

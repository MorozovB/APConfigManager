using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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

            _ = InitAsync();
        }

        private async Task InitAsync()
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

                var url = await DetectUrlAsync();
                WebView.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                File.WriteAllText(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "desktop_error.txt"),
                    ex.ToString());
            }
        }

        private static async Task<string> DetectUrlAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

            try
            {
                await client.GetAsync("http://localhost:5173");
                return "http://localhost:5173";
            }
            catch { }

            for (var i = 0; i < 30; i++)
            {
                try
                {
                    var r = await client.GetAsync("http://localhost:5000");
                    if (r.IsSuccessStatusCode)
                        return "http://localhost:5000";
                }
                catch { }
                await Task.Delay(500);
            }

            return "http://localhost:5000";
        }
    }
}

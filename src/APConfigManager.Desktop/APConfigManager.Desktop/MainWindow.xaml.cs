using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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
        private bool _isActive = true;

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;
        private const uint MB_ICONHAND = 0x00000010;

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

                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                this.Activated += OnWindowActivated;

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

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            _isActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json;
            try { json = e.WebMessageAsJson; }
            catch { return; }

            if (json.Contains("operations-finished") && !_isActive)
            {
                NotifyFinished();
            }
        }

        private void NotifyFinished()
        {
            _ = MessageBeep(MB_ICONHAND);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0,
            };
            _ = FlashWindowEx(ref info);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MessageBeep(uint uType);
    }
}

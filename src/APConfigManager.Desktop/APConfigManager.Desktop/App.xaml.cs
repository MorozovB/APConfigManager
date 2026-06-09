using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace APConfigManager.Desktop
{
    public partial class App : Application
    {
        private Window? _window;
        private Process? _apiProcess;

        public App()
        {
            InitializeComponent();
            UnhandledException += (_, e) =>
            {
                Debug.WriteLine($"Unhandled: {e.Exception}");
                e.Handled = true;
            };
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            StartApi();
            await WaitForApiAsync();

            _window = new MainWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }

        private void StartApi()
        {
            var localApiDll = Path.Combine(AppContext.BaseDirectory, "APConfigManager.Api.dll");
            var startInfo = File.Exists(localApiDll)
                ? CreateApiProcessStartInfo("dotnet", $"\"{localApiDll}\" --urls http://localhost:5000")
                : CreateApiProjectStartInfo();

            if (startInfo is null)
            {
                Debug.WriteLine("API executable or project not found");
                return;
            }

            _apiProcess = new Process
            {
                StartInfo = startInfo
            };

            _apiProcess.Start();
            Debug.WriteLine($"API started: PID {_apiProcess.Id}");
        }

        private static ProcessStartInfo? CreateApiProjectStartInfo()
        {
            var csproj = FindApiProject();
            if (csproj is null)
                return null;

            return CreateApiProcessStartInfo("dotnet", $"run --project \"{csproj}\" --urls http://localhost:5000");
        }

        private static ProcessStartInfo CreateApiProcessStartInfo(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["ASPNETCORE_URLS"] = "http://localhost:5000";
            return startInfo;
        }

        private static string? FindApiProject()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var path = Path.Combine(dir.FullName, "src",
                    "APConfigManager.Api", "APConfigManager.Api.csproj");
                if (File.Exists(path)) return path;
                dir = dir.Parent;
            }
            return null;
        }

        private static async Task WaitForApiAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            for (var i = 0; i < 30; i++)
            {
                try
                {
                    await client.GetAsync("http://localhost:5000/api/ports");
                    Debug.WriteLine("API ready");
                    return;
                }
                catch { }
                await Task.Delay(500);
            }
            Debug.WriteLine("API not ready after 15s");
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_apiProcess is not null && !_apiProcess.HasExited)
            {
                try
                {
                    _apiProcess.Kill(entireProcessTree: true);
                    _apiProcess.Dispose();
                }
                catch { }
            }
        }
    }
}

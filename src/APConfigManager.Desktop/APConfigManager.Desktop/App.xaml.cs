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

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();

            _ = StartApiAndLoadAsync();
        }

        private async Task StartApiAndLoadAsync()
        {
            try
            {
                StartApi();
                await WaitForApiAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API startup error: {ex.Message}");
            }
        }

        private void StartApi()
        {
            try
            {
                var appDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? AppContext.BaseDirectory;

                var apiExe = Path.Combine(appDir, "..", "api", "APConfigManager.Api.exe");
                apiExe = Path.GetFullPath(apiExe);

                if (!File.Exists(apiExe))
                {
                    apiExe = Path.Combine(appDir, "api", "APConfigManager.Api.exe");
                }

                if (!File.Exists(apiExe))
                {
                    var csproj = FindApiProject();
                    if (string.IsNullOrEmpty(csproj))
                    {
                        Debug.WriteLine("API not found");
                        return;
                    }

                    _apiProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"run --project \"{csproj}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    _apiProcess.Start();
                    Debug.WriteLine($"API (dev) started: PID {_apiProcess.Id}");
                    return;
                }

                Debug.WriteLine($"API exe: {apiExe}");

                _apiProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = apiExe,
                        WorkingDirectory = Path.GetDirectoryName(apiExe)!,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _apiProcess.Start();
                Debug.WriteLine($"API started: PID {_apiProcess.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start API: {ex.Message}");
            }
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

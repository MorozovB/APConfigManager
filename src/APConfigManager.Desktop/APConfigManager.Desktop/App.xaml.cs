using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;

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
            StartApi();

            _window = new MainWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }

        private void StartApi()
        {
            try
            {
                var appDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? AppContext.BaseDirectory;

                var apiExe = Path.GetFullPath(Path.Combine(appDir, "..", "api", "APConfigManager.Api.exe"));

                if (!File.Exists(apiExe))
                    apiExe = Path.Combine(appDir, "api", "APConfigManager.Api.exe");

                if (!File.Exists(apiExe))
                {
                    var csproj = FindApiProject();
                    if (string.IsNullOrEmpty(csproj)) return;

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

                    return;
                }

                _apiProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = apiExe,
                        WorkingDirectory = Path.GetDirectoryName(apiExe)!,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };

                _apiProcess.Start();
                File.WriteAllText(
                    @"C:\Temp\api_started.txt",
                    $"PID={_apiProcess.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"API failed: {ex.Message}");
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

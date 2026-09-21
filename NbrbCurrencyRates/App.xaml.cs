using NbrbCurrencyRates.Services;
using NbrbCurrencyRates.ViewModels;
using System;
using System.IO;
using System.Windows;

namespace NbrbCurrencyRates
{
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += 
                (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                LogError("AppDomain UnhandledException", exception);
            };

            this.DispatcherUnhandledException += 
                (sender, args) =>
            {
                LogError("Dispatcher UnhandledException", args.Exception);
                args.Handled = true;
                Current.Shutdown();
            };
        }

        protected override void OnStartup(
            StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var nbrbApiClient =
                    new NbrbApiClient();

                var jsonFileStorage =
                    new JsonFileStorage();

                var viewModel =
                    new MainViewModel(
                        nbrbApiClient,
                        jsonFileStorage,
                        @"D:\nbrb-rates.json");

                var window =
                    new MainWindow(viewModel);

                MainWindow = window;

                LogError("Application Started Successfully", null);

                window.Show();
            }
            catch (System.Exception ex)
            {
                LogError("OnStartup Exception", ex);
                MessageBox.Show(
                    $"Ошибка при запуске приложения:\n{ex.Message}\n\n{ex.StackTrace}",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private static void LogError(string title, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NbrbCurrencyRates",
                    "error.log");

                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{ex?.Message}\n{ex?.StackTrace}\n\n";

                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
}

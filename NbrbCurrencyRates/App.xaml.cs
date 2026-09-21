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

            DispatcherUnhandledException +=
                (sender, args) =>
                {
                    LogError("DispatcherUnhandledException", args.Exception);
                    args.Handled = true;
                    Current.Shutdown();
                };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var nbrbApiClient = new NbrbApiClient();
                var jsonFileStorage = new JsonFileStorage();

                string dataDirectory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "NbrbCurrencyRates");

                string dataFilePath = Path.Combine(
                    dataDirectory,
                    "nbrb-rates.json");

                var viewModel = new MainViewModel(
                    nbrbApiClient,
                    jsonFileStorage,
                    dataFilePath);

                var window = new MainWindow(viewModel);
                MainWindow = window;
                window.Show();
            }
            catch (Exception exception)
            {
                LogError("OnStartup Exception", exception);
                MessageBox.Show(
                    "Ошибка при запуске приложения:\n" +
                    exception.Message,
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void LogError(string title, Exception exception)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "NbrbCurrencyRates",
                    "error.log");

                string directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string message = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss}] {1}\n{2}\n{3}\n\n",
                    DateTime.Now,
                    title,
                    exception?.Message,
                    exception?.StackTrace);

                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Ошибка логирования не должна завершать приложение.
            }
        }
    }
}

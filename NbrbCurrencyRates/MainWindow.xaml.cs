using NbrbCurrencyRates.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NbrbCurrencyRates
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;

            // Инициализация осей графика в коде, чтобы избежать ошибок XAML-активации
            try
            {
                var vAxis =
                    new Telerik.Windows.Controls.ChartView.LinearAxis();

                // Горизонтальная DateTimeContinuousAxis задана в XAML.
                RatesChart.VerticalAxis = vAxis;
            }
            catch (MissingMethodException mex)
            {
                // Если конструктор отсутствует в загруженной сборке — логируем и продолжаем без осей
                System.Diagnostics.Debug.WriteLine($"NumericalAxis ctor missing: {mex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Axis initialization failed: {ex.Message}");
            }
        }
    }
}

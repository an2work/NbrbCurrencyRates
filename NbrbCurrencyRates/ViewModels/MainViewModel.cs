using NbrbCurrencyRates.Models;
using NbrbCurrencyRates.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NbrbCurrencyRates.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly NbrbApiClient _nbrbApiClient;
        private readonly JsonFileStorage _jsonFileStorage;
        private readonly string _filePath;

        private CancellationTokenSource _cancellationTokenSource;
        private DateTime? _startDate;
        private DateTime? _endDate;
        private CurrencyRateRow _selectedRate;
        private bool _isBusy;
        private string _statusText;

        public MainViewModel(
            NbrbApiClient nbrbApiClient,
            JsonFileStorage jsonFileStorage,
            string filePath)
        {
            _nbrbApiClient = nbrbApiClient;
            _jsonFileStorage = jsonFileStorage;
            _filePath = filePath;

            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            Rates = new ObservableCollection<CurrencyRateRow>();
            SelectedCurrencyRates = new ObservableCollection<CurrencyRateRow>();

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync, () => !IsBusy);
            LoadFileCommand = new AsyncRelayCommand(LoadFileAsync, () => !IsBusy);
            SaveChangesCommand = new AsyncRelayCommand(
                SaveChangesAsync,
                () => !IsBusy && Rates.Count > 0);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy);

            StatusText = "Готово к работе.";
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate == value)
                {
                    return;
                }

                _startDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate == value)
                {
                    return;
                }

                _endDate = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<CurrencyRateRow> Rates { get; private set; }

        public CurrencyRateRow SelectedRate
        {
            get => _selectedRate;
            set
            {
                if (_selectedRate == value)
                {
                    return;
                }

                _selectedRate = value;
                OnPropertyChanged();
                UpdateChartForSelectedCurrency();
            }
        }

        public ObservableCollection<CurrencyRateRow> SelectedCurrencyRates
        {
            get;
            private set;
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
                LoadDataCommand.RaiseCanExecuteChanged();
                LoadFileCommand.RaiseCanExecuteChanged();
                SaveChangesCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText == value)
                {
                    return;
                }

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public AsyncRelayCommand LoadDataCommand { get; }

        public AsyncRelayCommand LoadFileCommand { get; }

        public AsyncRelayCommand SaveChangesCommand { get; }

        public RelayCommand CancelCommand { get; }

        private async Task LoadDataAsync()
        {
            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                StatusText = "Укажите начальную и конечную даты.";
                return;
            }

            if (StartDate.Value.Date > EndDate.Value.Date)
            {
                StatusText = "Дата начала не может быть позже даты окончания.";
                return;
            }

            await RunOperationAsync(
                "Загрузка данных из API НБ РБ...",
                "Загрузка отменена.",
                async token =>
                {
                    string json = await _nbrbApiClient
                        .GetAllCurrenciesRatesJsonAsync(
                            StartDate.Value.Date,
                            EndDate.Value.Date,
                            token)
                        .ConfigureAwait(true);

                    StatusText = "Сохранение JSON-файла...";

                    await _jsonFileStorage
                        .SaveAsync(json, _filePath, token)
                        .ConfigureAwait(true);

                    LoadRatesFromJson(json);
                    StatusText = string.Format(
                        "Данные загружены. Записей: {0}. Файл: {1}",
                        Rates.Count,
                        _filePath);
                },
                "Ошибка: ");
        }

        private Task LoadFileAsync()
        {
            return RunOperationAsync(
                "Чтение JSON-файла...",
                "Чтение файла отменено.",
                async token =>
                {
                    string json = await _jsonFileStorage
                        .LoadAsync(_filePath, token)
                        .ConfigureAwait(true);

                    LoadRatesFromJson(json);
                    StatusText = string.Format(
                        "Файл загружен. Записей: {0}",
                        Rates.Count);
                },
                "Ошибка: ");
        }

        private Task SaveChangesAsync()
        {
            return RunOperationAsync(
                "Сохранение изменений...",
                "Сохранение отменено.",
                async token =>
                {
                    string json = JsonConvert.SerializeObject(
                        Rates,
                        Formatting.Indented);

                    await _jsonFileStorage
                        .SaveAsync(json, _filePath, token)
                        .ConfigureAwait(true);

                    StatusText = "Изменения успешно сохранены.";
                },
                "Ошибка сохранения: ");
        }

        private async Task RunOperationAsync(
            string busyMessage,
            string cancelledMessage,
            Func<CancellationToken, Task> operation,
            string errorPrefix)
        {
            IsBusy = true;
            StatusText = busyMessage;

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;

            try
            {
                await operation(cancellationTokenSource.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                StatusText = cancelledMessage;
            }
            catch (Exception exception)
            {
                StatusText = errorPrefix + exception.Message;
            }
            finally
            {
                if (ReferenceEquals(
                    _cancellationTokenSource,
                    cancellationTokenSource))
                {
                    _cancellationTokenSource = null;
                }

                IsBusy = false;
                cancellationTokenSource.Dispose();
            }
        }

        private void LoadRatesFromJson(string json)
        {
            List<CurrencyRateRow> rows =
                JsonConvert.DeserializeObject<List<CurrencyRateRow>>(json)
                ?? new List<CurrencyRateRow>();

            Rates = new ObservableCollection<CurrencyRateRow>(rows);
            OnPropertyChanged(nameof(Rates));
            SaveChangesCommand.RaiseCanExecuteChanged();
            UpdateChartForSelectedCurrency();
        }

        private void UpdateChartForSelectedCurrency()
        {
            IEnumerable<CurrencyRateRow> rows = Enumerable.Empty<CurrencyRateRow>();

            if (SelectedRate != null)
            {
                int id = SelectedRate.Cur_ID;
                rows = Rates
                    .Where(row => row.Cur_ID == id)
                    .OrderBy(row => row.Date);
            }

            SelectedCurrencyRates =
                new ObservableCollection<CurrencyRateRow>(rows);
            OnPropertyChanged(nameof(SelectedCurrencyRates));
        }

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}

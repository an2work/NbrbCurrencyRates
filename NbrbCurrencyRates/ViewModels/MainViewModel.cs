using NbrbCurrencyRates.Models;
using NbrbCurrencyRates.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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

                LoadDataCommand =
                    new AsyncRelayCommand(
                        LoadDataAsync,
                        () => !IsBusy);

                LoadFileCommand =
                    new AsyncRelayCommand(
                        LoadFileAsync,
                        () => !IsBusy);

                SaveChangesCommand =
                    new AsyncRelayCommand(
                        SaveChangesAsync,
                        () => !IsBusy && Rates.Count > 0);

                CancelCommand =
                    new RelayCommand(
                        Cancel,
                        () => IsBusy);

                StatusText = "Готово к работе.";
            }

            public DateTime? StartDate
            {
                get
                {
                    return _startDate;
                }
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
                get
                {
                    return _endDate;
                }
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

            public ObservableCollection<CurrencyRateRow> Rates
            {
                get;
            }

            private CurrencyRateRow _selectedRate;

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

            public ObservableCollection<CurrencyRateRow> SelectedCurrencyRates { get; } = new ObservableCollection<CurrencyRateRow>();

            public bool IsBusy
            {
                get
                {
                    return _isBusy;
                }
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
                get
                {
                    return _statusText;
                }
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

            public AsyncRelayCommand LoadDataCommand
            {
                get;
            }

            public AsyncRelayCommand LoadFileCommand
            {
                get;
            }

            public AsyncRelayCommand SaveChangesCommand
            {
                get;
            }

            public RelayCommand CancelCommand
            {
                get;
            }
            private async Task LoadDataAsync()
            {
                if (!StartDate.HasValue ||
                    !EndDate.HasValue)
                {
                    StatusText =
                        "Укажите начальную и конечную даты.";

                    return;
                }

                if (StartDate.Value.Date > EndDate.Value.Date)
                {
                    StatusText =
                        "Дата начала не может быть позже даты окончания.";

                    return;
                }

                IsBusy = true;
                StatusText = "Загрузка данных из API НБ РБ...";

                _cancellationTokenSource =
                    new CancellationTokenSource();

                try
                {
                    string json =
                        await _nbrbApiClient
                            .GetAllCurrenciesRatesJsonAsync(
                                StartDate.Value.Date,
                                EndDate.Value.Date,
                                _cancellationTokenSource.Token)
                            .ConfigureAwait(true);

                    StatusText =
                        "Сохранение JSON-файла...";

                    await _jsonFileStorage
                        .SaveAsync(
                            json,
                            _filePath,
                            _cancellationTokenSource.Token)
                        .ConfigureAwait(true);

                    LoadRatesFromJson(json);

                    StatusText = string.Format(
                        "Данные загружены. Записей: {0}. Файл: {1}",
                        Rates.Count,
                        _filePath);
                }
                catch (OperationCanceledException)
                {
                    StatusText =
                        "Загрузка отменена.";
                }
                catch (Exception exception)
                {
                    StatusText =
                        "Ошибка: " + exception.Message;
                }
                finally
                {
                    IsBusy = false;

                    if (_cancellationTokenSource != null)
                    {
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                }
            }

            private async Task LoadFileAsync()
            {
                IsBusy = true;
                StatusText = "Чтение JSON-файла...";

                _cancellationTokenSource =
                    new CancellationTokenSource();

                try
                {
                    string json =
                        await _jsonFileStorage
                            .LoadAsync(
                                _filePath,
                                _cancellationTokenSource.Token)
                            .ConfigureAwait(true);

                    LoadRatesFromJson(json);

                    StatusText = string.Format(
                        "Файл загружен. Записей: {0}",
                        Rates.Count);
                }
                catch (OperationCanceledException)
                {
                    StatusText =
                        "Чтение файла отменено.";
                }
                catch (Exception exception)
                {
                    StatusText =
                        "Ошибка: " + exception.Message;
                }
                finally
                {
                    IsBusy = false;

                    if (_cancellationTokenSource != null)
                    {
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                }
            }

            private async Task SaveChangesAsync()
            {
                IsBusy = true;
                StatusText = "Сохранение изменений...";

                _cancellationTokenSource =
                    new CancellationTokenSource();

                try
                {
                    string json =
                        JsonConvert.SerializeObject(
                            Rates,
                            Formatting.Indented);

                    await _jsonFileStorage
                        .SaveAsync(
                            json,
                            _filePath,
                            _cancellationTokenSource.Token)
                        .ConfigureAwait(true);
                    StatusText =
                    "Изменения успешно сохранены.";
                }
                catch (OperationCanceledException)
                {
                    StatusText =
                        "Сохранение отменено.";
                }
                catch (Exception exception)
                {
                    StatusText =
                        "Ошибка сохранения: " +
                        exception.Message;
                }
                finally
                {
                    IsBusy = false;

                    if (_cancellationTokenSource != null)
                    {
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                }
            }

            private void LoadRatesFromJson(string json)
            {
                List<CurrencyRateRow> rows =
                    JsonConvert.DeserializeObject<
                        List<CurrencyRateRow>>(json)
                    ?? new List<CurrencyRateRow>();

                Rates.Clear();

                foreach (CurrencyRateRow row in rows)
                {
                    Rates.Add(row);
                }

                SaveChangesCommand.RaiseCanExecuteChanged();

                // Обновляем график при загрузке новых данных
                UpdateChartForSelectedCurrency();
            }

            private void UpdateChartForSelectedCurrency()
            {
                SelectedCurrencyRates.Clear();

                if (SelectedRate == null)
                {
                    return;
                }

                int id = SelectedRate.Cur_ID;

                foreach (CurrencyRateRow row in System.Linq.Enumerable.OrderBy(Rates, r => r.Date))
                {
                    if (row.Cur_ID == id)
                    {
                        SelectedCurrencyRates.Add(row);
                    }
                }
            }

            private void Cancel()
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                }
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

using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace NbrbCurrencyRates.Models
{
    public sealed class CurrencyRateRow : INotifyPropertyChanged
    {
        private string _curOfficialRate;

        [JsonProperty("Cur_ID")]
        public int Cur_ID { get; set; }

        [JsonProperty("Date")]
        public DateTime Date { get; set; }

        [JsonProperty("Cur_Code")]
        public int Cur_Code { get; set; }

        [JsonProperty("Cur_Abbreviation")]
        public string Cur_Abbreviation { get; set; }

        [JsonProperty("Cur_Name")]
        public string Cur_Name { get; set; }

        [JsonProperty("Cur_Scale")]
        public int Cur_Scale { get; set; }

        [JsonProperty("Cur_OfficialRate")]
        public string Cur_OfficialRate
        {
            get
            {
                return _curOfficialRate;
            }

            set
            {
                if (_curOfficialRate == value)
                {
                    return;
                }

                _curOfficialRate = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public double Cur_OfficialRateValue
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_curOfficialRate))
                {
                    return double.NaN;
                }

                string normalized =
                    _curOfficialRate.Trim()
                        .Replace(" ", string.Empty)
                        .Replace("\u00A0", string.Empty);

                int commaIndex = normalized.LastIndexOf(',');
                int dotIndex = normalized.LastIndexOf('.');

                if (commaIndex >= 0 && dotIndex >= 0)
                {
                    // Последний разделитель считаем десятичным,
                    // остальные удаляем как разделители тысяч.
                    if (commaIndex > dotIndex)
                    {
                        normalized = normalized.Replace(".", string.Empty)
                            .Replace(',', '.');
                    }
                    else
                    {
                        normalized = normalized.Replace(",", string.Empty);
                    }
                }
                else if (commaIndex >= 0)
                {
                    normalized = normalized.Replace(',', '.');
                }

                double parsed;
                if (double.TryParse(
                    normalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed))
                {
                    if (Cur_Scale != 0)
                    {
                        return parsed / Cur_Scale;
                    }

                    return parsed;
                }

                return double.NaN;
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

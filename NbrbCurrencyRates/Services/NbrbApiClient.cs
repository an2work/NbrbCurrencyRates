using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NbrbCurrencyRates.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NbrbCurrencyRates.Services
{
    public sealed class NbrbApiClient
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://api.nbrb.by/"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Add("Accept", "application/json");
            return client;
        }

        public async Task<string> GetAllCurrenciesRatesJsonAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            startDate = startDate.Date;
            endDate = endDate.Date;
            ValidatePeriod(startDate, endDate);
            cancellationToken.ThrowIfCancellationRequested();

            List<CurrencyRateRow> currencies = await GetCurrenciesAsync(
                cancellationToken).ConfigureAwait(false);

            currencies = currencies
                .GroupBy(currency => currency.Cur_ID)
                .Select(group => group.First())
                .ToList();

            var allRates = new List<CurrencyRateRow>();

            foreach (CurrencyRateRow currency in currencies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<CurrencyRateRow> returnedRates =
                    await LoadCurrencyRatesAsync(
                        currency,
                        startDate,
                        endDate,
                        cancellationToken)
                    .ConfigureAwait(false);

                Dictionary<DateTime, CurrencyRateRow> ratesByDate = returnedRates
                    .GroupBy(rate => rate.Date.Date)
                    .ToDictionary(group => group.Key, group => group.First());

                for (DateTime date = startDate;
                     date <= endDate;
                     date = date.AddDays(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    CurrencyRateRow rate;
                    if (!ratesByDate.TryGetValue(date.Date, out rate))
                    {
                        rate = new CurrencyRateRow
                        {
                            Cur_ID = currency.Cur_ID,
                            Date = date,
                            Cur_Code = currency.Cur_Code,
                            Cur_Abbreviation = currency.Cur_Abbreviation,
                            Cur_Name = currency.Cur_Name,
                            Cur_Scale = currency.Cur_Scale,
                            Cur_OfficialRate = string.Empty
                        };
                    }
                    else
                    {
                        ApplyCurrencyMetadata(rate, currency);
                    }

                    allRates.Add(rate);
                }
            }

            return JsonConvert.SerializeObject(
                allRates,
                Formatting.Indented);
        }

        private async Task<List<CurrencyRateRow>> GetCurrenciesAsync(
            CancellationToken cancellationToken)
        {
            string json = await GetJsonAsync(
                "exrates/currencies",
                cancellationToken).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<List<CurrencyRateRow>>(json)
                ?? new List<CurrencyRateRow>();
        }

        private async Task<List<CurrencyRateRow>> LoadCurrencyRatesAsync(
            CurrencyRateRow currency,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            string start = startDate.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            string end = endDate.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            string requestUri =
                "exrates/rates/dynamics/" +
                currency.Cur_ID +
                "?startdate=" + start +
                "&enddate=" + end;

            string json = await GetJsonAsync(
                requestUri,
                cancellationToken).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<List<CurrencyRateRow>>(json)
                ?? new List<CurrencyRateRow>();
        }

        private static void ApplyCurrencyMetadata(
            CurrencyRateRow rate,
            CurrencyRateRow currency)
        {
            rate.Cur_ID = currency.Cur_ID;
            rate.Cur_Code = currency.Cur_Code;
            rate.Cur_Abbreviation = currency.Cur_Abbreviation;
            rate.Cur_Name = currency.Cur_Name;
            rate.Cur_Scale = currency.Cur_Scale;

            if (rate.Cur_OfficialRate == null)
            {
                rate.Cur_OfficialRate = string.Empty;
            }
        }

        private static async Task<string> GetJsonAsync(
            string requestUri,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await HttpClient
                .GetAsync(requestUri, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);
            }
        }

        private static void ValidatePeriod(
            DateTime startDate,
            DateTime endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException(
                    "Дата начала не может быть позже даты окончания.");
            }

            if ((endDate - startDate).TotalDays > 365)
            {
                throw new ArgumentException(
                    "Период не должен превышать 365 дней.");
            }
        }

        private static JObject CreateEmptyRateObject(
           CurrencyRateRow currency,
           DateTime date)
        {
            var rateObject = new JObject();

            rateObject["Cur_ID"] =
                currency.Cur_ID;

            rateObject["Date"] =
                date.ToString(
                    "yyyy-MM-dd'T'00:00:00",
                    CultureInfo.InvariantCulture);

            rateObject["Cur_Code"] =
                currency.Cur_Code;

            rateObject["Cur_Abbreviation"] =
                currency.Cur_Abbreviation;

            rateObject["Cur_Name"] =
                currency.Cur_Name;

            rateObject["Cur_Scale"] =
                currency.Cur_Scale;

            rateObject["Cur_OfficialRate"] =
                string.Empty;

            return rateObject;
        }
        private static void EnrichRateObject(
            JObject rateObject,
            CurrencyRateRow currency)
        {
            rateObject["Cur_ID"] = currency.Cur_ID;
            rateObject["Cur_Code"] = currency.Cur_Code;
            rateObject["Cur_Abbreviation"] = currency.Cur_Abbreviation;
            rateObject["Cur_Name"] = currency.Cur_Name;
            rateObject["Cur_Scale"] = currency.Cur_Scale;

            // Гарантируем что Cur_OfficialRate присутствует и преобразуем в строку если нужно
            // API может возвращать его как число или строку или null
            JToken rateValue = rateObject["Cur_OfficialRate"];
            if (rateValue == null)
            {
                rateObject["Cur_OfficialRate"] = string.Empty;
            }
            else if (rateValue.Type != JTokenType.String)
            {
                // Если приходит как число, преобразуем в строку
                rateObject["Cur_OfficialRate"] = rateValue.ToString();
            }
        }
    }
}

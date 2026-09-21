using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NbrbCurrencyRates.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace NbrbCurrencyRates.Services
{
    public sealed class NbrbApiClient
    {
        private static readonly HttpClient HttpClient =
            CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(
                    "https://api.nbrb.by/"),

                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Add(
                "Accept",
                "application/json");

            return client;
        }

            public async Task<string>
            GetAllCurrenciesRatesJsonAsync(
                DateTime startDate,
                DateTime endDate,
                CancellationToken cancellationToken)
        {
            startDate = startDate.Date;
            endDate = endDate.Date;

            ValidatePeriod(startDate, endDate);

            cancellationToken.ThrowIfCancellationRequested();

            List<CurrencyRateRow> currencies =
                await GetCurrenciesAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            // Защищаемся от возможных повторяющихся Cur_ID.
            currencies = currencies
                .GroupBy(currency => currency.Cur_ID)
                .Select(group => group.First())
                .ToList();

            var allRatesJson = new JArray();

            // Запросы выполняем последовательно.
            // Это уменьшает нагрузку на API НБ РБ.
            foreach (CurrencyRateRow currency in currencies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string apiJson =
                    await LoadCurrencyRatesJsonAsync(
                            currency,
                            startDate,
                            endDate,
                            cancellationToken)
                        .ConfigureAwait(false);

                JArray apiRates = JArray.Parse(apiJson);

                var ratesByDate =
                    new Dictionary<DateTime, JObject>();

                foreach (JToken token in apiRates)
                {
                    JObject rateObject =
                        token as JObject;

                    if (rateObject == null)
                    {
                        continue;
                    }

                    JToken dateToken =
                        rateObject["Date"];

                    if (dateToken == null)
                    {
                        continue;
                    }

                    DateTime rateDate;

                    bool dateParsed =
                        DateTime.TryParse(
                            dateToken.ToString(),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out rateDate);

                    if (!dateParsed)
                    {
                        continue;
                    }
                    // Endpoint dynamics возвращает в основном
                    // Cur_ID, Date и Cur_OfficialRate.
                    // Добавляем остальные сведения о валюте,
                    // чтобы их можно было показать в RadGridView.
                    EnrichRateObject(
                        rateObject,
                        currency);

                    ratesByDate[rateDate.Date] =
                        rateObject;
                }

                // Формируем полную последовательность дат.
                for (DateTime date = startDate;
                     date <= endDate;
                     date = date.AddDays(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    JObject rateObject;

                    bool hasRate =
                        ratesByDate.TryGetValue(
                            date.Date,
                            out rateObject);

                    if (hasRate)
                    {
                        // Сохраняем реальный объект курса.
                        allRatesJson.Add(
                            rateObject.DeepClone());
                    }
                    else
                    {
                        // Если API не вернуло курс,
                        // добавляем пустую запись.
                        allRatesJson.Add(
                            CreateEmptyRateObject(
                                currency,
                                date));
                    }
                }
            }

            string resultJson = allRatesJson.ToString(
                Formatting.Indented);

            // Logging removed to keep client minimal.
            return resultJson;
        }

        private async Task<List<CurrencyRateRow>>
            GetCurrenciesAsync(
                CancellationToken cancellationToken)
        {
            string json =
                await GetJsonAsync(
                        "exrates/currencies",
                        cancellationToken)
                    .ConfigureAwait(false);

            return JsonConvert.DeserializeObject<
                       List<CurrencyRateRow>>(json)
                   ?? new List<CurrencyRateRow>();
        }

        private async Task<string>
            LoadCurrencyRatesJsonAsync(
                CurrencyRateRow currency,
                DateTime startDate,
                DateTime endDate,
                CancellationToken cancellationToken)
        {
            string start =
                startDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

            string end =
                endDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);

            string requestUri =
                "exrates/rates/dynamics/" +
                currency.Cur_ID +
                "?startdate=" + start +
                "&enddate=" + end;

            return await GetJsonAsync(
                    requestUri,
                    cancellationToken)
                .ConfigureAwait(false);
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

        private static async Task<string> GetJsonAsync(
            string requestUri,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response =
                await HttpClient.GetAsync(
                    requestUri,
                    cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync()
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

    }
}

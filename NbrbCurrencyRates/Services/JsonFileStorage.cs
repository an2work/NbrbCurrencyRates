using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NbrbCurrencyRates.Services
{
    public sealed class JsonFileStorage
    {
        public async Task SaveAsync(
            string json,
            string filePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException(
                    "JSON не может быть пустым.",
                    nameof(json));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string fullPath =
                Path.GetFullPath(filePath);

            string directory =
                Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(directory) &&
                !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(
                fullPath,
                false,
                new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json)
                    .ConfigureAwait(false);

                await writer.FlushAsync()
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task<string> LoadAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "JSON-файл не найден.",
                    filePath);
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var reader = new StreamReader(
                filePath,
                Encoding.UTF8))
            {
                string json =
                    await reader.ReadToEndAsync()
                        .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                return json;
            }
        }
    }
}

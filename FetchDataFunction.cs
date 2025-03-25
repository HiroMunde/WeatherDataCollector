using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System.IO;

public static class FetchDataFunction
{
    private static readonly HttpClient client = new HttpClient();

    [FunctionName("FetchDataFunction")]
    public static async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
        [Table("WeatherLogs")] IAsyncCollector<WeatherLogEntry> tableCollector,
        [Blob("weather-data/{DateTime}.json", FileAccess.Write)] Stream blobOutput,
        ILogger log)
    {
        // Hämta API-nyckel från konfiguration
        var apiKey = Environment.GetEnvironmentVariable("OpenWeatherMapApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            log.LogError("OpenWeatherMap API key is missing in configuration");
            return;
        }

        var openWeatherUrl = $"https://api.openweathermap.org/data/2.5/weather?q=London&appid={apiKey}";

        try
        {
            var response = await client.GetAsync($"{openWeatherUrl}");
            var content = await response.Content.ReadAsStringAsync();

            var logEntry = new WeatherLogEntry
            {
                PartitionKey = DateTime.UtcNow.ToString("yyyyMMdd"),
                RowKey = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Status = response.IsSuccessStatusCode ? "Success" : "Failed",
                StatusCode = (int)response.StatusCode,
                City = "London"
            };

            await tableCollector.AddAsync(logEntry);

            using (var writer = new StreamWriter(blobOutput))
            {
                await writer.WriteAsync(content);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error in FetchDataFunction");
        }
    }
}

public class WeatherLogEntry : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
    public string City { get; set; }
    public string Status { get; set; }
    public int? StatusCode { get; set; }
    public string ErrorMessage { get; set; }
}
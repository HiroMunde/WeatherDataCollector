using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System.IO;

public static class FetchDataFunction
{
    internal static readonly HttpClient client = new HttpClient();
    private static IWeatherDataService _weatherDataService = new WeatherDataService();

    internal static void SetWeatherDataService(IWeatherDataService service)
    {
        _weatherDataService = service;
    }

    [FunctionName("FetchDataFunction")]
    public static async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
        [Table("WeatherLogs")] IAsyncCollector<WeatherLogEntry> tableCollector,
        Binder binder,
        ILogger log)
    {
        var apiKey = Environment.GetEnvironmentVariable("OpenWeatherMapApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            log.LogError("OpenWeatherMap API key is missing in configuration");
            return;
        }

        await _weatherDataService.FetchAndStoreWeatherData("London", apiKey, tableCollector, binder, log);
    }
}

public interface IWeatherDataService
{
    Task FetchAndStoreWeatherData(
        string location,
        string apiKey,
        IAsyncCollector<WeatherLogEntry> tableCollector,
        Binder binder,
        ILogger log);
}

public class WeatherDataService : IWeatherDataService
{
    private readonly IHttpClientWrapper _httpClient;
    private readonly IBlobStorageService _blobStorageService;

    public WeatherDataService() : this(new HttpClientWrapper(FetchDataFunction.client), new BlobStorageService())
    {
    }

    public WeatherDataService(IHttpClientWrapper httpClient, IBlobStorageService blobStorageService)
    {
        _httpClient = httpClient;
        _blobStorageService = blobStorageService;
    }

    public async Task FetchAndStoreWeatherData(
        string location,
        string apiKey,
        IAsyncCollector<WeatherLogEntry> tableCollector,
        Binder binder,
        ILogger log)
    {
        var openWeatherUrl = $"https://api.openweathermap.org/data/2.5/weather?q={location}&appid={apiKey}";
        var rowKey = Guid.NewGuid().ToString();

        try
        {
            var response = await _httpClient.GetAsync(openWeatherUrl);
            var content = await response.Content.ReadAsStringAsync();

            string blobPath = await _blobStorageService.SaveToBlobStorage(binder, content, rowKey, log);

            var logEntry = CreateWeatherLogEntry(location, rowKey, response, blobPath);

            await tableCollector.AddAsync(logEntry);

            log.LogInformation($"Successfully saved weather data with BlobId: {rowKey}");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error in FetchDataFunction");
            throw;
        }
    }

    internal static WeatherLogEntry CreateWeatherLogEntry(
        string partitionKey,
        string rowKey,
        HttpResponseMessage response,
        string blobPath)
    {
        return new WeatherLogEntry
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            Status = response.IsSuccessStatusCode ? "Success" : "Failed",
            StatusCode = (int)response.StatusCode,
            BlobPath = blobPath,
        };
    }
}

public interface IHttpClientWrapper
{
    Task<HttpResponseMessage> GetAsync(string requestUri);
}

public class HttpClientWrapper : IHttpClientWrapper
{
    private readonly HttpClient _httpClient;

    public HttpClientWrapper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        return _httpClient.GetAsync(requestUri);
    }
}

public interface IBlobStorageService
{
    Task<string> SaveToBlobStorage(Binder binder, string content, string rowKey, ILogger log);
}

public class BlobStorageService : IBlobStorageService
{
    public async Task<string> SaveToBlobStorage(Binder binder, string content, string rowKey, ILogger log)
    {
        try
        {
            var blobPath = $"weather-data/{rowKey}.json";
            var blobAttribute = new BlobAttribute(blobPath, FileAccess.Write);

            using (var blobOutput = await binder.BindAsync<Stream>(blobAttribute))
            using (var writer = new StreamWriter(blobOutput))
            {
                await writer.WriteAsync(content);
            }

            return blobPath;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error saving to blob storage");
            throw;
        }
    }
}

public class WeatherLogEntry : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public Azure.ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public string Status { get; set; }
    public int? StatusCode { get; set; }
    public string BlobPath { get; set; }
}
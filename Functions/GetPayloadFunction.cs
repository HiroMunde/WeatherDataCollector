using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using System.IO;
using Azure;

public static class GetPayloadFunction
{
    [FunctionName("GetPayload")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "payload/{RowKey?}")] HttpRequest req,
        string RowKey,
        ILogger log)
    {
        log.LogInformation($"Fetching weather payload for RowKey: {RowKey}");

        if (string.IsNullOrEmpty(RowKey))
        {
            return new BadRequestObjectResult("Please provide a RowKey in the URL path (/payload/{RowKey})");
        }

        if (!Guid.TryParse(RowKey, out _))
        {
            return new BadRequestObjectResult("RowKey must be a valid GUID format");
        }

        try
        {
            var tableClient = new TableClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "WeatherLogs");

            WeatherLogEntry logEntry;
            try
            {
                var response = await tableClient.GetEntityAsync<WeatherLogEntry>("London", RowKey);
                logEntry = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return new NotFoundObjectResult($"No weather log found with RowKey: {RowKey}");
            }

            if (string.IsNullOrEmpty(logEntry.BlobPath))
            {
                return new NotFoundObjectResult($"Weather log with RowKey {RowKey} has no associated blob reference");
            }

            var blobClient = new BlobClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "weather-data",
                $"{RowKey}.json");

            if (!await blobClient.ExistsAsync())
            {
                return new NotFoundObjectResult($"Weather data blob not found for RowKey: {RowKey}");
            }

            try
            {
                var response = await blobClient.DownloadAsync();
                using var streamReader = new StreamReader(response.Value.Content);
                var content = await streamReader.ReadToEndAsync();

                return new OkObjectResult(content);
            }
            catch (RequestFailedException ex)
            {
                log.LogError(ex, $"Blob download failed for RowKey: {RowKey}");
                return new ObjectResult($"Failed to retrieve weather data: {ex.Message}")
                {
                    StatusCode = 500
                };
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Unexpected error processing request for RowKey: {RowKey}");
            return new ObjectResult("An unexpected error occurred. Please try again later.")
            {
                StatusCode = 500
            };
        }
    }
}
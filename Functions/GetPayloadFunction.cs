using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using System;

public static class GetPayloadFunction
{
    [FunctionName("GetPayload")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "payload/{RowKey?}")] HttpRequest req,
        string RowKey,
        [Table("WeatherLogs", "London", "{RowKey}")] WeatherLogEntry logEntry,
        ILogger log)
    {
        log.LogInformation($"Fetching weather payload for RowKey: {RowKey}");

        if (string.IsNullOrEmpty(RowKey))
            return new BadRequestObjectResult("Please provide a RowKey in the URL path (/payload/{RowKey})");

        if (!Guid.TryParse(RowKey, out _))
            return new BadRequestObjectResult("RowKey must be a valid GUID format");

        if (logEntry == null)
            return new NotFoundObjectResult($"No weather log found with RowKey: {RowKey}");

        if (string.IsNullOrWhiteSpace(logEntry.BlobPath))
            return new NotFoundObjectResult($"Weather log with RowKey {RowKey} has no associated blob reference");

        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            var parts = logEntry.BlobPath.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return new BadRequestObjectResult($"Invalid BlobPath format: {logEntry.BlobPath}");

            var containerName = parts[0];
            var blobName = parts[1];

            var blobClient = new BlobClient(connectionString, containerName, blobName);

            if (!await blobClient.ExistsAsync())
                return new NotFoundObjectResult($"Blob not found at path: {logEntry.BlobPath}");

            var response = await blobClient.DownloadStreamingAsync();
            using var reader = new StreamReader(response.Value.Content);
            var content = await reader.ReadToEndAsync();

            return new OkObjectResult(content);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to retrieve payload");
            return new ObjectResult("Failed to retrieve weather data") { StatusCode = 500 };
        }
    }
}

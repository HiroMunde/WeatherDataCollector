using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

public static class GetPayloadFunction
{
    [FunctionName("GetPayload")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Getting weather payload.");

        string logId = req.Query["logId"];
        if (string.IsNullOrEmpty(logId))
        {
            return new BadRequestObjectResult("Please provide a 'logId' parameter.");
        }

        try
        {
            var tableClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var table = tableClient.GetTableClient("WeatherLogs");

            // Get the log entry
            var logEntry = await table.GetEntityAsync<WeatherLogEntry>("partitionKey", logId);
            if (logEntry == null)
            {
                return new NotFoundObjectResult($"Log entry with ID {logId} not found.");
            }

            // Get the blob content
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var containerClient = blobServiceClient.GetBlobContainerClient("weather-data");

            // Assuming blob name is based on timestamp
            var blobName = $"{logEntry.Value.Timestamp:yyyyMMddHHmmss}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                return new NotFoundObjectResult($"Payload not found for log ID {logId}");
            }

            var response = await blobClient.DownloadContentAsync();
            var content = response.Value.Content.ToString();

            return new OkObjectResult(content);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error retrieving payload");
            return new StatusCodeResult(500);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using System.Linq;

public static class GetLogsFunction
{
    [FunctionName("GetLogs")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Getting weather logs.");

        string fromDateStr = req.Query["from"];
        string toDateStr = req.Query["to"];

        if (!DateTime.TryParse(fromDateStr, out DateTime fromDate) ||
            !DateTime.TryParse(toDateStr, out DateTime toDate))
        {
            return new BadRequestObjectResult("Please provide valid 'from' and 'to' date parameters.");
        }

        try
        {
            var tableClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var table = tableClient.GetTableClient("WeatherLogs");

            var queryResults = table.QueryAsync<WeatherLogEntry>(filter:
                $"PartitionKey ge '{fromDate:yyyyMMdd}' and PartitionKey le '{toDate:yyyyMMdd}'");

            var logs = new List<WeatherLogEntry>();
            await foreach (var logEntry in queryResults)
            {
                if (logEntry.Timestamp >= fromDate && logEntry.Timestamp <= toDate)
                {
                    logs.Add(logEntry);
                }
            }

            return new OkObjectResult(logs.OrderBy(l => l.Timestamp));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error retrieving logs");
            return new StatusCodeResult(500);
        }
    }
}
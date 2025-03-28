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
using Azure;

public static class GetLogsFunction
{
    [FunctionName("GetLogs")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "logs")] HttpRequest req,
        ILogger log,
        [Table("WeatherLogs")] TableClient tableClient)
    {
        log.LogInformation("GetLogs function processing request.");

        if (!req.Query.TryGetValue("from", out var fromDateStr) ||
            !req.Query.TryGetValue("to", out var toDateStr))
        {
            return new BadRequestObjectResult("Both 'from' and 'to' date parameters are required (YYYY-MM-DD).");
        }

        if (!DateTimeOffset.TryParse(fromDateStr, out var fromDate) ||
            !DateTimeOffset.TryParse(toDateStr, out var toDate))
        {
            return new BadRequestObjectResult("Invalid date format. Use ISO 8601 format (e.g., 2025-03-01).");
        }

        if (fromDate > toDate)
        {
            return new BadRequestObjectResult("'from' date cannot be after 'to' date.");
        }

        try
        {
            var filter =
                $"PartitionKey eq 'London' and Timestamp ge datetime'{fromDate:o}' and Timestamp le datetime'{toDate:o}'";

            var logs = new List<WeatherLogEntry>();
            var queryResults = tableClient.QueryAsync<WeatherLogEntry>(filter: filter);

            await foreach (var page in queryResults.AsPages())
            {
                logs.AddRange(page.Values);
            }

            var result = new
            {
                City = "London",
                logs.Count,
                FromDate = fromDate,
                ToDate = toDate,
                Logs = logs.OrderBy(l => l.Timestamp)
                           .Select(l => new
                           {
                               l.RowKey,
                               l.Timestamp,
                               l.Status,
                               l.StatusCode,
                               l.BlobPath
                           })
            };

            return new OkObjectResult(result);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult("Weather logs table not found.");
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Error retrieving logs from {fromDate} to {toDate}");
            return new ObjectResult("An error occurred while processing your request.")
            {
                StatusCode = 500
            };
        }
    }
}

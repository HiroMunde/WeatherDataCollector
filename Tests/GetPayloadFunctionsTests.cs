using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Internal;

public class GetPayloadFunctionTests
{
    [Fact]
    public async Task Run_ShouldReturnBadRequest_WhenRowKeyIsNull()
    {
        var context = new DefaultHttpContext();
        var req = new DefaultHttpRequest(context);

        var result = await GetPayloadFunction.Run(
            req,
            null,
            null,
            Mock.Of<ILogger>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Please provide a RowKey in the URL path (/payload/{RowKey})", badRequest.Value);
    }

    [Fact]
    public async Task Run_ShouldReturnBadRequest_WhenRowKeyIsInvalid()
    {
        var context = new DefaultHttpContext();
        var req = new DefaultHttpRequest(context);

        var result = await GetPayloadFunction.Run(
            req,
            "not-a-guid",
            null,
            Mock.Of<ILogger>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("RowKey must be a valid GUID format", badRequest.Value);
    }

    [Fact]
    public async Task Run_ShouldReturnNotFound_WhenLogEntryIsMissing()
    {
        var context = new DefaultHttpContext();
        var req = new DefaultHttpRequest(context);

        var result = await GetPayloadFunction.Run(
            req,
            Guid.NewGuid().ToString(),
            null,
            Mock.Of<ILogger>());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("No weather log found", notFound.Value.ToString());
    }

    [Fact]
    public async Task Run_ShouldReturnNotFound_WhenBlobPathMissing()
    {
        var context = new DefaultHttpContext();
        var req = new DefaultHttpRequest(context);

        var logEntry = new WeatherLogEntry
        {
            RowKey = Guid.NewGuid().ToString(),
            PartitionKey = "London",
            BlobPath = null
        };

        var result = await GetPayloadFunction.Run(
            req,
            logEntry.RowKey,
            logEntry,
            Mock.Of<ILogger>());

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("has no associated blob reference", notFound.Value.ToString());
    }

    [Fact]
    public async Task Run_ShouldReturnBadRequest_WhenBlobPathIsInvalid()
    {
        var context = new DefaultHttpContext();
        var req = new DefaultHttpRequest(context);

        var logEntry = new WeatherLogEntry
        {
            RowKey = Guid.NewGuid().ToString(),
            PartitionKey = "London",
            BlobPath = "invalid-path-no-slash"
        };

        var result = await GetPayloadFunction.Run(
            req,
            logEntry.RowKey,
            logEntry,
            Mock.Of<ILogger>());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid BlobPath format", badRequest.Value.ToString());
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Xunit;
using Moq;
using Azure;
using System.Linq;
using Microsoft.AspNetCore.Http.Internal;
using System.Threading;

public class GetLogsFunctionTests
{
    private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();
    private readonly Mock<TableClient> _mockTableClient = new Mock<TableClient>();
    private readonly DefaultHttpContext _httpContext = new DefaultHttpContext();

    [Fact]
    public async Task Run_MissingFromAndToParams_ReturnsBadRequest()
    {
        var request = new DefaultHttpRequest(_httpContext)
        {
            Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>())
        };

        var result = await GetLogsFunction.Run(request, _mockLogger.Object, _mockTableClient.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Both 'from' and 'to' date parameters are required (YYYY-MM-DD).", badRequestResult.Value);
    }

    [Fact]
    public async Task Run_InvalidDateFormat_ReturnsBadRequest()
    {
        var request = new DefaultHttpRequest(_httpContext)
        {
            Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "from", "invalid-date" },
                { "to", "invalid-date" }
            })
        };

        var result = await GetLogsFunction.Run(request, _mockLogger.Object, _mockTableClient.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid date format. Use ISO 8601 format (e.g., 2025-03-01).", badRequestResult.Value);
    }

    [Fact]
    public async Task Run_FromDateAfterToDate_ReturnsBadRequest()
    {
        var request = new DefaultHttpRequest(_httpContext)
        {
            Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "from", "2025-03-02" },
                { "to", "2025-03-01" }
            })
        };

        var result = await GetLogsFunction.Run(request, _mockLogger.Object, _mockTableClient.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("'from' date cannot be after 'to' date.", badRequestResult.Value);
    }

    [Fact]
    public async Task Run_TableNotFound_ReturnsNotFound()
    {
        var request = new DefaultHttpRequest(_httpContext)
        {
            Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "from", "2025-03-01" },
                { "to", "2025-03-02" }
            })
        };

        var mockAsyncPageable = new Mock<AsyncPageable<WeatherLogEntry>>();
        mockAsyncPageable.Setup(m => m.AsPages(It.IsAny<string>(), It.IsAny<int?>()))
            .Throws(new RequestFailedException(404, "Not Found", "TableNotFound", null));

        _mockTableClient.Setup(c => c.QueryAsync<WeatherLogEntry>(It.IsAny<string>(), null, null, default))
            .Returns(mockAsyncPageable.Object);

        var result = await GetLogsFunction.Run(request, _mockLogger.Object, _mockTableClient.Object);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Weather logs table not found.", notFoundResult.Value);
    }

    [Fact]
    public async Task Run_ValidRequest_ReturnsLogs()
    {
        var request = new DefaultHttpRequest(_httpContext)
        {
            Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "from", "2025-03-01" },
            { "to", "2025-03-02" }
        })
        };

        var weatherLogs = new List<WeatherLogEntry>
        {
            new WeatherLogEntry
            {
                PartitionKey = "London",
                RowKey = "1",
                Timestamp = DateTimeOffset.UtcNow,
                Status = "Sunny",
                StatusCode = 200,
                BlobPath = "path/to/blob"
            }
        };

        var mockAsyncPageable = new MockAsyncPageable<WeatherLogEntry>(weatherLogs);

        _mockTableClient.Setup(c => c.QueryAsync<WeatherLogEntry>(It.IsAny<string>(), null, null, default))
            .Returns(mockAsyncPageable);

        var result = await GetLogsFunction.Run(request, _mockLogger.Object, _mockTableClient.Object);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value as dynamic;
        Assert.NotNull(response);
        Assert.Equal(1, response.Count);
        Assert.Equal("London", response.City);
    }

    public class MockAsyncPageable<T> : AsyncPageable<T>
    {
        private readonly IReadOnlyList<T> _items;

        public MockAsyncPageable(IEnumerable<T> items)
        {
            _items = items.ToList();
        }

        public override async IAsyncEnumerable<Page<T>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            var page = Page<T>.FromValues(_items, null, Mock.Of<Response>());
            yield return page;
            await Task.Yield();
        }
    }
}
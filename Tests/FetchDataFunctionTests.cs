using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.IO;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using System.Threading;
using Moq.Protected;

public class FetchDataFunctionTests
{
    private readonly Mock<IAsyncCollector<WeatherLogEntry>> _mockTableCollector;
    private readonly Mock<Binder> _mockBinder;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IWeatherDataService> _mockWeatherDataService;
    private readonly Mock<IBlobStorageService> _mockBlobStorageService;
    private readonly Mock<IHttpClientWrapper> _mockHttpClient;

    public FetchDataFunctionTests()
    {
        _mockTableCollector = new Mock<IAsyncCollector<WeatherLogEntry>>();
        _mockBinder = new Mock<Binder>();
        _mockLogger = new Mock<ILogger>();
        _mockWeatherDataService = new Mock<IWeatherDataService>();
        _mockBlobStorageService = new Mock<IBlobStorageService>();
        _mockHttpClient = new Mock<IHttpClientWrapper>();
    }

    [Fact]
    public async Task Run_ShouldCallWeatherDataService()
    {
        FetchDataFunction.SetWeatherDataService(_mockWeatherDataService.Object);
        Environment.SetEnvironmentVariable("OpenWeatherMapApiKey", "test-key");

        var timerInfo = new TimerInfo(null, new ScheduleStatus(), false);

        await FetchDataFunction.Run(timerInfo, _mockTableCollector.Object, _mockBinder.Object, _mockLogger.Object);

        _mockWeatherDataService.Verify(x => x.FetchAndStoreWeatherData(
            "London",
            "test-key",
            _mockTableCollector.Object,
            _mockBinder.Object,
            _mockLogger.Object), Times.Once);
    }

    [Fact]
    public async Task Run_ShouldLogError_WhenApiKeyMissing()
    {
        Environment.SetEnvironmentVariable("OpenWeatherMapApiKey", null);
        var timerInfo = new TimerInfo(null, new ScheduleStatus(), false);

        await FetchDataFunction.Run(timerInfo, _mockTableCollector.Object, _mockBinder.Object, _mockLogger.Object);

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("OpenWeatherMap API key is missing")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public async Task WeatherDataService_FetchAndStoreWeatherData_ShouldCreateCorrectLogEntry()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var blobPath = "weather-data/test123.json";
        var rowKey = Guid.NewGuid().ToString();

        _mockHttpClient.Setup(x => x.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(response);
        _mockBlobStorageService.Setup(x => x.SaveToBlobStorage(It.IsAny<Binder>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
            .ReturnsAsync(blobPath);

        var service = new WeatherDataService(_mockHttpClient.Object, _mockBlobStorageService.Object);

        await service.FetchAndStoreWeatherData(
            "London",
            "test-key",
            _mockTableCollector.Object,
            _mockBinder.Object,
            _mockLogger.Object);


        _mockTableCollector.Verify(x => x.AddAsync(
            It.Is<WeatherLogEntry>(e =>
            e.PartitionKey == "London" &&
            e.StatusCode == (int)HttpStatusCode.OK &&
            e.BlobPath == blobPath),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WeatherDataService_FetchAndStoreWeatherData_ShouldHandleError()
    {
        _mockHttpClient.Setup(x => x.GetAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Test error"));

        var service = new WeatherDataService(_mockHttpClient.Object, _mockBlobStorageService.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.FetchAndStoreWeatherData(
                "London",
                "test-key",
                _mockTableCollector.Object,
                _mockBinder.Object,
                _mockLogger.Object));

        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error in FetchDataFunction")),
            It.IsAny<HttpRequestException>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public async Task BlobStorageService_SaveToBlobStorage_ShouldReturnCorrectPath()
    {
        var content = "test content";
        var rowKey = Guid.NewGuid().ToString();
        var expectedPath = $"weather-data/{rowKey}.json";
        var mockStream = new MemoryStream();
        var wasWritten = false;

        _mockBinder.Setup(x => x.BindAsync<Stream>(It.IsAny<BlobAttribute>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream)
            .Callback(() => wasWritten = true);

        var service = new BlobStorageService();

        var result = await service.SaveToBlobStorage(_mockBinder.Object, content, rowKey, _mockLogger.Object);

        Assert.Equal(expectedPath, result);
        Assert.True(wasWritten);
    }

    [Fact]
    public void CreateWeatherLogEntry_ShouldReturnCorrectEntry()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var partitionKey = "London";
        var rowKey = Guid.NewGuid().ToString();
        var blobPath = "weather-data/test123.json";

        var result = WeatherDataService.CreateWeatherLogEntry(partitionKey, rowKey, response, blobPath);

        Assert.Equal(partitionKey, result.PartitionKey);
        Assert.Equal(rowKey, result.RowKey);
        Assert.Equal("Success", result.Status);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(blobPath, result.BlobPath);
    }
}
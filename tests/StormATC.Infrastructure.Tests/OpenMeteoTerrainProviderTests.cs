using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StormATC.Application.Options;
using StormATC.Infrastructure.Terrain;

namespace StormATC.Infrastructure.Tests;

/// <summary>Open-Meteo terrain provider wire format, offline gating, and grid generation.</summary>
public sealed class OpenMeteoTerrainProviderTests
{
    [Fact]
    public async Task Offline_mode_returns_null_and_sends_no_request()
    {
        var handler = new RecordingHandler("""{"elevation":[1.0]}""");
        var provider = CreateProvider(handler, offline: true);

        var result = await provider.GetElevationsAsync(
            new[] { (48.0, 11.0) },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task Elevations_are_converted_from_metres_to_feet()
    {
        var handler = new RecordingHandler("""{"elevation":[100.0,null,200.0]}""");
        var provider = CreateProvider(handler, offline: false);

        IReadOnlyList<double?>? result = await provider.GetElevationsAsync(
            new[] { (48.0, 11.0), (48.1, 11.1), (48.2, 11.2) },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Equal(100.0 * 3.280839895, result[0]!.Value, 6);
        Assert.Null(result[1]);
        Assert.Equal(200.0 * 3.280839895, result[2]!.Value, 6);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal("api.open-meteo.com", handler.RequestUri!.Host);
        Assert.Contains("latitude=48.00000,48.10000,48.20000", handler.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Http_failure_returns_null()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
        var provider = CreateProvider(handler, offline: false);

        var result = await provider.GetElevationsAsync(
            new[] { (48.0, 11.0) },
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Ground_grid_returns_one_elevation_per_cell()
    {
        var handler = new RecordingHandler("""{"elevation":[10.0,20.0,30.0,40.0]}""");
        var provider = CreateProvider(handler, offline: false);

        double[]? grid = await provider.GetGroundGridAsync(
            centerLat: 48.0,
            centerLon: 11.0,
            widthPoints: 2,
            heightPoints: 2,
            spacingNm: 1.0,
            CancellationToken.None);

        Assert.NotNull(grid);
        Assert.Equal(4, grid!.Length);
        Assert.All(grid, value => Assert.False(double.IsNaN(value)));
    }

    [Fact]
    public async Task Ground_grid_rejects_invalid_dimensions()
    {
        var provider = CreateProvider(new RecordingHandler("{}"), offline: false);

        Assert.Null(await provider.GetGroundGridAsync(48, 11, 0, 4, 1, CancellationToken.None));
        Assert.Null(await provider.GetGroundGridAsync(48, 11, 4, 4, 0, CancellationToken.None));
    }

    private static OpenMeteoTerrainProvider CreateProvider(
        HttpMessageHandler handler,
        bool offline) =>
        new(
            new HttpClient(handler),
            Options.Create(new ApplicationOptions { OfflineMode = offline }),
            NullLogger<OpenMeteoTerrainProvider>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(string body)
        {
            _body = body;
            _statusCode = HttpStatusCode.OK;
        }

        public RecordingHandler(HttpStatusCode statusCode)
        {
            _body = "";
            _statusCode = statusCode;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}

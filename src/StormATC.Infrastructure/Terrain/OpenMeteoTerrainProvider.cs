using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;

namespace StormATC.Infrastructure.Terrain;

/// <summary>
/// Terrain elevation provider backed by the Open-Meteo elevation API.
/// </summary>
/// <remarks>
/// <para>
/// MSFS SimConnect has no ground-info request (that API exists only in Prepar3D), so live
/// terrain elevation cannot come from the simulator. Open-Meteo is a free, keyless elevation
/// service that returns metres above sea level for up to 100 coordinates per request; results
/// are converted to feet for <see cref="ITerrainProvider"/>.
/// </para>
/// <para>
/// When <see cref="ApplicationOptions.OfflineMode"/> is on the provider is inert and returns
/// null, matching the contract that a missing source must degrade rather than fabricate data.
/// Any HTTP or parse failure also yields null so callers fall back honestly.
/// </para>
/// </remarks>
public sealed class OpenMeteoTerrainProvider : ITerrainProvider
{
    private const int MaxCoordinatesPerRequest = 100;
    private const double MetersToFeet = 3.280839895;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _offlineMode;
    private readonly ILogger<OpenMeteoTerrainProvider> _logger;
    private readonly ConcurrentDictionary<string, double?> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates an Open-Meteo terrain provider.</summary>
    public OpenMeteoTerrainProvider(
        HttpClient httpClient,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<OpenMeteoTerrainProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _offlineMode = applicationOptions?.Value.OfflineMode ?? false;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<double?>?> GetElevationsAsync(
        IReadOnlyList<(double lat, double lon)> points,
        CancellationToken ct = default)
    {
        if (_offlineMode || points.Count == 0)
        {
            return _offlineMode ? null : points.Select(_ => (double?)null).ToList();
        }

        var results = new List<double?>(points.Count);
        var missing = new List<(int index, double lat, double lon)>();

        for (int i = 0; i < points.Count; i++)
        {
            (double lat, double lon) = points[i];
            string key = CacheKey(lat, lon);
            if (_cache.TryGetValue(key, out double? cached))
            {
                results.Add(cached);
            }
            else
            {
                results.Add(null);
                missing.Add((i, lat, lon));
            }
        }

        if (missing.Count == 0)
        {
            return results;
        }

        for (int offset = 0; offset < missing.Count; offset += MaxCoordinatesPerRequest)
        {
            ct.ThrowIfCancellationRequested();

            var batch = missing.Skip(offset).Take(MaxCoordinatesPerRequest).ToList();
            IReadOnlyList<double?>? elevations = await RequestBatchAsync(batch, ct)
                .ConfigureAwait(false);

            if (elevations is null)
            {
                return null;
            }

            for (int i = 0; i < batch.Count; i++)
            {
                double? elevation = i < elevations.Count ? elevations[i] : null;
                results[batch[i].index] = elevation;
                _cache[CacheKey(batch[i].lat, batch[i].lon)] = elevation;
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<double[]?> GetGroundGridAsync(
        double centerLat,
        double centerLon,
        int widthPoints,
        int heightPoints,
        double spacingNm,
        CancellationToken ct = default)
    {
        if (widthPoints <= 0 || heightPoints <= 0 || spacingNm <= 0)
        {
            return null;
        }

        // 1 NM of latitude is 1/60 degree; longitude degrees shrink with latitude.
        double latStep = spacingNm / 60.0;
        double lonStep = spacingNm / (60.0 * Math.Max(Math.Cos(centerLat * Math.PI / 180.0), 0.01));
        double latOrigin = centerLat - (heightPoints - 1) * latStep / 2;
        double lonOrigin = centerLon - (widthPoints - 1) * lonStep / 2;

        var points = new List<(double lat, double lon)>(widthPoints * heightPoints);
        for (int row = 0; row < heightPoints; row++)
        {
            for (int col = 0; col < widthPoints; col++)
            {
                points.Add((latOrigin + row * latStep, lonOrigin + col * lonStep));
            }
        }

        IReadOnlyList<double?>? elevations = await GetElevationsAsync(points, ct)
            .ConfigureAwait(false);
        if (elevations is null)
        {
            return null;
        }

        var grid = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            grid[i] = i < elevations.Count && elevations[i] is double value
                ? value
                : double.NaN;
        }

        return grid;
    }

    private async Task<IReadOnlyList<double?>?> RequestBatchAsync(
        List<(int index, double lat, double lon)> batch,
        CancellationToken ct)
    {
        string latitude = string.Join(',', batch.Select(b => b.lat.ToString("F5", CultureInfo.InvariantCulture)));
        string longitude = string.Join(',', batch.Select(b => b.lon.ToString("F5", CultureInfo.InvariantCulture)));
        string url = $"https://api.open-meteo.com/v1/elevation?latitude={latitude}&longitude={longitude}";

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(url, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Terrain elevation request failed with HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(ct)
                .ConfigureAwait(false);
            using JsonDocument document = await JsonDocument
                .ParseAsync(stream, cancellationToken: ct)
                .ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("elevation", out JsonElement elevationElement)
                || elevationElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Terrain elevation response had no 'elevation' array.");
                return null;
            }

            var elevations = new List<double?>(batch.Count);
            foreach (JsonElement item in elevationElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out double meters))
                {
                    elevations.Add(meters * MetersToFeet);
                }
                else
                {
                    elevations.Add(null);
                }
            }

            return elevations;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Includes TaskCanceledException from the HttpClient timeout when the caller did
            // not cancel: degrade to "no terrain data" rather than fault the tick loop.
            _logger.LogWarning(ex, "Terrain elevation request failed; continuing without terrain data.");
            return null;
        }
    }

    private static string CacheKey(double lat, double lon) =>
        $"{lat.ToString("F4", CultureInfo.InvariantCulture)},{lon.ToString("F4", CultureInfo.InvariantCulture)}";
}

/// <summary>Mock terrain provider for offline unit tests and development.</summary>
public sealed class MockTerrainProvider : ITerrainProvider
{
    private static readonly Dictionary<string, double> _cache = new(StringComparer.Ordinal);

    /// <summary>Gets terrain elevations for a list of points.</summary>
    public Task<IReadOnlyList<double?>?> GetElevationsAsync(
        IReadOnlyList<(double lat, double lon)> points,
        CancellationToken ct = default)
    {
        var results = new List<double?>();
        foreach (var (lat, lon) in points)
        {
            var key = $"{lat:F2},{lon:F2}";
            if (_cache.TryGetValue(key, out var cached))
            {
                results.Add(cached);
            }
            else
            {
                var mockElev = (Math.Abs(lat * 1000 + lon * 1000) % 3000.0);
                _cache[key] = mockElev;
                results.Add(mockElev);
            }
        }

        return Task.FromResult<IReadOnlyList<double?>?>(results);
    }

    /// <summary>Gets a ground elevation grid around a center point.</summary>
    public Task<double[]?> GetGroundGridAsync(
        double centerLat,
        double centerLon,
        int widthPoints,
        int heightPoints,
        double spacingNm,
        CancellationToken ct = default)
    {
        var grid = new double[widthPoints * heightPoints];
        for (var i = 0; i < grid.Length; i++)
        {
            grid[i] = 1000.0;
        }

        return Task.FromResult<double[]?>(grid);
    }
}

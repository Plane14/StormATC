using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;

namespace StormATC.Infrastructure.Terrain;

/// <summary>MSFS-based terrain provider using SimConnect RequestGroundInfo.</summary>
public sealed class MsfsTerrainProvider : ITerrainProvider
{
    private readonly ILogger<MsfsTerrainProvider> _logger;

    /// <summary>Creates an MSFS terrain provider.</summary>
    public MsfsTerrainProvider(ILogger<MsfsTerrainProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets terrain elevations for a list of points.</summary>
    public Task<IReadOnlyList<double?>?> GetElevationsAsync(
        IReadOnlyList<(double lat, double lon)> points,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Requesting {Count} terrain elevations from MSFS SimConnect ground info.", points.Count);
#if MSFS_SDK_AVAILABLE
        // Real SimConnect integration would go here
#endif
        return Task.FromResult<IReadOnlyList<double?>?>(null);
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
        _logger.LogDebug(
            "Requesting ground grid ({Width}x{Height}, spacing {Spacing} NM) from MSFS SimConnect.",
            widthPoints, heightPoints, spacingNm);
#if MSFS_SDK_AVAILABLE
        // Real SimConnect integration would go here
#endif
        return Task.FromResult<double[]?>(null);
    }
}

/// <summary>Mock terrain provider for offline unit tests.</summary>
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

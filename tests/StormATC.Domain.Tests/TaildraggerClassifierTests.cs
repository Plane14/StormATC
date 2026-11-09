using StormATC.Domain.Aircraft;
using Xunit;

namespace StormATC.Domain.Tests;

/// <summary>Tests for taildragger classification from simulator titles.</summary>
public sealed class TaildraggerClassifierTests
{
    [Theory]
    [InlineData("CubCrafters Carbon Cub")]
    [InlineData("Piper J-3 Cub")]
    [InlineData("Douglas DC-3")]
    [InlineData("De Havilland DH-82 Tiger Moth")]
    [InlineData("P-51D Mustang")]
    [InlineData("Super Decathlon")]
    [InlineData("Aeronca Champ")]
    public void RecognizesTaildraggerTitles(string title)
    {
        Assert.True(TaildraggerClassifier.IsTaildragger(title));
    }

    [Theory]
    [InlineData("Cessna 172 Skyhawk")]
    [InlineData("Airbus A320neo")]
    [InlineData("Beechcraft Bonanza G36")]
    [InlineData("DHC-6 Twin Otter")]
    [InlineData("PMDG 737-800")]
    [InlineData("Cirrus SR22")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsTricycleAndUnknownTitles(string? title)
    {
        Assert.False(TaildraggerClassifier.IsTaildragger(title));
    }

    [Fact]
    public void AircraftStateExposesTaildraggerFlag()
    {
        var taildragger = AircraftState.Unknown(DateTimeOffset.UtcNow) with
        {
            AircraftTitle = "Piper J-3 Cub",
        };
        var tricycle = AircraftState.Unknown(DateTimeOffset.UtcNow) with
        {
            AircraftTitle = "Cessna 172 Skyhawk",
        };

        Assert.True(taildragger.IsTaildragger);
        Assert.False(tricycle.IsTaildragger);
    }
}

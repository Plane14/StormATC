namespace StormATC.Domain.Aircraft;

/// <summary>
/// Classifies whether an aircraft is a taildragger from its simulator title.
/// </summary>
/// <remarks>
/// MSFS does not expose a dedicated taildragger flag through SimConnect, so classification is
/// title-based. The match list covers the taildragger types commonly flown in MSFS 2024 —
/// tailwheel singles, warbirds, and a few tailwheel twins — while deliberately erring toward
/// tricycle gear for ambiguous titles. Matching is case-insensitive and ordinal.
/// </remarks>
public static class TaildraggerClassifier
{
    private static readonly string[] TaildraggerMarkers =
    [
        // Tailwheel singles and twins.
        "cub", "j-3", "j3 ", "aeronca", "champ", "de havilland", "dhc-2",
        "beaver", "dc-3", "dc3", "mustang", "p-51", "spitfire", "hurricane",
        "fw 190", "bf 109", "f4u", "corsair", "zero", "a-4", "harvard",
        "chipmunk", "grob", "cap 10", "cap 232", "cap10", "sv-4", "victa",
        "navion", "aviat", "kitfox", "bushwhacker", "super cub", "supercub",
        "stinson", "tripacer", "travelair", "waco", "pitts", "decathlon",
        "extra 300", "extra-300", "edge 540", "carbon cub",
        // Warbirds and tailwheel aerobatics.
        "f6f", "hellcat", "f8f", "bearcat", "p-40", "warhawk", "p-47",
        "thunderbolt", "p-38", "lightning", "mosquito", "b-25", "b-17",
        "fortress", "avro lancaster", "de havilland dh-82", "dh-82",
        "dh82", "dh-89a", "dh89a", "gipsy moth", "de havilland tiger moth",
    ];

    /// <summary>Returns true when the title matches a known taildragger.</summary>
    public static bool IsTaildragger(string? aircraftTitle)
    {
        if (string.IsNullOrWhiteSpace(aircraftTitle))
        {
            return false;
        }

        string title = aircraftTitle.Trim();
        foreach (string marker in TaildraggerMarkers)
        {
            if (title.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

namespace StormATC.Domain.ValueObjects;

/// <summary>Altitude in feet above mean sea level.</summary>
public readonly record struct AltitudeFeet
{
    /// <summary>Sentinel value indicating the altitude is invalid or unknown.</summary>
    public static readonly AltitudeFeet Invalid = new(double.NaN);

    /// <summary>Zero altitude.</summary>
    public static readonly AltitudeFeet Zero = new(0);

    /// <summary>The altitude in feet.</summary>
    public double Feet { get; }

    /// <summary>Creates an altitude in feet.</summary>
    public AltitudeFeet(double feet) => Feet = feet;

    /// <summary>True when the value is a valid finite number.</summary>
    public bool IsValid => !double.IsNaN(Feet) && double.IsFinite(Feet);

    /// <inheritdoc />
    public override string ToString() => IsValid ? $"{Feet:F0} ft" : "invalid";
}

/// <summary>Speed in knots.</summary>
public readonly record struct SpeedKnots
{
    /// <summary>Zero speed.</summary>
    public static readonly SpeedKnots Zero = new(0);

    /// <summary>The speed in knots.</summary>
    public double Knots { get; }

    /// <summary>Creates a speed in knots.</summary>
    public SpeedKnots(double knots) => Knots = knots;

    /// <inheritdoc />
    public override string ToString() => $"{Knots:F1} kt";
}

/// <summary>Vertical speed in feet per minute.</summary>
public readonly record struct VerticalSpeedFpm
{
    /// <summary>Zero vertical speed.</summary>
    public static readonly VerticalSpeedFpm Zero = new(0);

    /// <summary>The vertical speed in feet per minute.</summary>
    public double Fpm { get; }

    /// <summary>Creates a vertical speed in feet per minute.</summary>
    public VerticalSpeedFpm(double fpm) => Fpm = fpm;

    /// <inheritdoc />
    public override string ToString() => $"{Fpm:F0} fpm";
}

/// <summary>Heading in degrees.</summary>
public readonly record struct HeadingDegrees
{
    /// <summary>Zero heading.</summary>
    public static readonly HeadingDegrees Zero = new(0);

    /// <summary>The heading in degrees.</summary>
    public double Degrees { get; }

    /// <summary>Creates a heading in degrees.</summary>
    public HeadingDegrees(double degrees) => Degrees = degrees;

    /// <inheritdoc />
    public override string ToString() => $"{Degrees:F0}°";
}

/// <summary>Frequency in megahertz.</summary>
public readonly record struct FrequencyMhz
{
    /// <summary>Zero frequency.</summary>
    public static readonly FrequencyMhz Zero = new(0);

    /// <summary>The frequency in megahertz.</summary>
    public double Mhz { get; }

    /// <summary>Creates a frequency in megahertz.</summary>
    public FrequencyMhz(double mhz) => Mhz = mhz;

    /// <inheritdoc />
    public override string ToString() => $"{Mhz:F3} MHz";
}

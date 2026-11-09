namespace StormATC.Domain.ValueObjects;

/// <summary>Altitude in feet above mean sea level.</summary>
public readonly record struct AltitudeFeet
{
    public static readonly AltitudeFeet Invalid = new(double.NaN);
    public static readonly AltitudeFeet Zero = new(0);

    public double Feet { get; }

    public AltitudeFeet(double feet) => Feet = feet;

    public bool IsValid => !double.IsNaN(Feet) && double.IsFinite(Feet);

    public override string ToString() => IsValid ? $"{Feet:F0} ft" : "invalid";
}

/// <summary>Speed in knots.</summary>
public readonly record struct SpeedKnots
{
    public static readonly SpeedKnots Zero = new(0);

    public double Knots { get; }

    public SpeedKnots(double knots) => Knots = knots;

    public override string ToString() => $"{Knots:F1} kt";
}

/// <summary>Vertical speed in feet per minute.</summary>
public readonly record struct VerticalSpeedFpm
{
    public static readonly VerticalSpeedFpm Zero = new(0);

    public double Fpm { get; }

    public VerticalSpeedFpm(double fpm) => Fpm = fpm;

    public override string ToString() => $"{Fpm:F0} fpm";
}

/// <summary>Heading in degrees.</summary>
public readonly record struct HeadingDegrees
{
    public static readonly HeadingDegrees Zero = new(0);

    public double Degrees { get; }

    public HeadingDegrees(double degrees) => Degrees = degrees;

    public override string ToString() => $"{Degrees:F0}°";
}

/// <summary>Frequency in megahertz.</summary>
public readonly record struct FrequencyMhz
{
    public static readonly FrequencyMhz Zero = new(0);

    public double Mhz { get; }

    public FrequencyMhz(double mhz) => Mhz = mhz;

    public override string ToString() => $"{Mhz:F3} MHz";
}

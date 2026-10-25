namespace PartialInnerOutline;

internal static class PartialInnerOutlineSettings
{
    public const double PercentScale = 100.0;
    public const double RadiansPerDegree = Math.PI / 180.0;
    public const double MaximumThickness = 500.0;
    public const int ThicknessDecimals = 1;
    public const int MinimumQuality = 3;
    public const int MaximumQuality = 256;
    public const double MaximumBlur = 1000.0;
    public const double GaussianKernelRadiusInSigmas = 3.0;
    public const double FullBandWidthPercent = 200.0;
    public const double CoverageThreshold = 0.5;
    public const double RingSpacingPixels = 1.0;
    public const double SquareRingCornerStretch = 2.0;

    public static readonly int MaximumStepCount = GetStepCapacity(MaximumThickness);
    public static readonly int MaximumInflation = (int)Math.Ceiling(MaximumThickness) + MaximumStepCount;
    public static readonly int MaximumBlurPadding = GetBlurPadding(MaximumBlur);

    public static int GetStepCapacity(double maximumThickness)
        => (int)Math.Floor(Math.Log2(maximumThickness + 1.0)) + 1;

    public static int DecomposeThickness(double thickness, Span<float> steps)
    {
        var count = 0;
        var remaining = thickness;
        for (var power = 1.0; remaining > 0.0; power *= 2.0)
        {
            var step = Math.Min(remaining, power);
            steps[count++] = (float)step;
            remaining -= step;
        }

        for (var index = 1; index < count; index++)
        {
            var value = steps[index];
            var previous = index - 1;
            while (previous >= 0 && steps[previous] < value)
            {
                steps[previous + 1] = steps[previous];
                previous--;
            }

            steps[previous + 1] = value;
        }

        return count;
    }

    public static int GetRingSampleCount(float step, bool isAngular, int quality)
    {
        var perimeter = 2.0 * Math.PI * step * (isAngular ? SquareRingCornerStretch : 1.0);
        var needed = (int)Math.Ceiling(perimeter / RingSpacingPixels);
        return Math.Clamp(needed, MinimumQuality, Math.Clamp(quality, MinimumQuality, MaximumQuality));
    }

    public static int GetReach(float step)
        => (int)Math.Ceiling(step);

    public static int GetBlurPadding(double blur)
        => (int)Math.Ceiling(Math.Clamp(blur, 0.0, MaximumBlur) * GaussianKernelRadiusInSigmas);

    public static void GetOutputInflations(ReadOnlySpan<float> steps, Span<int> inflations)
    {
        var following = 0;
        for (var index = steps.Length - 1; index >= 0; index--)
        {
            inflations[index] = following;
            following += GetReach(steps[index]);
        }

        for (var index = steps.Length - 1; index > 0; index--)
            inflations[index] -= inflations[index - 1];
    }

    public static FeatherRamp GetFeatherRamp(double smoothness)
    {
        var clamped = Math.Clamp(smoothness, 0.0, 1.0);
        var low = CoverageThreshold * (1.0 - clamped);
        return new FeatherRamp((float)low, clamped > 0.0 ? (float)(1.0 / clamped) : 0f);
    }

    public static BandGeometry GetBand(double angleDegrees, double positionPercent, double widthPercent, double softnessPercent, double left, double top, double right, double bottom)
    {
        var angle = angleDegrees * RadiansPerDegree;
        var directionX = Math.Cos(angle);
        var directionY = -Math.Sin(angle);
        var centerX = (left + right) * 0.5;
        var centerY = (top + bottom) * 0.5;
        var halfExtent = Math.Abs(directionX) * (right - left) * 0.5 + Math.Abs(directionY) * (bottom - top) * 0.5;
        var bandCenter = positionPercent / PercentScale * halfExtent;
        var halfBand = Math.Max(widthPercent / FullBandWidthPercent * halfExtent, 0.0);
        var feather = Math.Clamp(softnessPercent / PercentScale, 0.0, 1.0) * halfBand;
        return new BandGeometry(
            (float)directionX,
            (float)directionY,
            (float)(-(centerX * directionX + centerY * directionY) - bandCenter),
            (float)(halfBand - feather),
            (float)(halfBand + feather));
    }

    internal readonly record struct FeatherRamp(float Low, float InverseWidth);

    internal readonly record struct BandGeometry(float DirectionX, float DirectionY, float Offset, float EdgeNear, float EdgeFar);
}

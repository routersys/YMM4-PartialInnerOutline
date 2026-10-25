namespace PartialInnerOutline.Tests;

public sealed class PartialInnerOutlineSettingsTests
{
    public static readonly TheoryData<double, float[]> Decompositions = new()
    {
        { 0.0, [] },
        { 0.5, [0.5f] },
        { 1.0, [1f] },
        { 2.0, [1f, 1f] },
        { 3.0, [2f, 1f] },
        { 5.0, [2f, 2f, 1f] },
        { 7.0, [4f, 2f, 1f] },
        { 2.5, [1.5f, 1f] },
        { 500.0, [245f, 128f, 64f, 32f, 16f, 8f, 4f, 2f, 1f] },
    };

    public static readonly TheoryData<float[], int[]> Inflations = new()
    {
        { [4f, 3f, 2f, 1f], [6, -3, -2, -1] },
        { [1f], [0] },
        { [0.5f, 0.5f], [1, -1] },
        { [9f, 8f, 4f, 2f, 1f], [15, -8, -4, -2, -1] },
    };

    [Fact]
    public void TheStepCapacityIsEnoughForEveryThickness()
    {
        var steps = new float[PartialInnerOutlineSettings.MaximumStepCount];

        for (var tenths = 0; tenths <= PartialInnerOutlineSettings.MaximumThickness * 10; tenths++)
        {
            var count = PartialInnerOutlineSettings.DecomposeThickness(tenths / 10.0, steps);

            Assert.InRange(count, 0, steps.Length);
        }
    }

    [Fact]
    public void TheStepCapacityOfTheLargestThicknessIsNine()
    {
        Assert.Equal(9, PartialInnerOutlineSettings.MaximumStepCount);
    }

    [Theory]
    [MemberData(nameof(Decompositions))]
    public void ThicknessSplitsIntoDescendingSteps(double thickness, float[] expected)
    {
        var steps = new float[PartialInnerOutlineSettings.MaximumStepCount];

        var count = PartialInnerOutlineSettings.DecomposeThickness(thickness, steps);

        Assert.Equal(expected, steps.AsSpan(0, count).ToArray());
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(3.7)]
    [InlineData(12.3)]
    [InlineData(99.9)]
    [InlineData(250.4)]
    [InlineData(500.0)]
    public void TheStepsAddUpToTheThickness(double thickness)
    {
        var steps = new float[PartialInnerOutlineSettings.MaximumStepCount];

        var count = PartialInnerOutlineSettings.DecomposeThickness(thickness, steps);

        Assert.Equal(thickness, steps.Take(count).Sum(step => (double)step), 3);
    }

    [Theory]
    [InlineData(1f, false, 64, 7)]
    [InlineData(2f, false, 64, 13)]
    [InlineData(4f, false, 64, 26)]
    [InlineData(1f, true, 64, 13)]
    [InlineData(2f, true, 64, 26)]
    [InlineData(10f, false, 8, 8)]
    [InlineData(10f, true, 20, 20)]
    [InlineData(0.1f, false, 64, 3)]
    [InlineData(10f, false, 1, 3)]
    [InlineData(500f, false, 1000, 256)]
    public void TheRingKeepsItsPointsAboutOnePixelApart(float step, bool isAngular, int quality, int expected)
    {
        Assert.Equal(expected, PartialInnerOutlineSettings.GetRingSampleCount(step, isAngular, quality));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3.5f)]
    [InlineData(9f)]
    [InlineData(40f)]
    public void ARingAtFullQualityLeavesNoGapWiderThanOnePixel(float step)
    {
        var count = PartialInnerOutlineSettings.GetRingSampleCount(step, false, PartialInnerOutlineSettings.MaximumQuality);

        Assert.True(2.0 * Math.PI * step / count <= PartialInnerOutlineSettings.RingSpacingPixels);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 1)]
    [InlineData(1f, 1)]
    [InlineData(1.2f, 2)]
    [InlineData(8f, 8)]
    public void TheReachRoundsTheStepUpToWholePixels(float step, int expected)
    {
        Assert.Equal(expected, PartialInnerOutlineSettings.GetReach(step));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(1.0, 3)]
    [InlineData(2.5, 8)]
    [InlineData(1000.0, 3000)]
    [InlineData(-4.0, 0)]
    [InlineData(5000.0, 3000)]
    public void TheBlurPaddingCoversThreeStandardDeviations(double blur, int expected)
    {
        Assert.Equal(expected, PartialInnerOutlineSettings.GetBlurPadding(blur));
    }

    [Theory]
    [MemberData(nameof(Inflations))]
    public void EveryPassKeepsRoomForThePassesAfterIt(float[] steps, int[] expected)
    {
        var inflations = new int[steps.Length];

        PartialInnerOutlineSettings.GetOutputInflations(steps, inflations);

        Assert.Equal(expected, inflations);
    }

    [Fact]
    public void TheLastPassEndsExactlyAtTheImage()
    {
        float[] steps = [9f, 8f, 4f, 2f, 1f];
        var inflations = new int[steps.Length];

        PartialInnerOutlineSettings.GetOutputInflations(steps, inflations);

        Assert.Equal(0, inflations.Sum());
    }

    [Fact]
    public void ThePassesNeverLeaveLessRoomThanThePassesAfterThemNeed()
    {
        float[] steps = [9f, 8f, 4f, 2f, 1f];
        var inflations = new int[steps.Length];

        PartialInnerOutlineSettings.GetOutputInflations(steps, inflations);

        var room = 0;
        for (var index = 0; index < steps.Length; index++)
        {
            room += inflations[index];
            var needed = steps.Skip(index + 1).Sum(step => PartialInnerOutlineSettings.GetReach(step));
            Assert.Equal(needed, room);
        }
    }

    [Fact]
    public void TheLargestInflationsCoverTheLargestThicknessAndTheLargestBlur()
    {
        Assert.Equal(509, PartialInnerOutlineSettings.MaximumInflation);
        Assert.Equal(3000, PartialInnerOutlineSettings.MaximumBlurPadding);
    }

    [Theory]
    [InlineData(1.0, 0f, 1f)]
    [InlineData(0.5, 0.25f, 2f)]
    [InlineData(0.25, 0.375f, 4f)]
    [InlineData(0.0, 0.5f, 0f)]
    [InlineData(2.0, 0f, 1f)]
    [InlineData(-1.0, 0.5f, 0f)]
    public void TheFeatherRampSpansTheSmoothnessAroundHalfCoverage(double smoothness, float low, float inverseWidth)
    {
        var ramp = PartialInnerOutlineSettings.GetFeatherRamp(smoothness);

        Assert.Equal(low, ramp.Low, 4);
        Assert.Equal(inverseWidth, ramp.InverseWidth, 4);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.6)]
    [InlineData(1.0)]
    public void TheFeatherRampIsCenteredOnHalfCoverage(double smoothness)
    {
        var ramp = PartialInnerOutlineSettings.GetFeatherRamp(smoothness);

        Assert.Equal(PartialInnerOutlineSettings.CoverageThreshold, ramp.Low + 0.5 / ramp.InverseWidth, 4);
    }

    [Fact]
    public void ABandAcrossTheWholeHeightReachesTheTopAndBottomEdges()
    {
        var band = PartialInnerOutlineSettings.GetBand(90, 0, 200, 0, -200, -150, 200, 150);

        Assert.Equal(0f, band.DirectionX, 4);
        Assert.Equal(-1f, band.DirectionY, 4);
        Assert.Equal(0f, band.Offset, 3);
        Assert.Equal(150f, band.EdgeNear, 3);
        Assert.Equal(150f, band.EdgeFar, 3);
    }

    [Theory]
    [InlineData(0, 1f, 0f, 200f)]
    [InlineData(90, 0f, -1f, 150f)]
    [InlineData(180, -1f, 0f, 200f)]
    [InlineData(270, 0f, 1f, 150f)]
    public void TheBandAxisPointsInTheChosenDirectionWithTheScreenYAxisDown(double angle, float directionX, float directionY, float halfExtent)
    {
        var band = PartialInnerOutlineSettings.GetBand(angle, 0, 200, 0, -200, -150, 200, 150);

        Assert.Equal(directionX, band.DirectionX, 4);
        Assert.Equal(directionY, band.DirectionY, 4);
        Assert.Equal(halfExtent, band.EdgeNear, 3);
    }

    [Theory]
    [InlineData(100, -150f)]
    [InlineData(-100, 150f)]
    [InlineData(50, -75f)]
    [InlineData(0, 0f)]
    public void ThePositionMovesTheBandCenterByAFractionOfTheHalfExtent(double position, float offset)
    {
        var band = PartialInnerOutlineSettings.GetBand(90, position, 100, 0, -200, -150, 200, 150);

        Assert.Equal(offset, band.Offset, 3);
    }

    [Fact]
    public void TheOffsetFollowsTheCenterOfTheItem()
    {
        var band = PartialInnerOutlineSettings.GetBand(45, 0, 100, 0, 30, 10, 70, 50);

        Assert.Equal(-(50.0 * Math.Cos(Math.PI / 4) - 30.0 * Math.Sin(Math.PI / 4)), band.Offset, 3);
    }

    [Theory]
    [InlineData(100, 75f)]
    [InlineData(200, 150f)]
    [InlineData(50, 37.5f)]
    [InlineData(0, 0f)]
    [InlineData(-30, 0f)]
    public void TheWidthIsMeasuredInHalvesOfTheHalfExtent(double width, float halfBand)
    {
        var band = PartialInnerOutlineSettings.GetBand(90, 0, width, 0, -200, -150, 200, 150);

        Assert.Equal(halfBand, band.EdgeNear, 3);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(30, 0.3)]
    [InlineData(100, 1.0)]
    [InlineData(250, 1.0)]
    [InlineData(-10, 0.0)]
    public void TheSoftnessWidensTheEdgeOnBothSidesOfTheBand(double softness, double fraction)
    {
        var band = PartialInnerOutlineSettings.GetBand(90, 0, 100, softness, -200, -150, 200, 150);

        Assert.Equal(75.0 * (1.0 - fraction), band.EdgeNear, 3);
        Assert.Equal(75.0 * (1.0 + fraction), band.EdgeFar, 3);
    }
}

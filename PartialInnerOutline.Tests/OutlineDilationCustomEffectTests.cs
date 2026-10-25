using Vortice;
using YukkuriMovieMaker.Commons;
using static PartialInnerOutline.OutlineDilationCustomEffect;

namespace PartialInnerOutline.Tests;

[Collection("Direct2D")]
public sealed class OutlineDilationCustomEffectTests
{
    const int Tolerance = 2;

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, Action<OutlineDilationCustomEffect> configure)
    {
        using var effect = new OutlineDilationCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        configure(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    static EffectImpl Impl(float step = 0f, int inflation = 0)
        => new() { StepPixels = step, OutputInflation = inflation };

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineDilationCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineDilationCustomEffect(context);

        Assert.Equal(0f, effect.GetFloatValue((int)PropertyIndex.StepPixels));
        Assert.Equal(0, effect.GetIntValue((int)PropertyIndex.SampleCount));
        Assert.False(effect.GetBoolValue((int)PropertyIndex.IsAngular));
        Assert.False(effect.GetBoolValue((int)PropertyIndex.IsInverted));
        Assert.Equal(0, effect.GetIntValue((int)PropertyIndex.OutputInflation));
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(2.5f, 2.5f)]
    [InlineData(500f, 500f)]
    [InlineData(1000f, 500f)]
    [InlineData(float.PositiveInfinity, 500f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheStepIsClampedToTheLargestThickness(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineDilationCustomEffect(context);

        effect.StepPixels = value;

        Assert.Equal(expected, effect.GetFloatValue((int)PropertyIndex.StepPixels));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(256, 256)]
    [InlineData(1000, 256)]
    public void TheSampleCountIsClampedToTheLargestQuality(int value, int expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineDilationCustomEffect(context);

        effect.SampleCount = value;

        Assert.Equal(expected, effect.GetIntValue((int)PropertyIndex.SampleCount));
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(0)]
    [InlineData(17)]
    public void TheOutputInflationKeepsValuesWithinTheLargestRoomAnyPassNeeds(int value)
    {
        var impl = Impl();

        impl.OutputInflation = value;

        Assert.Equal(value, impl.OutputInflation);
    }

    [Fact]
    public void TheOutputInflationNeverExceedsTheLargestRoomAnyPassNeeds()
    {
        var impl = Impl();

        impl.OutputInflation = int.MaxValue;
        var largest = impl.OutputInflation;
        impl.OutputInflation = int.MinValue;

        Assert.Equal(PartialInnerOutlineSettings.MaximumInflation, largest);
        Assert.Equal(-PartialInnerOutlineSettings.MaximumInflation, impl.OutputInflation);
    }

    [Fact]
    public void TheOutputRectGrowsByTheInflationOnEverySide()
    {
        var impl = Impl(inflation: 3);

        impl.MapInputRectsToOutputRect([new RawRect(0, 0, 32, 24)], [default], out var output, out var opaque);

        Assert.Equal(new RawRect(-3, -3, 35, 27), output);
        Assert.Equal(default, opaque);
    }

    [Fact]
    public void ANegativeInflationShrinksTheOutputRect()
    {
        var impl = Impl(inflation: -2);

        impl.MapInputRectsToOutputRect([new RawRect(-10, -6, 30, 18)], [default], out var output, out _);

        Assert.Equal(new RawRect(-8, -4, 28, 16), output);
    }

    [Fact]
    public void AnInflationLargerThanTheRectNeverTurnsItInsideOut()
    {
        var impl = Impl(inflation: -100);

        impl.MapInputRectsToOutputRect([new RawRect(0, 0, 10, 8)], [default], out var output, out _);

        Assert.True(output.Right >= output.Left);
        Assert.True(output.Bottom >= output.Top);
    }

    [Fact]
    public void AnEmptyInputStaysEmpty()
    {
        var impl = Impl(inflation: 5);

        impl.MapInputRectsToOutputRect([new RawRect(4, 4, 4, 4)], [default], out var output, out _);

        Assert.Equal(new RawRect(4, 4, 4, 4), output);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 1)]
    [InlineData(4f, 4)]
    [InlineData(4.2f, 5)]
    public void TheInputRectReachesTheStepBeyondTheOutputRect(float step, int reach)
    {
        var impl = Impl(step);
        var inputs = new RawRect[1];

        impl.MapOutputRectToInputRects(new RawRect(10, 20, 30, 50), inputs);

        Assert.Equal(new RawRect(10 - reach, 20 - reach, 30 + reach, 50 + reach), inputs[0]);
    }

    [Fact]
    public void AChangedInputRegionInvalidatesTheOutputWithinTheReach()
    {
        var impl = Impl(4.2f);

        var invalid = impl.MapInvalidRect(0, new RawRect(10, 20, 30, 50));

        Assert.Equal(new RawRect(5, 15, 35, 55), invalid);
    }

    [Fact]
    public void WithoutSamplesTheOutputCarriesTheCoverageOfTheInput()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 8, (x, _) => new Bgra(0, 0, 0, (byte)(x * 5)));

        var rendering = Render(context, source, effect => { });

        Assert.All(rendering.Coordinates(), point =>
        {
            var alpha = source[point.X, point.Y].Alpha;
            Assert.InRange(rendering[point.X, point.Y].Alpha, alpha - Tolerance, alpha + Tolerance);
            Assert.InRange(rendering[point.X, point.Y].Red, alpha - Tolerance, alpha + Tolerance);
        });
    }

    [Fact]
    public void AnInvertedPassTurnsTheCoverageUpsideDown()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 8, (x, _) => new Bgra(0, 0, 0, (byte)(x * 5)));

        var rendering = Render(context, source, effect => effect.IsInverted = true);

        Assert.All(rendering.Coordinates(), point =>
        {
            var inverted = byte.MaxValue - source[point.X, point.Y].Alpha;
            Assert.InRange(rendering[point.X, point.Y].Alpha, inverted - Tolerance, inverted + Tolerance);
        });
    }

    [Fact]
    public void TheCoverageSpreadsByTheStepAlongTheHorizontalAxis()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 40, 56, (x, _) => x < 20 ? Bgra.Opaque(0, 0, 0) : Bgra.Transparent);

        var rendering = Render(context, source, effect =>
        {
            effect.StepPixels = 4f;
            effect.SampleCount = 64;
        });

        Assert.Equal(byte.MaxValue, rendering[19, 28].Alpha);
        Assert.InRange(rendering[23, 28].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
        Assert.InRange(rendering[24, 28].Alpha, 0, Tolerance);
        Assert.InRange(rendering[30, 28].Alpha, 0, Tolerance);
    }

    [Fact]
    public void TheCoverageSpreadsByTheStepAlongTheVerticalAxis()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 56, 40, (_, y) => y < 20 ? Bgra.Opaque(0, 0, 0) : Bgra.Transparent);

        var rendering = Render(context, source, effect =>
        {
            effect.StepPixels = 4f;
            effect.SampleCount = 64;
        });

        Assert.InRange(rendering[28, 23].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
        Assert.InRange(rendering[28, 24].Alpha, 0, Tolerance);
    }

    [Fact]
    public void ARoundRingDoesNotReachTheDiagonalCornerThatASquareRingReaches()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 48, (x, y) => x < 20 && y < 20 ? Bgra.Opaque(0, 0, 0) : Bgra.Transparent);

        var round = Render(context, source, effect =>
        {
            effect.StepPixels = 4f;
            effect.SampleCount = 64;
        });
        var square = Render(context, source, effect =>
        {
            effect.StepPixels = 4f;
            effect.SampleCount = 64;
            effect.IsAngular = true;
        });

        Assert.InRange(round[23, 23].Alpha, 0, Tolerance);
        Assert.InRange(square[23, 23].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
    }

    [Fact]
    public void ASquareRingReachesExactlyTheStepAlongBothAxes()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var across = new SourceImage(context, 40, 56, (x, _) => x < 20 ? Bgra.Opaque(0, 0, 0) : Bgra.Transparent);
        using var down = new SourceImage(context, 56, 40, (_, y) => y < 20 ? Bgra.Opaque(0, 0, 0) : Bgra.Transparent);

        void Square(OutlineDilationCustomEffect effect)
        {
            effect.StepPixels = 4f;
            effect.SampleCount = 64;
            effect.IsAngular = true;
        }

        var horizontal = Render(context, across, Square);
        var vertical = Render(context, down, Square);

        Assert.InRange(horizontal[23, 28].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
        Assert.InRange(horizontal[24, 28].Alpha, 0, Tolerance);
        Assert.InRange(vertical[28, 23].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
        Assert.InRange(vertical[28, 24].Alpha, 0, Tolerance);
    }

    [Fact]
    public void TheInflatedAreaOutsideTheSourceIsTransparentCoverage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 32, 24, Bgra.Opaque(0, 0, 0));

        var plain = Render(context, source, effect => effect.OutputInflation = 3);
        var inverted = Render(context, source, effect =>
        {
            effect.OutputInflation = 3;
            effect.IsInverted = true;
        });

        Assert.Equal((-3, -3, 35, 27), (plain.Left, plain.Top, plain.Right, plain.Bottom));
        Assert.Equal(byte.MaxValue, plain[10, 10].Alpha);
        Assert.InRange(plain[-2, 5].Alpha, 0, Tolerance);
        Assert.Equal(byte.MaxValue, inverted[-2, 5].Alpha);
        Assert.Equal(byte.MaxValue, inverted[33, 26].Alpha);
        Assert.InRange(inverted[10, 10].Alpha, 0, Tolerance);
    }
}

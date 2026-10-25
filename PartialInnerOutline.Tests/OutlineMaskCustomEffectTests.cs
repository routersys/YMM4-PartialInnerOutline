using Vortice;
using YukkuriMovieMaker.Commons;
using static PartialInnerOutline.OutlineMaskCustomEffect;

namespace PartialInnerOutline.Tests;

[Collection("Direct2D")]
public sealed class OutlineMaskCustomEffectTests
{
    const int Tolerance = 2;
    const float Unbounded = 1e9f;

    static readonly byte[] Coverages = [0, 32, 64, 96, 128, 160, 192, 224, 255];

    static float Read(OutlineMaskCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, Action<OutlineMaskCustomEffect> configure)
    {
        using var effect = new OutlineMaskCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        configure(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    static Rendering RenderCoverages(IGraphicsDevicesAndContext devices, Action<OutlineMaskCustomEffect> configure)
    {
        using var source = new SourceImage(devices, Coverages.Length, 2, (x, _) => new Bgra(0, 0, 0, Coverages[x]));

        return Render(devices, source, effect =>
        {
            effect.BandEdgeNear = Unbounded;
            effect.BandEdgeFar = Unbounded;
            configure(effect);
        });
    }

    static double SmoothStep(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        return clamped * clamped * (3.0 - 2.0 * clamped);
    }

    static void AssertAlpha(double expected, byte actual, string where)
        => Assert.True(Math.Abs(expected * byte.MaxValue - actual) <= Tolerance, $"{where} expected {expected * byte.MaxValue:F1} but {actual}");

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        Assert.Equal(0.5f, Read(effect, PropertyIndex.FeatherLow));
        Assert.Equal(0f, Read(effect, PropertyIndex.FeatherInverseWidth));
        Assert.Equal(1f, Read(effect, PropertyIndex.Opacity));
        Assert.Equal(0f, Read(effect, PropertyIndex.BandOffset));
        Assert.Equal(0f, Read(effect, PropertyIndex.BandDirectionX));
        Assert.Equal(0f, Read(effect, PropertyIndex.BandDirectionY));
        Assert.Equal(0f, Read(effect, PropertyIndex.BandEdgeNear));
        Assert.Equal(0f, Read(effect, PropertyIndex.BandEdgeFar));
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(2f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheFeatherLowAndTheOpacityAreClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        effect.FeatherLow = value;
        effect.Opacity = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.FeatherLow));
        Assert.Equal(expected, Read(effect, PropertyIndex.Opacity));
    }

    [Theory]
    [InlineData(-3f, 0f)]
    [InlineData(4f, 4f)]
    [InlineData(float.PositiveInfinity, float.MaxValue)]
    [InlineData(float.NaN, 0f)]
    public void TheWidthsNeverGoNegative(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        effect.FeatherInverseWidth = value;
        effect.BandEdgeNear = value;
        effect.BandEdgeFar = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.FeatherInverseWidth));
        Assert.Equal(expected, Read(effect, PropertyIndex.BandEdgeNear));
        Assert.Equal(expected, Read(effect, PropertyIndex.BandEdgeFar));
    }

    [Theory]
    [InlineData(-2f, -1f)]
    [InlineData(-0.5f, -0.5f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(3f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, -1f)]
    [InlineData(float.NaN, 0f)]
    public void TheBandDirectionStaysInsideTheUnitSquare(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        effect.BandDirectionX = value;
        effect.BandDirectionY = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.BandDirectionX));
        Assert.Equal(expected, Read(effect, PropertyIndex.BandDirectionY));
    }

    [Theory]
    [InlineData(-250f, -250f)]
    [InlineData(1234.5f, 1234.5f)]
    [InlineData(float.PositiveInfinity, float.MaxValue)]
    [InlineData(float.NegativeInfinity, float.MinValue)]
    [InlineData(float.NaN, 0f)]
    public void TheBandOffsetKeepsAnyFiniteDistance(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new OutlineMaskCustomEffect(context);

        effect.BandOffset = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.BandOffset));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(3000, 3000)]
    [InlineData(1000000, 3000)]
    public void TheOutputInflationIsClampedToTheLargestBlurPadding(int value, int expected)
    {
        var impl = new EffectImpl { OutputInflation = value };

        Assert.Equal(expected, impl.OutputInflation);
    }

    [Fact]
    public void TheOutputRectGrowsByTheInflationAndTheInputRectStaysAsRequested()
    {
        var impl = new EffectImpl { OutputInflation = 3 };
        var inputs = new RawRect[1];

        impl.MapInputRectsToOutputRect([new RawRect(0, 0, 32, 24)], [default], out var output, out var opaque);
        impl.MapOutputRectToInputRects(new RawRect(-3, -3, 35, 27), inputs);

        Assert.Equal(new RawRect(-3, -3, 35, 27), output);
        Assert.Equal(default, opaque);
        Assert.Equal(new RawRect(-3, -3, 35, 27), inputs[0]);
    }

    [Fact]
    public void AnEmptyInputStaysEmpty()
    {
        var impl = new EffectImpl { OutputInflation = 5 };

        impl.MapInputRectsToOutputRect([new RawRect(4, 4, 4, 4)], [default], out var output, out _);

        Assert.Equal(new RawRect(4, 4, 4, 4), output);
    }

    [Fact]
    public void TheMarginAroundTheSourceCountsAsFullCoverage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 16, 12, Bgra.Transparent);

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.BandEdgeNear = Unbounded;
            effect.BandEdgeFar = Unbounded;
            effect.Opacity = 0.5f;
            effect.OutputInflation = 4;
        });

        Assert.Equal((-4, -4, 20, 16), (rendering.Left, rendering.Top, rendering.Right, rendering.Bottom));
        Assert.InRange(rendering[-4, -4].Alpha, 128 - Tolerance, 128 + Tolerance);
        Assert.InRange(rendering[19, 15].Alpha, 128 - Tolerance, 128 + Tolerance);
        Assert.InRange(rendering[8, -1].Alpha, 128 - Tolerance, 128 + Tolerance);
        Assert.Equal(0, rendering[0, 0].Alpha);
        Assert.Equal(0, rendering[15, 11].Alpha);
        Assert.Equal(0, rendering[8, 6].Alpha);
    }

    [Fact]
    public void TheMarginFollowsTheRectOfTheSourceWhereverItIs()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 16, 12, Bgra.Transparent);
        using var move = new Vortice.Direct2D1.Effects.AffineTransform2D(context.DeviceContext) { TransformMatrix = System.Numerics.Matrix3x2.CreateTranslation(37f, 21f) };
        move.SetInput(0, source.Bitmap, true);
        using var moved = move.Output;
        using var effect = new OutlineMaskCustomEffect(context);
        effect.SetInput(0, moved, true);
        effect.FeatherLow = 0f;
        effect.FeatherInverseWidth = 1f;
        effect.BandEdgeNear = Unbounded;
        effect.BandEdgeFar = Unbounded;
        effect.OutputInflation = 4;
        using var output = effect.Output;

        var rendering = Rendering.Capture(context, output);

        Assert.Equal((33, 17, 57, 37), (rendering.Left, rendering.Top, rendering.Right, rendering.Bottom));
        Assert.Equal(byte.MaxValue, rendering[34, 25].Alpha);
        Assert.Equal(byte.MaxValue, rendering[45, 18].Alpha);
        Assert.Equal(0, rendering[37, 21].Alpha);
        Assert.Equal(0, rendering[52, 32].Alpha);
    }

    [Fact]
    public void TheOutputIsAGrayAlphaMaskThatFollowsTheSmoothStepOfTheCoverage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = RenderCoverages(context, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
        });

        for (var x = 0; x < Coverages.Length; x++)
        {
            var pixel = rendering[x, 0];
            AssertAlpha(SmoothStep(Coverages[x] / 255.0), pixel.Alpha, $"x={x}");
            Assert.Equal(pixel.Alpha, pixel.Red);
            Assert.Equal(pixel.Alpha, pixel.Green);
            Assert.Equal(pixel.Alpha, pixel.Blue);
        }
    }

    [Fact]
    public void ANarrowerRampStartsLaterAndEndsEarlierAroundHalfCoverage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = RenderCoverages(context, effect =>
        {
            effect.FeatherLow = 0.25f;
            effect.FeatherInverseWidth = 2f;
        });

        for (var x = 0; x < Coverages.Length; x++)
            AssertAlpha(SmoothStep((Coverages[x] / 255.0 - 0.25) * 2.0), rendering[x, 0].Alpha, $"x={x}");
    }

    [Fact]
    public void WithoutAWidthTheCoverageIsCutAtTheLowEdge()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 3, 1, (x, _) => new Bgra(0, 0, 0, (byte)(126 + x * 2)));

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0.5f;
            effect.FeatherInverseWidth = 0f;
            effect.BandEdgeNear = Unbounded;
            effect.BandEdgeFar = Unbounded;
        });

        Assert.Equal(0, rendering[0, 0].Alpha);
        Assert.Equal(byte.MaxValue, rendering[1, 0].Alpha);
        Assert.Equal(byte.MaxValue, rendering[2, 0].Alpha);
    }

    [Theory]
    [InlineData(0.4f)]
    [InlineData(0.75f)]
    [InlineData(0f)]
    public void TheOpacityScalesTheMask(float opacity)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = RenderCoverages(context, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.Opacity = opacity;
        });

        for (var x = 0; x < Coverages.Length; x++)
            AssertAlpha(opacity * SmoothStep(Coverages[x] / 255.0), rendering[x, 0].Alpha, $"x={x}");
    }

    [Fact]
    public void ABandWithHardEdgesKeepsTheRowsAroundItsCenter()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 8, 48, Bgra.Opaque(0, 0, 0));

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.BandDirectionY = 1f;
            effect.BandOffset = -24f;
            effect.BandEdgeNear = 10f;
            effect.BandEdgeFar = 10f;
        });

        for (var y = 0; y < 48; y++)
        {
            var center = y + 0.5 - 24.0;
            if (Math.Abs(Math.Abs(center) - 10.0) < 1.0)
                continue;

            Assert.Equal(Math.Abs(center) < 10.0 ? byte.MaxValue : 0, rendering[3, y].Alpha);
        }
    }

    [Fact]
    public void TheBandEdgesFadeBetweenTheNearAndFarDistancesWithASmoothStep()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 8, 64, Bgra.Opaque(0, 0, 0));

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.BandDirectionY = 1f;
            effect.BandOffset = -32f;
            effect.BandEdgeNear = 5f;
            effect.BandEdgeFar = 15f;
        });

        for (var y = 0; y < 64; y++)
        {
            var distance = Math.Abs(y + 0.5 - 32.0);
            AssertAlpha(1.0 - SmoothStep((distance - 5.0) / 10.0), rendering[3, y].Alpha, $"y={y}");
        }
    }

    [Fact]
    public void TheBandFollowsAnyDirectionOfItsAxis()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 48, 36, Bgra.Opaque(0, 0, 0));

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.BandDirectionX = 0.6f;
            effect.BandDirectionY = 0.8f;
            effect.BandOffset = -30f;
            effect.BandEdgeNear = 8f;
            effect.BandEdgeFar = 8f;
        });

        var inside = 0;
        foreach (var (x, y) in rendering.Coordinates())
        {
            var distance = Math.Abs(0.6 * (x + 0.5) + 0.8 * (y + 0.5) - 30.0);
            if (Math.Abs(distance - 8.0) < 1.0)
                continue;

            var expected = distance < 8.0 ? byte.MaxValue : 0;
            Assert.Equal(expected, rendering[x, y].Alpha);
            inside += expected == byte.MaxValue ? 1 : 0;
        }

        Assert.InRange(inside, 100, 48 * 36 - 100);
    }

    [Fact]
    public void ATransparentSourceHasNoMaskWhateverTheBandIs()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 16, 16, Bgra.Transparent);

        var rendering = Render(context, source, effect =>
        {
            effect.FeatherLow = 0f;
            effect.FeatherInverseWidth = 1f;
            effect.BandEdgeNear = Unbounded;
            effect.BandEdgeFar = Unbounded;
        });

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
    }
}

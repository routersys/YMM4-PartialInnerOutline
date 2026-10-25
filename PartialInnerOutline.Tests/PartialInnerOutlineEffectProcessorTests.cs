using System.Globalization;
using System.Numerics;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace PartialInnerOutline.Tests;

[Collection("Direct2D")]
public sealed class PartialInnerOutlineEffectProcessorTests
{
    const int Length = 30;
    const int Tolerance = 3;

    static readonly Bgra Crimson = Bgra.Opaque(60, 20, 220);
    static readonly Bgra Fabric = Bgra.Opaque(230, 150, 100);

    static Bgra Block(int x, int y) => x is >= 16 and < 80 && y is >= 12 and < 52 ? Fabric : Bgra.Transparent;

    static Bgra FullBleed(int x, int y) => Fabric;

    static Bgra Disc(int x, int y)
    {
        var distance = Math.Sqrt((x + 0.5 - 48.0) * (x + 0.5 - 48.0) + (y + 0.5 - 32.0) * (y + 0.5 - 32.0));
        var coverage = Math.Clamp(24.0 - distance + 0.5, 0.0, 1.0);
        return new Bgra(230, 150, 100, (byte)Math.Round(coverage * byte.MaxValue));
    }

    static Bgra Notch(int x, int y) => x >= 48 && y < 48 ? Bgra.Transparent : Fabric;

    static PartialInnerOutlineEffect Outline(Action<PartialInnerOutlineEffect>? configure = null)
        => TestEffects.Create(effect =>
        {
            effect.IsOutlineOnly = true;
            effect.Quality.Values[0].Value = PartialInnerOutlineSettings.MaximumQuality;
            effect.BandWidth.Values[0].Value = PartialInnerOutlineSettings.FullBandWidthPercent;
            effect.BandSoftness.Values[0].Value = 0d;
            TestEffects.Paint(effect, Colors.Crimson);
            configure?.Invoke(effect);
        });

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    static Rendering Draw(IGraphicsDevicesAndContext devices, PartialInnerOutlineEffect effect, int width, int height, Func<int, int, Bgra> pixel, int frame = 0)
    {
        using var source = new SourceImage(devices, width, height, pixel);
        using var processor = effect.CreateVideoEffect(devices);
        processor.SetInput(source.Bitmap);

        return RenderFrame(devices, processor, frame);
    }

    static Bgra At(Rendering rendering, int x, int y) => rendering.Contains(x, y) ? rendering[x, y] : Bgra.Transparent;

    static int Reach(Rendering rendering, int row, int from)
    {
        var count = 0;
        while (At(rendering, from + count, row).Alpha >= 128)
            count++;
        return count;
    }

    static void AssertColor(Bgra expected, Bgra actual, string where)
    {
        Assert.True(Math.Abs(expected.Blue - actual.Blue) <= Tolerance, $"{where} blue {expected} {actual}");
        Assert.True(Math.Abs(expected.Green - actual.Green) <= Tolerance, $"{where} green {expected} {actual}");
        Assert.True(Math.Abs(expected.Red - actual.Red) <= Tolerance, $"{where} red {expected} {actual}");
        Assert.True(Math.Abs(expected.Alpha - actual.Alpha) <= Tolerance, $"{where} alpha {expected} {actual}");
    }

    static double SmoothStep(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        return clamped * clamped * (3.0 - 2.0 * clamped);
    }

    [Fact]
    public void TheProcessorHandsTheDrawDescriptionBackUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 96, 64, Block);
        using var processor = Outline().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Same(description.DrawDescription, draw);
    }

    public static readonly TheoryData<string, Action<PartialInnerOutlineEffect>> Invisible = new()
    {
        { nameof(PartialInnerOutlineEffect.Thickness), effect => effect.Thickness.Values[0].Value = 0d },
        { nameof(PartialInnerOutlineEffect.Opacity), effect => effect.Opacity.Values[0].Value = 0d },
        { nameof(PartialInnerOutlineEffect.BandWidth), effect => effect.BandWidth.Values[0].Value = 0d },
    };

    [Theory]
    [MemberData(nameof(Invisible))]
    public void AnOutlineThatCannotBeSeenPassesTheImageThrough(string setting, Action<PartialInnerOutlineEffect> hide)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = TestEffects.Create(effect => { TestEffects.Paint(effect, Colors.Crimson); hide(effect); });
        using var source = new SourceImage(context, 96, 64, Block);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.True((0, 0, 96, 64) == (rendering.Left, rendering.Top, rendering.Width, rendering.Height), setting);
        Assert.All(rendering.Coordinates(), point => Assert.True(source[point.X, point.Y] == rendering[point.X, point.Y], $"{setting} {point}"));
    }

    [Fact]
    public void ThePassedThroughImageFollowsTheInputThatIsSetLater()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = TestEffects.Create(effect => { TestEffects.Paint(effect, Colors.Crimson); effect.Thickness.Values[0].Value = 0d; });
        using var first = new SourceImage(context, 96, 64, Block);
        using var second = new SourceImage(context, 96, 64, FullBleed);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(first.Bitmap);
        RenderFrame(context, processor, 0);

        processor.SetInput(second.Bitmap);
        var rendering = RenderFrame(context, processor, 1);

        Assert.All(rendering.Coordinates(), point => Assert.True(second[point.X, point.Y] == rendering[point.X, point.Y], $"{point}"));
    }

    [Theory]
    [MemberData(nameof(Invisible))]
    public void AnOutlineThatCannotBeSeenLeavesNothingWhenOnlyTheOutlineIsShown(string setting, Action<PartialInnerOutlineEffect> hide)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(hide);

        var rendering = Draw(context, effect, 96, 64, Block);

        Assert.All(rendering.Coordinates(), point => Assert.True(rendering[point.X, point.Y] == Bgra.Transparent, $"{setting} {point}"));
    }

    [Fact]
    public void TheOutlineHugsTheInsideOfTheShape()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.Values[0].Value = 6d);

        var rendering = Draw(context, effect, 96, 64, Block);

        foreach (var x in new[] { 16, 17, 18, 19, 76, 77, 78, 79 })
            AssertColor(Crimson, rendering[x, 32], $"x={x}");
        foreach (var x in new[] { 0, 8, 15, 24, 30, 48, 70, 72, 80, 90 })
            Assert.InRange(rendering[x, 32].Alpha, 0, Tolerance);
        foreach (var y in new[] { 12, 13, 14, 15, 48, 49, 50, 51 })
            AssertColor(Crimson, rendering[48, y], $"y={y}");
        foreach (var y in new[] { 0, 11, 20, 32, 44, 52, 63 })
            Assert.InRange(rendering[48, y].Alpha, 0, Tolerance);
    }

    [Theory]
    [InlineData(2d)]
    [InlineData(5d)]
    [InlineData(10d)]
    [InlineData(17d)]
    public void TheThicknessSetsHowFarTheOutlineReachesIn(double thickness)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.Values[0].Value = thickness);

        var rendering = Draw(context, effect, 96, 64, Block);

        Assert.InRange(Reach(rendering, 32, 16), thickness - 1, thickness + 1);
    }

    [Fact]
    public void TheOutlineNeverLeavesTheShapeEvenWhenBlurred()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 10d;
            e.Blur.Values[0].Value = 8d;
        });

        var rendering = Draw(context, effect, 96, 64, Disc);

        Assert.All(rendering.Coordinates(), point => Assert.InRange(rendering[point.X, point.Y].Alpha, 0, Disc(point.X, point.Y).Alpha + 1));
        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha > byte.MaxValue / 2);
    }

    [Fact]
    public void TheOutlineAlsoRunsAlongTheBorderOfTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.Values[0].Value = 6d);

        var rendering = Draw(context, effect, 64, 48, FullBleed);

        foreach (var x in new[] { 0, 1, 2, 3 })
            AssertColor(Crimson, rendering[x, 24], $"x={x}");
        foreach (var x in new[] { 60, 61, 62, 63 })
            AssertColor(Crimson, rendering[x, 24], $"x={x}");
        foreach (var y in new[] { 0, 1, 2, 3 })
            AssertColor(Crimson, rendering[32, y], $"y={y}");
        foreach (var y in new[] { 44, 45, 46, 47 })
            AssertColor(Crimson, rendering[32, y], $"y={y}");
        Assert.InRange(rendering[32, 24].Alpha, 0, Tolerance);
    }

    [Theory]
    [InlineData(1.5d)]
    [InlineData(2d)]
    [InlineData(5d)]
    [InlineData(10d)]
    [InlineData(17d)]
    public void TheThicknessSetsHowFarTheOutlineReachesInFromTheBorderOfTheImage(double thickness)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.Values[0].Value = thickness);

        var rendering = Draw(context, effect, 96, 64, FullBleed);

        Assert.InRange(Reach(rendering, 32, 0), thickness - 1, thickness + 1);
        Assert.InRange(rendering[48, 0].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
        Assert.InRange(rendering[0, 32].Alpha, byte.MaxValue - Tolerance, byte.MaxValue);
    }

    [Fact]
    public void TheBlurredOutlineKeepsItsStrengthAtTheBorderWhenTheBlurGrowsLater()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.Thickness.Values[0].Value = 10d; e.Blur.CopyFrom(Linear(0d, 6d)); });
        using var source = new SourceImage(context, 64, 48, FullBleed);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        RenderFrame(context, processor, 0);

        var rendering = RenderFrame(context, processor, Length - 1);

        Assert.InRange(rendering[0, 24].Alpha, 232, byte.MaxValue);
        Assert.InRange(rendering[63, 24].Alpha, 232, byte.MaxValue);
    }

    [Fact]
    public void TheBandKeepsTheOutlineWithinItsRows()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 4d;
            e.BandWidth.Values[0].Value = 100d;
        });

        var rendering = Draw(context, effect, 64, 48, FullBleed);

        for (var y = 0; y < 48; y++)
        {
            var distance = Math.Abs(y + 0.5 - 24.0);
            if (Math.Abs(distance - 12.0) < 1.0)
                continue;

            Assert.Equal(distance < 12.0 ? byte.MaxValue : 0, rendering[1, y].Alpha);
        }
    }

    [Fact]
    public void ThePositionMovesTheBandUpAlongItsAxis()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 4d;
            e.BandWidth.Values[0].Value = 100d;
            e.BandPosition.Values[0].Value = 50d;
        });

        var rendering = Draw(context, effect, 64, 48, FullBleed);

        for (var y = 0; y < 48; y++)
        {
            var distance = Math.Abs(y + 0.5 - 12.0);
            if (Math.Abs(distance - 12.0) < 1.0)
                continue;

            Assert.Equal(distance < 12.0 ? byte.MaxValue : 0, rendering[1, y].Alpha);
        }
    }

    [Fact]
    public void TheDirectionTurnsTheBandAlongTheHorizontalAxis()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 4d;
            e.BandWidth.Values[0].Value = 100d;
            e.BandDirection.Values[0].Value = 0d;
        });

        var rendering = Draw(context, effect, 64, 48, FullBleed);

        for (var x = 0; x < 64; x++)
        {
            var distance = Math.Abs(x + 0.5 - 32.0);
            if (Math.Abs(distance - 16.0) < 1.0)
                continue;

            Assert.Equal(distance < 16.0 ? byte.MaxValue : 0, rendering[x, 1].Alpha);
        }
    }

    [Fact]
    public void TheSoftnessFadesTheOutlineTowardTheBandEdges()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 4d;
            e.BandWidth.Values[0].Value = 100d;
            e.BandSoftness.Values[0].Value = 100d;
        });

        var rendering = Draw(context, effect, 64, 48, FullBleed);

        for (var y = 0; y < 48; y++)
        {
            var distance = Math.Abs(y + 0.5 - 24.0);
            var expected = 1.0 - SmoothStep(distance / 24.0);
            Assert.InRange(rendering[1, y].Alpha, expected * byte.MaxValue - Tolerance - 2, expected * byte.MaxValue + Tolerance + 2);
        }
    }

    [Fact]
    public void ARoundOutlineDoesNotReachTheDiagonalOfAnInnerCornerThatASquareOutlineReaches()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var round = Draw(context, Outline(e => e.Thickness.Values[0].Value = 20d), 96, 96, Notch);
        var square = Draw(context, Outline(e => { e.Thickness.Values[0].Value = 20d; e.IsAngular = true; }), 96, 96, Notch);

        Assert.InRange(round[30, 65].Alpha, 0, Tolerance);
        AssertColor(Crimson, square[30, 65], "square");
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.CopyFrom(Linear(2d, 20d)));
        using var source = new SourceImage(context, 96, 64, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 5);
        RenderFrame(context, processor, 25);
        var again = RenderFrame(context, processor, 5);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void AnimatedThicknessIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => e.Thickness.CopyFrom(Linear(2d, 20d)));
        using var source = new SourceImage(context, 96, 64, Block);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.InRange(Reach(start, 32, 16), 1, 3);
        Assert.InRange(Reach(end, 32, 16), 19, 21);
    }

    public static readonly TheoryData<string, Action<PartialInnerOutlineEffect>> Changes = new()
    {
        { nameof(PartialInnerOutlineEffect.Thickness), effect => effect.Thickness.Values[0].Value = 14d },
        { nameof(PartialInnerOutlineEffect.Quality), effect => effect.Quality.Values[0].Value = 4d },
        { nameof(PartialInnerOutlineEffect.Smoothness), effect => effect.Smoothness.Values[0].Value = 0d },
        { nameof(PartialInnerOutlineEffect.IsAngular), effect => effect.IsAngular = true },
        { nameof(PartialInnerOutlineEffect.BandDirection), effect => effect.BandDirection.Values[0].Value = 0d },
        { nameof(PartialInnerOutlineEffect.BandPosition), effect => effect.BandPosition.Values[0].Value = 50d },
        { nameof(PartialInnerOutlineEffect.BandWidth), effect => effect.BandWidth.Values[0].Value = 30d },
        { nameof(PartialInnerOutlineEffect.BandSoftness), effect => effect.BandSoftness.Values[0].Value = 0d },
        { nameof(PartialInnerOutlineEffect.Opacity), effect => effect.Opacity.Values[0].Value = 40d },
        { nameof(PartialInnerOutlineEffect.Blur), effect => effect.Blur.Values[0].Value = 6d },
        { nameof(PartialInnerOutlineEffect.Blend), effect => effect.Blend = Blend.Multiply },
        { nameof(PartialInnerOutlineEffect.IsOutlineOnly), effect => effect.IsOutlineOnly = true },
        { nameof(PartialInnerOutlineEffect.Brush), effect => TestEffects.Paint(effect, Colors.SeaGreen) },
    };

    static PartialInnerOutlineEffect Base(Action<PartialInnerOutlineEffect>? change = null)
        => TestEffects.Create(effect =>
        {
            effect.Thickness.Values[0].Value = 8d;
            effect.Quality.Values[0].Value = PartialInnerOutlineSettings.MaximumQuality;
            TestEffects.Paint(effect, Colors.Crimson);
            change?.Invoke(effect);
        });

    [Theory]
    [MemberData(nameof(Changes))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheOutput(string setting, Action<PartialInnerOutlineEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Base();
        using var source = new SourceImage(context, 96, 64, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 3);
        change(effect);
        var after = RenderFrame(context, processor, 3);

        Assert.False(before.SamePixelsAs(after), setting);
        Assert.True(after.SamePixelsAs(Draw(context, Base(change), 96, 64, Disc, 3)), setting);
    }

    [Fact]
    public void AProcessorThatDrewOtherThicknessesDrawsLikeAFreshOne()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Base(e => e.Thickness.Values[0].Value = 24d);
        using var source = new SourceImage(context, 96, 64, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        RenderFrame(context, processor, 0);
        effect.Thickness.Values[0].Value = 2d;
        RenderFrame(context, processor, 0);
        effect.Thickness.Values[0].Value = 12d;
        var reused = RenderFrame(context, processor, 0);

        Assert.True(reused.SamePixelsAs(Draw(context, Base(e => e.Thickness.Values[0].Value = 12d), 96, 64, Disc)));
    }

    [Fact]
    public void TheOutlineReturnsOnceItBecomesVisibleAgain()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Base(e => e.Opacity.Values[0].Value = 0d);
        using var source = new SourceImage(context, 96, 64, Disc);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var hidden = RenderFrame(context, processor, 0);
        effect.Opacity.Values[0].Value = 100d;
        var shown = RenderFrame(context, processor, 0);

        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
        Assert.True(shown.SamePixelsAs(Draw(context, Base(), 96, 64, Disc)));
    }

    [Fact]
    public void TheOutlineTravelsWithTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 6d;
            e.BandWidth.Values[0].Value = 100d;
            e.BandPosition.Values[0].Value = 30d;
            e.BandDirection.Values[0].Value = 30d;
        });
        var expected = Draw(context, effect, 96, 64, Disc);
        using var source = new SourceImage(context, 96, 64, Disc);
        using var move = new Vortice.Direct2D1.Effects.AffineTransform2D(context.DeviceContext) { TransformMatrix = Matrix3x2.CreateTranslation(37f, 21f) };
        move.SetInput(0, source.Bitmap, true);
        using var moved = move.Output;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(moved);

        var rendering = RenderFrame(context, processor, 0);

        Assert.All(expected.Coordinates(), point => AssertColor(expected[point.X, point.Y], At(rendering, point.X + 37, point.Y + 21), $"{point}"));
    }

    [Theory]
    [InlineData(50d, 128)]
    [InlineData(25d, 64)]
    public void TheOpacityScalesTheOutline(double opacity, int alpha)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 6d;
            e.Opacity.Values[0].Value = opacity;
        });

        var rendering = Draw(context, effect, 96, 64, Block);

        var pixel = rendering[16, 32];
        Assert.InRange(pixel.Alpha, alpha - Tolerance, alpha + Tolerance);
        Assert.InRange(pixel.Red, Crimson.Red * alpha / 255 - Tolerance, Crimson.Red * alpha / 255 + Tolerance);
    }

    [Fact]
    public void TheBrushColorFillsTheOutline()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e =>
        {
            e.Thickness.Values[0].Value = 6d;
            TestEffects.Paint(e, Color.FromRgb(30, 200, 90));
        });

        var rendering = Draw(context, effect, 96, 64, Block);

        AssertColor(Bgra.Opaque(90, 200, 30), rendering[16, 32], "pixel");
    }

    [Fact]
    public void TheNormalBlendPutsTheOutlineOverTheShape()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.IsOutlineOnly = false; e.Thickness.Values[0].Value = 6d; });

        var rendering = Draw(context, effect, 96, 64, Block);

        AssertColor(Crimson, rendering[16, 32], "outline");
        AssertColor(Fabric, rendering[48, 32], "inside");
        Assert.InRange(rendering[8, 32].Alpha, 0, Tolerance);
    }

    [Fact]
    public void TheMultiplyBlendMultipliesTheOutlineWithTheShape()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.IsOutlineOnly = false; e.Thickness.Values[0].Value = 6d; e.Blend = Blend.Multiply; });

        var rendering = Draw(context, effect, 96, 64, Block);

        AssertColor(Bgra.Opaque(54, 12, 86), rendering[16, 32], "outline");
        AssertColor(Fabric, rendering[48, 32], "inside");
    }

    [Fact]
    public void TheAddBlendAddsTheOutlineToTheShape()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.IsOutlineOnly = false; e.Thickness.Values[0].Value = 6d; e.Blend = Blend.Add; });

        var rendering = Draw(context, effect, 96, 64, Block);

        AssertColor(Bgra.Opaque(255, 170, 255), rendering[16, 32], "outline");
        AssertColor(Fabric, rendering[48, 32], "inside");
    }

    [Fact]
    public void ABlurSpreadsTheOutlineFurtherInsideThanTheSharpOne()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var sharp = Draw(context, Outline(e => e.Thickness.Values[0].Value = 4d), 64, 48, FullBleed);
        var blurred = Draw(context, Outline(e => { e.Thickness.Values[0].Value = 4d; e.Blur.Values[0].Value = 6d; }), 64, 48, FullBleed);

        Assert.InRange(sharp[8, 24].Alpha, 0, Tolerance);
        Assert.True(blurred[8, 24].Alpha > 16);
        for (var x = 1; x < 20; x++)
            Assert.True(blurred[x, 24].Alpha <= blurred[x - 1, 24].Alpha, $"x={x}");
    }

    [Theory]
    [InlineData(0d, 0, 0)]
    [InlineData(100d, 10, int.MaxValue)]
    public void TheSmoothnessDecidesWhetherTheInnerEdgeOfTheOutlineIsAbrupt(double smoothness, int minimum, int maximum)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.Thickness.Values[0].Value = 8d; e.Smoothness.Values[0].Value = smoothness; });

        var rendering = Draw(context, effect, 96, 64, Disc);

        var gradual = rendering.Coordinates().Count(point => Disc(point.X, point.Y).Alpha == byte.MaxValue && rendering[point.X, point.Y].Alpha is > 8 and < 247);
        Assert.InRange(gradual, minimum, maximum);
    }

    [Theory]
    [InlineData(100d)]
    [InlineData(50d)]
    [InlineData(25d)]
    [InlineData(0d)]
    public void TheSmoothnessSetsHowStronglyAPartialCoverageShowsThrough(double smoothness)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Outline(e => { e.Thickness.Values[0].Value = 4d; e.Smoothness.Values[0].Value = smoothness; });

        var rendering = Draw(context, effect, 64, 48, (_, _) => new Bgra(100, 150, 230, 153));

        var fraction = smoothness / 100.0;
        var mask = fraction > 0.0 ? SmoothStep((0.4 - 0.5 * (1.0 - fraction)) / fraction) : 0.0;
        Assert.InRange(rendering[32, 24].Alpha, 0.6 * mask * byte.MaxValue - Tolerance, 0.6 * mask * byte.MaxValue + Tolerance);
    }

    [Fact]
    public void TheBlurredOutlineKeepsItsStrengthAtTheBorderOfTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var rendering = Draw(context, Outline(e => { e.Thickness.Values[0].Value = 10d; e.Blur.Values[0].Value = 6d; }), 64, 48, FullBleed);

        Assert.InRange(rendering[0, 24].Alpha, 232, byte.MaxValue);
        Assert.InRange(rendering[63, 24].Alpha, 232, byte.MaxValue);
    }

    [Fact]
    public void ATransparentSourceStaysTransparentInEveryMode()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();

        var only = Draw(context, Outline(e => e.Blur.Values[0].Value = 5d), 32, 32, (_, _) => Bgra.Transparent);
        var normal = Draw(context, Outline(e => e.IsOutlineOnly = false), 32, 32, (_, _) => Bgra.Transparent);

        Assert.All(only.Coordinates(), point => Assert.Equal(Bgra.Transparent, only[point.X, point.Y]));
        Assert.All(normal.Coordinates(), point => Assert.Equal(Bgra.Transparent, normal[point.X, point.Y]));
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Outline().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        Assert.ThrowsAny<Exception>(() => Outline().CreateVideoEffect(null!));
    }
}

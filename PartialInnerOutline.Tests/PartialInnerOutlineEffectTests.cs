using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace PartialInnerOutline.Tests;

public sealed class PartialInnerOutlineEffectTests
{
    static PropertyInfo Property(string name) => typeof(PartialInnerOutlineEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(PartialInnerOutlineEffect effect)
        => [effect.Thickness, effect.Quality, effect.Smoothness, effect.BandDirection, effect.BandPosition, effect.BandWidth, effect.BandSoftness, effect.Opacity, effect.Blur];

    [Theory]
    [InlineData(nameof(PartialInnerOutlineEffect.Thickness), 3d, 0d, 500d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Quality), 64d, 3d, 256d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Smoothness), 100d, 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandDirection), 90d, 0d, 360d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandPosition), 0d, -200d, 200d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandWidth), 100d, 0d, 200d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandSoftness), 30d, 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Opacity), 100d, 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Blur), 0d, 0d, 1000d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = TestEffects.Create();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheRangesMatchTheLimitsTheProcessorWorksWith()
    {
        var effect = TestEffects.Create();

        Assert.Equal(PartialInnerOutlineSettings.MaximumThickness, effect.Thickness.MaxValue);
        Assert.Equal(PartialInnerOutlineSettings.MinimumQuality, effect.Quality.MinValue);
        Assert.Equal(PartialInnerOutlineSettings.MaximumQuality, effect.Quality.MaxValue);
        Assert.Equal(PartialInnerOutlineSettings.MaximumBlur, effect.Blur.MaxValue);
        Assert.Equal(PartialInnerOutlineSettings.FullBandWidthPercent, effect.BandWidth.MaxValue);
    }

    [Fact]
    public void TheOptionsStartTurnedOffWithNormalBlending()
    {
        var effect = TestEffects.Create();

        Assert.False(effect.IsAngular);
        Assert.False(effect.IsOutlineOnly);
        Assert.Equal(Blend.Normal, effect.Blend);
    }

    [Fact]
    public void TheAnimatedParametersAreListedOnceAndReused()
    {
        var effect = TestEffects.Create();
        var method = typeof(PartialInnerOutlineEffect).GetMethod("GetAnimatables", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var first = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;
        var second = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;

        Assert.Same(first, second);
        Assert.Equal([.. Animations(effect), effect.Brush], first);
    }

    [Fact]
    public void TheLabelNamesTheEffectAndTheThickness()
    {
        var effect = TestEffects.Create(e => e.Thickness.Values[0].Value = 7);

        Assert.Equal($"{Texts.PartialInnerOutline} 7px", effect.Label);
    }

    [Fact]
    public void TheNineNumericParametersReceiveTheAnimationParameters()
    {
        var effect = TestEffects.Create();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.Equal(9, Animations(effect).Length);
        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = TestEffects.Create();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(PartialInnerOutlineEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Fact]
    public void TheEffectIsRegisteredForDecorationWithoutAviUtlSupport()
    {
        var attribute = typeof(PartialInnerOutlineEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.PartialInnerOutline), attribute.Name);
        Assert.Equal([VideoEffectCategories.Decoration], attribute.Categories);
        Assert.Equal(
            [nameof(Texts.TagPartialInnerOutline), nameof(Texts.TagInnerOutline), nameof(Texts.TagPartialOutline), nameof(Texts.TagOutline), nameof(Texts.TagBorder)],
            attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.PartialInnerOutline, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(PartialInnerOutlineEffect.Thickness), nameof(Texts.OutlineGroup), nameof(Texts.Thickness), nameof(Texts.ThicknessDescription), 0)]
    [InlineData(nameof(PartialInnerOutlineEffect.Quality), nameof(Texts.OutlineGroup), nameof(Texts.Quality), nameof(Texts.QualityDescription), 1)]
    [InlineData(nameof(PartialInnerOutlineEffect.Smoothness), nameof(Texts.OutlineGroup), nameof(Texts.Smoothness), nameof(Texts.SmoothnessDescription), 2)]
    [InlineData(nameof(PartialInnerOutlineEffect.IsAngular), nameof(Texts.OutlineGroup), nameof(Texts.IsAngular), nameof(Texts.IsAngularDescription), 3)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandDirection), nameof(Texts.BandGroup), nameof(Texts.BandDirection), nameof(Texts.BandDirectionDescription), 10)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandPosition), nameof(Texts.BandGroup), nameof(Texts.BandPosition), nameof(Texts.BandPositionDescription), 11)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandWidth), nameof(Texts.BandGroup), nameof(Texts.BandWidth), nameof(Texts.BandWidthDescription), 12)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandSoftness), nameof(Texts.BandGroup), nameof(Texts.BandSoftness), nameof(Texts.BandSoftnessDescription), 13)]
    [InlineData(nameof(PartialInnerOutlineEffect.Opacity), nameof(Texts.AppearanceGroup), nameof(Texts.Opacity), nameof(Texts.OpacityDescription), 20)]
    [InlineData(nameof(PartialInnerOutlineEffect.Blur), nameof(Texts.AppearanceGroup), nameof(Texts.Blur), nameof(Texts.BlurDescription), 21)]
    [InlineData(nameof(PartialInnerOutlineEffect.Blend), nameof(Texts.AppearanceGroup), nameof(Texts.Blend), nameof(Texts.BlendDescription), 22)]
    [InlineData(nameof(PartialInnerOutlineEffect.IsOutlineOnly), nameof(Texts.AppearanceGroup), nameof(Texts.IsOutlineOnly), nameof(Texts.IsOutlineOnlyDescription), 23)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Fact]
    public void TheBrushIsDisplayedInItsOwnGroup()
    {
        var display = Attribute<DisplayAttribute>(nameof(PartialInnerOutlineEffect.Brush));

        Assert.Equal(nameof(Texts.BrushGroup), display.GroupName);
        Assert.True(display.AutoGenerateField);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(PartialInnerOutlineEffect.Thickness), "F1", "px", 0d, 10d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Quality), "F0", "", 3d, 64d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Smoothness), "F1", "%", 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandDirection), "F1", "°", 0d, 360d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandPosition), "F1", "%", -100d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandWidth), "F1", "%", 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.BandSoftness), "F1", "%", 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Opacity), "F1", "%", 0d, 100d)]
    [InlineData(nameof(PartialInnerOutlineEffect.Blur), "F1", "px", 0d, 5d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void TheOptionsAreEditedWithTogglesAndAComboBox()
    {
        Assert.NotNull(Attribute<ToggleSliderAttribute>(nameof(PartialInnerOutlineEffect.IsAngular)));
        Assert.NotNull(Attribute<ToggleSliderAttribute>(nameof(PartialInnerOutlineEffect.IsOutlineOnly)));
        Assert.NotNull(Attribute<EnumComboBoxAttribute>(nameof(PartialInnerOutlineEffect.Blend)));
    }

    [Fact]
    public void ChangingAnOptionNotifiesTheEditor()
    {
        var effect = TestEffects.Create();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName);

        effect.IsAngular = true;
        effect.IsOutlineOnly = true;
        effect.Blend = Blend.Multiply;

        Assert.Equal([nameof(PartialInnerOutlineEffect.IsAngular), nameof(PartialInnerOutlineEffect.IsOutlineOnly), nameof(PartialInnerOutlineEffect.Blend)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedOptionDoesNotNotify()
    {
        var effect = TestEffects.Create();
        var notified = false;
        effect.PropertyChanged += (_, _) => notified = true;

        effect.IsAngular = false;
        effect.IsOutlineOnly = false;
        effect.Blend = Blend.Normal;

        Assert.False(notified);
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = TestEffects.Create(e =>
        {
            e.IsAngular = true;
            e.IsOutlineOnly = true;
            e.Blend = Blend.Multiply;
        });
        var values = new[] { 7.5d, 20d, 40d, 135d, -35d, 60d, 70d, 80d, 3.5d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
        Assert.True(clone.IsAngular);
        Assert.True(clone.IsOutlineOnly);
        Assert.Equal(Blend.Multiply, clone.Blend);
        Assert.Equal(effect.Brush.Type, clone.Brush.Type);
    }

    [Fact]
    public void TheFilesOfTheBrushAreReportedAndReplaced()
    {
        var parameter = new FileBrushParameter("pattern.png");
        var effect = TestEffects.Create(e => e.Brush.Parameter = parameter);

        Assert.Equal(["pattern.png"], effect.GetFiles());

        effect.ReplaceFile("pattern.png", "other.png");

        Assert.Equal(["other.png"], effect.GetFiles());
    }

    [Fact]
    public void TheResourcesOfTheBrushAreReported()
    {
        var parameter = new FileBrushParameter("pattern.png");
        var effect = TestEffects.Create(e => e.Brush.Parameter = parameter);

        Assert.Contains(FileBrushParameter.Resource, effect.GetResources());
    }

    sealed class FileBrushParameter(string file) : BrushParameterBase, IFileItem
    {
        public static readonly TimelineResource Resource = TimelineResource.FromApplication("pattern", "author", new ContentId("nc1"));

        string file = file;

        public IEnumerable<string> GetFiles() => [file];

        public void ReplaceFile(string from, string to)
        {
            if (file == from)
                file = to;
        }

        public IEnumerable<TimelineResource> GetResources() => [Resource];

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices) => throw new NotSupportedException();
    }
}

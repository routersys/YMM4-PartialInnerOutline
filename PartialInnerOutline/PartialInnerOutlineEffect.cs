using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace PartialInnerOutline;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(
    nameof(Texts.PartialInnerOutline),
    [VideoEffectCategories.Decoration],
    [nameof(Texts.TagPartialInnerOutline), nameof(Texts.TagInnerOutline), nameof(Texts.TagPartialOutline), nameof(Texts.TagOutline), nameof(Texts.TagBorder)],
    IsAviUtlSupported = false,
    ResourceType = typeof(Texts))]
public sealed class PartialInnerOutlineEffect : VideoEffectBase, IFileItem
{
    public override string Label => $"{Texts.PartialInnerOutline} {Thickness.GetValue(0, 1, 30):F0}px";

    public PartialInnerOutlineEffect()
    {
        PartialInnerOutlineTelemetry.EnsureStartedOnce();
        PartialInnerOutlineUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.OutlineGroup), Name = nameof(Texts.Thickness), Description = nameof(Texts.ThicknessDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, 10)]
    public Animation Thickness { get; } = new Animation(3, 0, PartialInnerOutlineSettings.MaximumThickness);

    [Display(GroupName = nameof(Texts.OutlineGroup), Name = nameof(Texts.Quality), Description = nameof(Texts.QualityDescription), Order = 1, ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "", PartialInnerOutlineSettings.MinimumQuality, 64)]
    public Animation Quality { get; } = new Animation(64, PartialInnerOutlineSettings.MinimumQuality, PartialInnerOutlineSettings.MaximumQuality);

    [Display(GroupName = nameof(Texts.OutlineGroup), Name = nameof(Texts.Smoothness), Description = nameof(Texts.SmoothnessDescription), Order = 2, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Smoothness { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.OutlineGroup), Name = nameof(Texts.IsAngular), Description = nameof(Texts.IsAngularDescription), Order = 3, ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool IsAngular { get => _isAngular; set => Set(ref _isAngular, value); }
    private bool _isAngular;

    [Display(GroupName = nameof(Texts.BandGroup), Name = nameof(Texts.BandDirection), Description = nameof(Texts.BandDirectionDescription), Order = 10, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", 0, 360)]
    public Animation BandDirection { get; } = new Animation(90, 0, 360);

    [Display(GroupName = nameof(Texts.BandGroup), Name = nameof(Texts.BandPosition), Description = nameof(Texts.BandPositionDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation BandPosition { get; } = new Animation(0, -200, 200);

    [Display(GroupName = nameof(Texts.BandGroup), Name = nameof(Texts.BandWidth), Description = nameof(Texts.BandWidthDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation BandWidth { get; } = new Animation(100, 0, PartialInnerOutlineSettings.FullBandWidthPercent);

    [Display(GroupName = nameof(Texts.BandGroup), Name = nameof(Texts.BandSoftness), Description = nameof(Texts.BandSoftnessDescription), Order = 13, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation BandSoftness { get; } = new Animation(30, 0, 100);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Opacity), Description = nameof(Texts.OpacityDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Opacity { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Blur), Description = nameof(Texts.BlurDescription), Order = 21, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, 5)]
    public Animation Blur { get; } = new Animation(0, 0, PartialInnerOutlineSettings.MaximumBlur);

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.Blend), Description = nameof(Texts.BlendDescription), Order = 22, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public Blend Blend { get => _blend; set => Set(ref _blend, value); }
    private Blend _blend = Blend.Normal;

    [Display(GroupName = nameof(Texts.AppearanceGroup), Name = nameof(Texts.IsOutlineOnly), Description = nameof(Texts.IsOutlineOnlyDescription), Order = 23, ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool IsOutlineOnly { get => _isOutlineOnly; set => Set(ref _isOutlineOnly, value); }
    private bool _isOutlineOnly;

    [Display(GroupName = nameof(Texts.BrushGroup), ResourceType = typeof(Texts), AutoGenerateField = true)]
    public YukkuriMovieMaker.Plugin.Brush.Brush Brush { get; } = new YukkuriMovieMaker.Plugin.Brush.Brush();

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new PartialInnerOutlineEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            PartialInnerOutlineTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Thickness, Quality, Smoothness, BandDirection, BandPosition, BandWidth, BandSoftness, Opacity, Blur, Brush];

    public override IEnumerable<string> GetFiles()
    {
        foreach (var file in base.GetFiles())
            yield return file;
        foreach (var file in Brush.GetFiles())
            yield return file;
    }

    public override void ReplaceFile(string from, string to)
    {
        base.ReplaceFile(from, to);
        Brush.ReplaceFile(from, to);
    }

    public override IEnumerable<TimelineResource> GetResources()
    {
        foreach (var resource in base.GetResources())
            yield return resource;
        foreach (var resource in Brush.GetResources())
            yield return resource;
    }
}

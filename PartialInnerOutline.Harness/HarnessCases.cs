using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Project;

namespace PartialInnerOutline.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, PartialInnerOutlineEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("thickness-animated-frames", Create(effect => effect.Thickness.CopyFrom(Linear(1d, 24d))), [0, 100, 200, 299]);
        yield return ("band-direction-animated-frames", Create(effect => effect.BandDirection.CopyFrom(Linear(0d, 360d))), [0, 100, 200, 299]);
        yield return ("band-position-animated-frames", Create(effect => effect.BandPosition.CopyFrom(Linear(-100d, 100d))), [0, 100, 200, 299]);
        yield return ("blur-animated-frames", Create(effect => effect.Blur.CopyFrom(Linear(0d, 12d))), [0, 100, 200, 299]);
        yield return ("thickness-1", Create(effect => effect.Thickness.Values[0].Value = 1), [0]);
        yield return ("thickness-8", Create(effect => effect.Thickness.Values[0].Value = 8), [0]);
        yield return ("thickness-24", Create(effect => effect.Thickness.Values[0].Value = 24), [0]);
        yield return ("quality-3", Create(effect => effect.Quality.Values[0].Value = 3), [0]);
        yield return ("quality-8", Create(effect => effect.Quality.Values[0].Value = 8), [0]);
        yield return ("smoothness-0", Create(effect => effect.Smoothness.Values[0].Value = 0), [0]);
        yield return ("smoothness-50", Create(effect => effect.Smoothness.Values[0].Value = 50), [0]);
        yield return ("angular", Create(effect => { effect.Thickness.Values[0].Value = 12; effect.IsAngular = true; }), [0]);
        yield return ("band-direction-0", Create(effect => effect.BandDirection.Values[0].Value = 0), [0]);
        yield return ("band-direction-45", Create(effect => effect.BandDirection.Values[0].Value = 45), [0]);
        yield return ("band-direction-180-position-60", Create(effect => { effect.BandDirection.Values[0].Value = 180; effect.BandPosition.Values[0].Value = 60; }), [0]);
        yield return ("band-position-minus-60", Create(effect => effect.BandPosition.Values[0].Value = -60), [0]);
        yield return ("band-position-60", Create(effect => effect.BandPosition.Values[0].Value = 60), [0]);
        yield return ("band-width-30", Create(effect => effect.BandWidth.Values[0].Value = 30), [0]);
        yield return ("band-width-200", Create(effect => effect.BandWidth.Values[0].Value = 200), [0]);
        yield return ("band-softness-0", Create(effect => effect.BandSoftness.Values[0].Value = 0), [0]);
        yield return ("band-softness-100", Create(effect => effect.BandSoftness.Values[0].Value = 100), [0]);
        yield return ("opacity-0", Create(effect => effect.Opacity.Values[0].Value = 0), [0]);
        yield return ("opacity-50", Create(effect => effect.Opacity.Values[0].Value = 50), [0]);
        yield return ("blur-4", Create(effect => effect.Blur.Values[0].Value = 4), [0]);
        yield return ("blur-12", Create(effect => effect.Blur.Values[0].Value = 12), [0]);
        yield return ("blend-add", Create(effect => effect.Blend = Blend.Add), [0]);
        yield return ("blend-multiply", Create(effect => effect.Blend = Blend.Multiply), [0]);
        yield return ("outline-only", Create(effect => effect.IsOutlineOnly = true), [0]);
    }

    public static IEnumerable<(string Name, Func<PartialInnerOutlineEffect> Create, Action<PartialInnerOutlineEffect> Change, int Frame)> Transitions()
    {
        yield return ("thickness-3-to-10", () => Create(), effect => effect.Thickness.Values[0].Value = 10, 0);
        yield return ("thickness-10-to-3", () => Create(effect => effect.Thickness.Values[0].Value = 10), effect => effect.Thickness.Values[0].Value = 3, 0);
        yield return ("thickness-3-to-0", () => Create(), effect => effect.Thickness.Values[0].Value = 0, 0);
        yield return ("thickness-0-to-3", () => Create(effect => effect.Thickness.Values[0].Value = 0), effect => effect.Thickness.Values[0].Value = 3, 0);
        yield return ("quality-64-to-8", () => Create(), effect => effect.Quality.Values[0].Value = 8, 0);
        yield return ("smoothness-100-to-0", () => Create(), effect => effect.Smoothness.Values[0].Value = 0, 0);
        yield return ("angular-off-to-on", () => Create(), effect => effect.IsAngular = true, 0);
        yield return ("band-direction-90-to-0", () => Create(), effect => effect.BandDirection.Values[0].Value = 0, 0);
        yield return ("band-position-0-to-50", () => Create(), effect => effect.BandPosition.Values[0].Value = 50, 0);
        yield return ("band-width-100-to-30", () => Create(), effect => effect.BandWidth.Values[0].Value = 30, 0);
        yield return ("band-softness-30-to-0", () => Create(), effect => effect.BandSoftness.Values[0].Value = 0, 0);
        yield return ("opacity-100-to-50", () => Create(), effect => effect.Opacity.Values[0].Value = 50, 0);
        yield return ("opacity-100-to-0", () => Create(), effect => effect.Opacity.Values[0].Value = 0, 0);
        yield return ("opacity-0-to-100", () => Create(effect => effect.Opacity.Values[0].Value = 0), effect => effect.Opacity.Values[0].Value = 100, 0);
        yield return ("blur-0-to-8", () => Create(), effect => effect.Blur.Values[0].Value = 8, 0);
        yield return ("blend-normal-to-multiply", () => Create(), effect => effect.Blend = Blend.Multiply, 0);
        yield return ("blend-multiply-to-add", () => Create(effect => effect.Blend = Blend.Multiply), effect => effect.Blend = Blend.Add, 0);
        yield return ("outline-only-off-to-on", () => Create(), effect => effect.IsOutlineOnly = true, 0);
        yield return ("outline-only-on-to-off", () => Create(effect => effect.IsOutlineOnly = true), effect => effect.IsOutlineOnly = false, 0);
    }

    public static IEnumerable<(string Name, PartialInnerOutlineEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("thickness-24", Create(effect => effect.Thickness.Values[0].Value = 24));
        yield return ("thickness-animated", Create(effect => effect.Thickness.CopyFrom(Linear(1d, 24d))));
        yield return ("band-direction-animated", Create(effect => effect.BandDirection.CopyFrom(Linear(0d, 360d))));
        yield return ("blur-animated", Create(effect => effect.Blur.CopyFrom(Linear(0d, 12d))));
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}""")) ?? throw new HarnessException("アニメーションを読み込めません。");

    public static PartialInnerOutlineEffect Create(Action<PartialInnerOutlineEffect>? configure = null)
    {
        BrushPluginHost.Register();
        var effect = new PartialInnerOutlineEffect();
        ((dynamic)effect.Brush.Parameter).Color = System.Windows.Media.Colors.Crimson;
        configure?.Invoke(effect);
        return effect;
    }
}

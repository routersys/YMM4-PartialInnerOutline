namespace PartialInnerOutline.Tests;

internal static class TestEffects
{
    public static PartialInnerOutlineEffect Create(Action<PartialInnerOutlineEffect>? configure = null)
    {
        BrushPluginHost.Register();
        var effect = new PartialInnerOutlineEffect();
        configure?.Invoke(effect);
        return effect;
    }
}

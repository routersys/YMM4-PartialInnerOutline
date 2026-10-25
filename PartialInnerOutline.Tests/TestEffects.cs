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

    public static void Paint(PartialInnerOutlineEffect effect, System.Windows.Media.Color color)
        => ((dynamic)effect.Brush.Parameter).Color = color;
}

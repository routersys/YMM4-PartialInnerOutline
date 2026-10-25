using Vortice;

namespace PartialInnerOutline;

internal static class RawRectExtensions
{
    public static RawRect Inflate(this RawRect rect, int amount)
    {
        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            return rect;

        var left = rect.Left - amount;
        var top = rect.Top - amount;
        return new RawRect(left, top, Math.Max(rect.Right + amount, left), Math.Max(rect.Bottom + amount, top));
    }
}

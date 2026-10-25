namespace PartialInnerOutline;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/PartialInnerOutline;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}

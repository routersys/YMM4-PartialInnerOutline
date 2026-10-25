using System.Reflection;
using YukkuriMovieMaker.Plugin;

namespace PartialInnerOutline.Harness;

internal static class BrushPluginHost
{
    const string HostAssemblyName = "YukkuriMovieMaker";
    const string SolidColorPluginTypeName = "YukkuriMovieMaker.Plugin.Brush.SolidColorBrushPlugin";

    static readonly Lock Gate = new();
    static bool registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (registered)
                return;

            var type = Assembly.Load(HostAssemblyName).GetType(SolidColorPluginTypeName, throwOnError: true)!;
            var plugin = (IPlugin)Activator.CreateInstance(type, nonPublic: true)!;
            typeof(PluginLoader).GetProperty(nameof(PluginLoader.Plugins))!.SetValue(null, new[] { plugin });
            registered = true;
        }
    }
}

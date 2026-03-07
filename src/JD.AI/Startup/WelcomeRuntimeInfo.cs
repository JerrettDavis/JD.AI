using System.Reflection;

namespace JD.AI.Startup;

internal static class WelcomeRuntimeInfo
{
    public static string GetDisplayVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+')[0];

        var version = assembly.GetName().Version;
        return version is null
            ? "unknown"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}

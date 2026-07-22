using System.Reflection;

namespace ExpeditionsMacro.Core.Runtime;

public static class ProductVersion
{
    public static string Current
    {
        get
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                return informational.Split('+', 2)[0];
            }

            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }
    }
}

using System.Reflection;

namespace Plmh.Core;

/// <summary>Build identity embedded into the PLMH assembly.</summary>
public sealed record ApplicationVersion(Version Number, DateTimeOffset? BuildTimestampUtc)
{
    public string DisplayName => $"PLMH v{Number}";

    public static ApplicationVersion Current { get; } = FromAssembly(typeof(ApplicationVersion).Assembly);

    public static ApplicationVersion FromAssembly(Assembly assembly)
    {
        var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? "");
        var number = Version.TryParse(attributes.GetValueOrDefault("ApplicationVersion"), out var version)
            ? version
            : new Version(assembly.GetName().Version?.Major ?? 0, assembly.GetName().Version?.Minor ?? 0, 0);
        var timestamp = DateTimeOffset.TryParse(attributes.GetValueOrDefault("BuildTimestampUtc"),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsedTimestamp) ? parsedTimestamp : (DateTimeOffset?)null;
        return new ApplicationVersion(number, timestamp);
    }
}

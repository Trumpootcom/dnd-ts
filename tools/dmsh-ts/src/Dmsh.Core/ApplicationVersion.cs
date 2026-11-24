using System.Reflection;

namespace Dmsh.Core;

/// <summary>Build identity embedded into assemblies by the shared MSBuild versioning targets.</summary>
public sealed record ApplicationVersion(
    Version Number,
    string CommitHash,
    string BuildState,
    DateTimeOffset? BuildTimestampUtc)
{
    public string DisplayName =>
        $"WMS Desktop v{Number} ({CommitHash}{(BuildState == "Modified" ? ", Modified" : "")})";

    public static ApplicationVersion Current { get; } = FromAssembly(typeof(ApplicationVersion).Assembly);

    public static ApplicationVersion FromAssembly(Assembly assembly)
    {
        var attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? "");
        var number = Version.TryParse(attributes.GetValueOrDefault("ApplicationVersion"), out var version)
            ? version
            : new Version(assembly.GetName().Version?.Major ?? 0, assembly.GetName().Version?.Minor ?? 0, 0);
        var timestamp = DateTimeOffset.TryParse(
            attributes.GetValueOrDefault("BuildTimestampUtc"),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsedTimestamp)
            ? parsedTimestamp
            : (DateTimeOffset?)null;

        return new ApplicationVersion(
            number,
            attributes.GetValueOrDefault("GitCommitHash", "unknown"),
            attributes.GetValueOrDefault("BuildState", "Unknown"),
            timestamp);
    }
}

using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SharedWorlds.Desktop;

/// <summary>
/// SafeWorld release-facing Steam configuration boundary. Canonical SafeWorld inputs are parsed here
/// directly. Older engineering inputs remain readable only as an explicit compatibility fallback.
/// </summary>
internal sealed record SafeWorldDesktopSteamConfiguration(uint AppId)
{
    internal const string PackageConfigurationFileName = "safeworld-steam.json";
    internal const string SteamAppIdVariable = "SAFEWORLD_STEAM_APP_ID";
    private const string LegacyPackageConfigurationFileName = "steward-steam.json";
    private const string LegacySteamAppIdVariable = "STEWARD_STEAM_APP_ID";
    private const int SchemaVersion = 1;
    private const int MaximumConfigurationBytes = 1024;

    public static bool TryLoad(
        out SafeWorldDesktopSteamConfiguration? configuration,
        out string? problem)
    {
        configuration = null;

        var canonicalPath = Path.Combine(
            AppContext.BaseDirectory,
            PackageConfigurationFileName);
        var canonicalEnvironmentAppId = Environment.GetEnvironmentVariable(
            SteamAppIdVariable);
        var legacyPath = Path.Combine(
            AppContext.BaseDirectory,
            LegacyPackageConfigurationFileName);
        var legacyEnvironmentAppId = Environment.GetEnvironmentVariable(
            LegacySteamAppIdVariable);

        var canonicalPackageConfigured = File.Exists(canonicalPath);
        var canonicalEnvironmentConfigured = !string.IsNullOrWhiteSpace(canonicalEnvironmentAppId);
        var legacyPackageConfigured = File.Exists(legacyPath);
        var legacyEnvironmentConfigured = !string.IsNullOrWhiteSpace(legacyEnvironmentAppId);

        var configuredSourceCount = 0;
        configuredSourceCount += canonicalPackageConfigured ? 1 : 0;
        configuredSourceCount += canonicalEnvironmentConfigured ? 1 : 0;
        configuredSourceCount += legacyPackageConfigured ? 1 : 0;
        configuredSourceCount += legacyEnvironmentConfigured ? 1 : 0;
        if (configuredSourceCount > 1)
        {
            problem =
                "Steam platform configuration is ambiguous. Configure exactly one SafeWorld or legacy AppID source.";
            return false;
        }

        if (canonicalEnvironmentConfigured)
        {
            if (!TryParsePositiveAppId(canonicalEnvironmentAppId, out var appId))
            {
                problem = $"{SteamAppIdVariable} must be a positive Steam AppID.";
                return false;
            }

            configuration = new SafeWorldDesktopSteamConfiguration(appId);
            problem = null;
            return true;
        }

        if (canonicalPackageConfigured)
        {
            return TryLoadCanonicalPackage(
                canonicalPath,
                out configuration,
                out problem);
        }

        if (legacyPackageConfigured || legacyEnvironmentConfigured)
        {
            if (!StewardDesktopSteamConfiguration.TryLoad(
                    out var legacy,
                    out problem))
            {
                return false;
            }

            if (legacy is null)
            {
                problem = null;
                return false;
            }

            configuration = new SafeWorldDesktopSteamConfiguration(legacy.AppId);
            problem = null;
            return true;
        }

        problem = null;
        return false;
    }

    private static bool TryLoadCanonicalPackage(
        string path,
        out SafeWorldDesktopSteamConfiguration? configuration,
        out string? problem)
    {
        configuration = null;
        try
        {
            var file = new FileInfo(Path.GetFullPath(path));
            file.Refresh();
            if (!file.Exists)
            {
                problem = null;
                return false;
            }

            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                problem = $"{PackageConfigurationFileName} must be a regular file, not a linked file.";
                return false;
            }

            if (file.Length is <= 0 or > MaximumConfigurationBytes)
            {
                problem = $"{PackageConfigurationFileName} has an invalid size.";
                return false;
            }

            var bytes = File.ReadAllBytes(file.FullName);
            if (bytes.Length is <= 0 or > MaximumConfigurationBytes)
            {
                problem = $"{PackageConfigurationFileName} has an invalid size.";
                return false;
            }

            using var document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3
                });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problem = $"{PackageConfigurationFileName} must contain one JSON object.";
                return false;
            }

            int? schemaVersion = null;
            uint? appId = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "schemaVersion":
                        if (schemaVersion is not null ||
                            property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out var parsedSchemaVersion))
                        {
                            problem = $"{PackageConfigurationFileName} contains an invalid schemaVersion.";
                            return false;
                        }

                        schemaVersion = parsedSchemaVersion;
                        break;
                    case "steamAppId":
                        if (appId is not null ||
                            property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetUInt32(out var parsedAppId))
                        {
                            problem = $"{PackageConfigurationFileName} contains an invalid steamAppId.";
                            return false;
                        }

                        appId = parsedAppId;
                        break;
                    default:
                        problem = $"{PackageConfigurationFileName} contains unsupported field '{property.Name}'.";
                        return false;
                }
            }

            if (schemaVersion != SchemaVersion)
            {
                problem = $"{PackageConfigurationFileName} uses an unsupported schema version.";
                return false;
            }

            if (appId is null or 0)
            {
                problem = $"{PackageConfigurationFileName} must contain a positive steamAppId.";
                return false;
            }

            configuration = new SafeWorldDesktopSteamConfiguration(appId.Value);
            problem = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            problem = $"{PackageConfigurationFileName} could not be read safely: {exception.Message}";
            return false;
        }
    }

    private static bool TryParsePositiveAppId(
        string? text,
        out uint appId)
        => uint.TryParse(
               text,
               NumberStyles.None,
               CultureInfo.InvariantCulture,
               out appId) &&
           appId != 0;
}

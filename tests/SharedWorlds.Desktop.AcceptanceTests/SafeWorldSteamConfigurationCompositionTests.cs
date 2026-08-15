using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class SafeWorldSteamConfigurationCompositionTests
{
    [Fact]
    public void PublicSteamConfigurationUsesOnlySafeWorldNames()
    {
        var source = ReadRepositoryFile(
            "src/SharedWorlds.Desktop/SafeWorldDesktopSteamConfiguration.cs");

        Assert.Contains(
            "PackageConfigurationFileName = \"safeworld-steam.json\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SteamAppIdVariable = \"SAFEWORLD_STEAM_APP_ID\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("steward-steam.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("STEWARD_STEAM_APP_ID", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardDesktopSteamConfiguration", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeWorldConfigurationFailsClosedWhenCanonicalSourcesAreMixed()
    {
        var source = ReadRepositoryFile(
            "src/SharedWorlds.Desktop/SafeWorldDesktopSteamConfiguration.cs");

        Assert.Contains("packageConfigured && environmentConfigured", source, StringComparison.Ordinal);
        Assert.Contains(
            "Configure exactly one SafeWorld AppID source",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("legacyPackageConfigured", source, StringComparison.Ordinal);
        Assert.DoesNotContain("legacyEnvironmentConfigured", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalSafeWorldPackageIsParsedBySafeWorldBoundaryDirectly()
    {
        var source = ReadRepositoryFile(
            "src/SharedWorlds.Desktop/SafeWorldDesktopSteamConfiguration.cs");

        Assert.Contains("TryLoadPackage(", source, StringComparison.Ordinal);
        Assert.Contains("$\"{PackageConfigurationFileName} must contain one JSON object.\"", source, StringComparison.Ordinal);
        Assert.Contains("$\"{SteamAppIdVariable} must be a positive Steam AppID.\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PeerRuntimeConsumesSafeWorldConfigurationWithoutCompatibilityBootstrap()
    {
        var source = ReadRepositoryFile(
            "src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");

        Assert.Contains("SafeWorldDesktopSteamConfiguration.TryLoad(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardDesktopSteamConfiguration.TryLoad(", source, StringComparison.Ordinal);
        Assert.False(File.Exists(FindRepositoryFile(
            "src/SharedWorlds.Desktop/SafeWorldSteamCompatibilityBootstrap.cs")));
    }

    [Fact]
    public void PublicBetaPackagerSupportsDistributionNeutralBuildWithoutSteamConfiguration()
    {
        var source = ReadRepositoryFile("tools/build-safeworld-public-beta.ps1");
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var parameterBlock = normalized[..RequiredIndex(normalized, "Set-StrictMode")];

        Assert.Contains("param(\n    [uint32]$SteamAppId,", parameterBlock, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[Parameter(Mandatory = $true)]\n    [uint32]$SteamAppId",
            parameterBlock,
            StringComparison.Ordinal);
        Assert.Contains(
            "$steamEnabled = $PSBoundParameters.ContainsKey('SteamAppId')",
            source,
            StringComparison.Ordinal);
        Assert.Contains("$manifestSteamAppId = $null", source, StringComparison.Ordinal);
        Assert.Contains("$steamConfigurationFileName = $null", source, StringComparison.Ordinal);
        Assert.Contains("if ($steamEnabled) {", source, StringComparison.Ordinal);
        Assert.Contains("steamAppId = $manifestSteamAppId", source, StringComparison.Ordinal);
        Assert.Contains(
            "steamConfiguration = $steamConfigurationFileName",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublicBetaPackagerUsesCanonicalSteamConfigurationWhenAppIdIsSupplied()
    {
        var source = ReadRepositoryFile("tools/build-safeworld-public-beta.ps1");

        Assert.Contains("$steamConfigurationFileName = 'safeworld-steam.json'", source, StringComparison.Ordinal);
        Assert.Contains(
            "Public beta product must not contain the legacy Steam configuration filename",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SteamAppId must be positive when supplied",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "AppID 480 is development-only",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("STEWARD_STEAM_APP_ID", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerReplacesPackageOwnedSteamConfigurationOnEveryInstall()
    {
        var source = ReadRepositoryFile("installer/windows/SafeWorldInstaller.nsi");
        var legacyDelete = RequiredIndex(source, "Delete \"$INSTDIR\\steward-steam.json\"");
        var canonicalDelete = RequiredIndex(source, "Delete \"$INSTDIR\\safeworld-steam.json\"");
        var productCopy = RequiredIndex(source, "File /r \"${PRODUCT_ROOT}\\*.*\"");

        Assert.True(legacyDelete < productCopy);
        Assert.True(canonicalDelete < productCopy);
    }

    private static string ReadRepositoryFile(string relativePath)
        => File.ReadAllText(FindRepositoryFile(relativePath));

    private static int RequiredIndex(string source, string value)
    {
        var index = source.IndexOf(value, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Required source fragment was not found: {value}");
        return index;
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            return Path.Combine(workspace, relativePath);
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate repository path '{relativePath}'.");
    }
}

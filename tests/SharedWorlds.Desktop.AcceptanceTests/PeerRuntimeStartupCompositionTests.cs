using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class PeerRuntimeStartupCompositionTests
{
    [Fact]
    public void PeerRuntimeStartsAfterDurableDeviceIdentityWithoutRemoteSession()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.UnifiedStartup.cs");
        var deviceSettings = RequiredIndex(source, "await LoadDeviceSettingsAsync();");
        var peerRuntime = RequiredIndex(source, "InitializeStewardPeerRuntime();", deviceSettings);
        var gameUi = RequiredIndex(source, "await InitializeUnifiedGameUiAsync();", peerRuntime);

        Assert.True(deviceSettings < peerRuntime);
        Assert.True(peerRuntime < gameUi);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerStartupLoadsSafeWorldSteamConfigurationAndOneAppOwnedRuntime()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");

        Assert.Contains("SafeWorldDesktopSteamConfiguration.TryLoad(", source, StringComparison.Ordinal);
        Assert.Contains("app.TryGetOrCreateSteamPlatformRuntime(", source, StringComparison.Ordinal);
        Assert.Contains("configuration!.AppId", source, StringComparison.Ordinal);
        Assert.Contains("StewardDesktopPeerRuntime.Create(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardDesktopSteamConfiguration.TryLoad(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardDesktopRemoteConfiguration", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardSessionClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamWebApiTicketSource", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerStartupRequiresDurableInstallationIdentityBeforeAuthorityComposition()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");
        var durableGate = RequiredIndex(source, "if (!_deviceSettingsUsableForRemote)");
        var steamCreation = RequiredIndex(source, "app.TryGetOrCreateSteamPlatformRuntime(", durableGate);
        var peerCreation = RequiredIndex(source, "_peerRuntime = StewardDesktopPeerRuntime.Create(", steamCreation);

        Assert.True(durableGate < steamCreation);
        Assert.True(steamCreation < peerCreation);
        Assert.Contains("_deviceSettings.InstallationId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerStartupUsesTheSameResolvedCanonicalStorageRootAsDesktopStorage()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");

        Assert.Contains("CreatePeerCanonicalStorage()", source, StringComparison.Ordinal);
        Assert.Contains("=> new(_storageRoot);", source, StringComparison.Ordinal);
        Assert.Contains("LocalWorldStorage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLocalDataRoot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SharedWorlds\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnedWorldLocationObservedWorldStorage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnedWorldLocation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerRuntimeLifetimeIsOwnedByMainWindowButSteamLifetimeIsNot()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");

        Assert.Contains("Closed -= MainWindow_PeerRuntimeClosed;", source, StringComparison.Ordinal);
        Assert.Contains("Closed += MainWindow_PeerRuntimeClosed;", source, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _peerRuntime, null)?.Dispose();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("platform!.Dispose", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamAPI.Shutdown", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPeerConfigurationDoesNotPreventLocalStartup()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.PeerRuntime.cs");
        var configurationLoad = RequiredIndex(source, "if (!SafeWorldDesktopSteamConfiguration.TryLoad(");
        var firstReturn = RequiredIndex(source, "return;", configurationLoad);
        var peerCreation = RequiredIndex(source, "_peerRuntime = StewardDesktopPeerRuntime.Create(", firstReturn);

        Assert.True(configurationLoad < firstReturn);
        Assert.True(firstReturn < peerCreation);
    }

    [Fact]
    public void LocalPlayRemainsDistributionNeutralWhileSharedPlayRequiresPeerAuthority()
    {
        var routing = Read("src/SharedWorlds.Desktop/MainWindow.WorldRouting.cs");
        var localOnlyGate = RequiredIndex(
            routing,
            "if (world.SharingMode == WorldSharingMode.LocalOnly)");
        var localAuthority = RequiredIndex(routing, "return true;", localOnlyGate);
        var peerAuthority = RequiredIndex(
            routing,
            "return _peerWorldIds.Contains(world.Id) && _peerRuntime is not null;",
            localAuthority);

        Assert.True(localOnlyGate < localAuthority);
        Assert.True(localAuthority < peerAuthority);

        var readiness = Read("src/SharedWorlds.Desktop/MainWindow.EnvironmentReadiness.cs");
        var readinessStart = RequiredIndex(
            readiness,
            "private bool IsSelectedWorldEnvironmentReadyForPlay()");
        var readinessEnd = RequiredIndex(
            readiness,
            "private EnvironmentVerificationReport? GetEnvironmentVerificationFor",
            readinessStart);
        var readinessBody = readiness[readinessStart..readinessEnd];

        Assert.Contains("HasAuthoritativeRuntimeForWorld(world)", readinessBody, StringComparison.Ordinal);
        Assert.Contains(
            "world.SharingMode == WorldSharingMode.LocalOnly",
            readinessBody,
            StringComparison.Ordinal);

        var games = Read("src/SharedWorlds.Desktop/MainWindow.UnifiedGames.cs");
        var continueStart = RequiredIndex(games, "private async void UnifiedContinueButton_Click");
        var hostStart = RequiredIndex(games, "private async void UnifiedHostButton_Click", continueStart);
        var refreshStart = RequiredIndex(games, "private async Task RefreshUnifiedWorldsAsync", hostStart);
        var actionStateStart = RequiredIndex(games, "private void UpdateUnifiedActionState()");
        var actionStateEnd = RequiredIndex(
            games,
            "private void UpdateUnifiedImportActionState()",
            actionStateStart);

        Assert.Contains(
            "if (!IsSelectedWorldEnvironmentReadyForPlay())",
            games[continueStart..hostStart],
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!IsSelectedWorldEnvironmentReadyForPlay())",
            games[hostStart..refreshStart],
            StringComparison.Ordinal);
        Assert.Contains(
            "var environmentReady = IsSelectedWorldEnvironmentReadyForPlay();",
            games[actionStateStart..actionStateEnd],
            StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(FindRepositoryFile(relativePath));

    private static int RequiredIndex(string source, string value, int startIndex = 0)
    {
        var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Required source fragment was not found: {value}");
        return index;
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            var candidate = Path.Combine(workspace, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}

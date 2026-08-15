using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class PeerDefaultShellLegacyCatalogCompositionTests
{
    private static readonly string[] RetiredDesktopBackendPaths =
    [
        "src/SharedWorlds.Desktop/MainWindow.OwnedPrivateWorldBringHere.cs",
        "src/SharedWorlds.Desktop/MainWindow.OwnedPrivateWorldCatalog.cs",
        "src/SharedWorlds.Desktop/MainWindow.OwnedWorldLocationPublication.cs",
        "src/SharedWorlds.Desktop/MainWindow.RemoteRuntime.cs",
        "src/SharedWorlds.Desktop/StewardDesktopRemoteRuntime.cs",
        "src/SharedWorlds.Desktop/WorldAccessDialog.cs",
        "src/SharedWorlds.Desktop/MainWindow.WorldInvitations.cs",
        "src/SharedWorlds.Desktop/MainWindow.LobbyInvitationsPresentation.cs",
        "src/SharedWorlds.Desktop/PendingInvitationsDialog.cs"
    ];

    private static readonly string[] RetiredLiveBackendSymbols =
    [
        "_remoteRuntime",
        "_remoteWorldIds",
        "StewardRemoteHostPresence",
        "StewardDesktopRemoteRuntime"
    ];

    [Fact]
    public void LegacyBackendRuntimeAndOwnedPrivateDesktopSourcesAreAbsent()
    {
        var root = FindRepositoryRoot();

        foreach (var relativePath in RetiredDesktopBackendPaths)
        {
            Assert.False(
                File.Exists(Path.Combine(root, relativePath)),
                $"Retired Desktop backend source '{relativePath}' must not re-enter the SafeWorld product assembly.");
        }
    }

    [Fact]
    public void DesktopSourceTreeContainsNoLiveBackendAuthoritySymbols()
    {
        var root = FindRepositoryRoot();
        var desktopRoot = Path.Combine(root, "src", "SharedWorlds.Desktop");

        foreach (var file in Directory.EnumerateFiles(desktopRoot, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (var retiredSymbol in RetiredLiveBackendSymbols)
            {
                Assert.DoesNotContain(retiredSymbol, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void MainWindowOwnsNoLegacyBackendPublicationOrCatalogState()
    {
        var source = ReadRepositoryFile("src/SharedWorlds.Desktop/MainWindow.xaml.cs");

        Assert.DoesNotContain("SharedWorlds.Infrastructure.Remote", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ownedWorldLocation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeOwnedWorldLocationMigrationState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DisposeRemoteRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetOwnedPrivateWorldCatalogBusyState", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalPeerStartupHasNoLegacyBackendCompositionEntryPoint()
    {
        var source = ReadStartup();

        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalogUi", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalogRefreshHooks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldBringHereAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerRuntimeAndCoreWorldUiComposeWithoutLegacyCatalogDecision()
    {
        var source = ReadStartup();
        var peerRuntime = RequiredIndex(source, "InitializeStewardPeerRuntime();");
        var gameUi = RequiredIndex(source, "await InitializeUnifiedGameUiAsync();", peerRuntime);
        var sharing = RequiredIndex(source, "InitializeWorldSharingUi();", gameUi);
        var join = RequiredIndex(source, "await InitializeWorldJoinUiAsync();", sharing);

        Assert.True(peerRuntime < gameUi);
        Assert.True(gameUi < sharing);
        Assert.True(sharing < join);
    }

    [Fact]
    public void SteamInviteJoinPathDoesNotDependOnLegacyCatalogSurface()
    {
        var source = ReadStartup();
        var join = RequiredIndex(source, "await InitializeWorldJoinUiAsync();");
        var coldJoin = RequiredIndex(source, "InitializeSteamLobbyLaunchRequest();", join);

        Assert.True(join < coldJoin);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldBringHereAction", source, StringComparison.Ordinal);
    }

    private static string ReadStartup()
        => ReadRepositoryFile("src/SharedWorlds.Desktop/MainWindow.UnifiedStartup.cs");

    private static string ReadRepositoryFile(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    private static int RequiredIndex(string source, string value, int startIndex = 0)
    {
        var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Required source fragment was not found: {value}");
        return index;
    }

    private static string FindRepositoryRoot()
    {
        var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(workspace) &&
            File.Exists(Path.Combine(workspace, "SharedWorlds.sln")))
        {
            return workspace;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedWorlds.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the SafeWorld repository root.");
    }
}

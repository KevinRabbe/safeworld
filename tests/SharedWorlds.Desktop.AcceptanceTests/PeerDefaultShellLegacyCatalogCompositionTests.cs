using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class PeerDefaultShellLegacyCatalogCompositionTests
{
    [Fact]
    public void NormalPeerStartupDoesNotInitializeLegacyOwnedPrivateCatalogOrBringHere()
    {
        var source = ReadStartup();

        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalogUi", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalogRefreshHooks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldBringHereAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PeerDefaultConstructorDoesNotBuildLegacyOwnedPrivateCatalogPresentation()
    {
        var source = ReadRepositoryFile("src/SharedWorlds.Desktop/MainWindow.xaml.cs");
        var constructor = RequiredIndex(source, "public MainWindow()");
        var constructorEnd = RequiredIndex(source, "private void InitializeLiveRegionAnnouncements()", constructor);
        var body = source[constructor..constructorEnd];

        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalogUi", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldBringHereAction", body, StringComparison.Ordinal);
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

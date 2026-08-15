using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class WorldLobbyPresentationTests
{
    [Fact]
    public void LobbyListsOnlyCanonicalPersistentPeerWorldsWithoutBackendPresence()
    {
        var lobby = File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/MainWindow.ProductShell.cs"));
        var oldWorldLobby = File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/MainWindow.WorldLobby.cs"));

        Assert.Contains("DesktopText.Lobby", lobby, StringComparison.Ordinal);
        Assert.Contains("_allWorldItems", lobby, StringComparison.Ordinal);
        Assert.Contains("_peerWorldIds.Contains(item.World.Id)", lobby, StringComparison.Ordinal);
        Assert.Contains("_peerRuntime is not null", lobby, StringComparison.Ordinal);
        Assert.Contains("OpenGameWorkspace(item.AdapterId, item.GameName, item.World.Id)", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedWorlds.Infrastructure.Remote", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteWorldIds", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerPresence", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime.Access", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardRemoteWorld", lobby, StringComparison.Ordinal);

        Assert.DoesNotContain("WorldDetailsPanel.Children.Insert", oldWorldLobby, StringComparison.Ordinal);
        Assert.Contains("Lobby is a top-level cross-game destination", oldWorldLobby, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticJoinPublishesOnlyAfterClientLaunchAndClearsAfterObservedEnd()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/CoordinatedJoinGameAdapter.cs"));
        var launch = source.IndexOf("await _inner.LaunchClientAsync", StringComparison.Ordinal);
        var publish = source.IndexOf("await StartPresenceAsync()", StringComparison.Ordinal);
        var observedEnd = source.IndexOf("await _inner.WaitForSessionEndAsync", StringComparison.Ordinal);
        var clear = source.IndexOf("await StopPresenceAsync()", observedEnd, StringComparison.Ordinal);

        Assert.True(launch >= 0);
        Assert.True(publish > launch);
        Assert.True(observedEnd > publish);
        Assert.True(clear > observedEnd);
        Assert.Contains("TimeSpan.FromSeconds(15)", source, StringComparison.Ordinal);
        Assert.Contains("TTL expiration is the fallback", source, StringComparison.Ordinal);
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

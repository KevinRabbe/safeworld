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
    public void LiveJoinReadinessComesFromAttachedAuthoritativeSteamLobbyOnly()
    {
        var join = File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/MainWindow.WorldJoin.cs"));
        var participants = File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/SteamPeerWorldLobby.Participants.cs"));

        Assert.Contains("snapshot = await peerRuntime.Lobby.GetAsync(world.Id);", join, StringComparison.Ordinal);
        Assert.Contains("!snapshot.OwnerConfirmed", join, StringComparison.Ordinal);
        Assert.Contains("snapshot.AuthorityGeneration == 0", join, StringComparison.Ordinal);
        Assert.Contains("snapshot.RequestedHost is not null", join, StringComparison.Ordinal);
        Assert.DoesNotContain("StewardWorldPlayerPresenceClient", join, StringComparison.Ordinal);
        Assert.DoesNotContain("GetHostPresenceAsync", join, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedWorlds.Infrastructure.Remote", join, StringComparison.Ordinal);

        Assert.Contains("ListCurrentMembersAsync(", participants, StringComparison.Ordinal);
        Assert.Contains("RequireKnownLobby(worldId)", participants, StringComparison.Ordinal);
        Assert.Contains("EnsureWritableOwner(", participants, StringComparison.Ordinal);
        Assert.Contains("SteamMatchmaking.GetNumLobbyMembers(lobbyId)", participants, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLobbyData", participants, StringComparison.Ordinal);
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

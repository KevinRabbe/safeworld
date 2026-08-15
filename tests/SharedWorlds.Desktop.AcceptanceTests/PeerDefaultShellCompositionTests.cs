using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class PeerDefaultShellCompositionTests
{
    [Fact]
    public void PeerJoinInviteHandlingIsInitializedDirectlyFromPeerStartup()
    {
        var source = ReadStartup();
        var peerRuntime = RequiredIndex(source, "InitializeStewardPeerRuntime();");
        var joinUi = RequiredIndex(source, "await InitializeWorldJoinUiAsync();", peerRuntime);
        var coldJoin = RequiredIndex(source, "InitializeSteamLobbyLaunchRequest();", joinUi);

        Assert.True(peerRuntime < joinUi);
        Assert.True(joinUi < coldJoin);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalPeerShellDoesNotInitializeLegacyBackendInvitationInbox()
    {
        var source = ReadStartup();

        Assert.DoesNotContain("InitializeWorldInvitationsUiAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RehomeInvitationsToGlobalLobby", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfessionalShellAndVisualDesignDoNotDependOnLegacyRuntimeDecision()
    {
        var source = ReadStartup();
        var shell = RequiredIndex(source, "InitializeProfessionalProductShell();");
        var visual = RequiredIndex(source, "InitializeVisualDesignV2();", shell);

        Assert.True(shell < visual);
        Assert.DoesNotContain("_remoteRuntime", source[shell..visual], StringComparison.Ordinal);
    }

    private static string ReadStartup()
        => File.ReadAllText(FindRepositoryFile(
            "src/SharedWorlds.Desktop/MainWindow.UnifiedStartup.cs"));

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

using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class PeerInitialShareCutoverCompositionTests
{
    [Fact]
    public void RuntimeExposesFreshGenerationOneShareService()
    {
        var source = Read("src/SharedWorlds.Desktop/StewardDesktopPeerRuntime.cs");
        var service = RequiredIndex(source, "var initialShare = new PeerWorldInitialShareService(");
        var serviceEnd = RequiredIndex(source, "authorityFences);", service);

        Assert.Contains("var membership = new PeerWorldMembershipService(", source, StringComparison.Ordinal);
        Assert.Contains("public PeerWorldInitialShareService InitialShare { get; }", source, StringComparison.Ordinal);
        Assert.Contains("storage,\n                authorityFences);", source[service..(serviceEnd + "authorityFences);".Length)], StringComparison.Ordinal);
    }

    [Fact]
    public void FreshLocalOnlyShareUsesPeerAuthorityWithoutBackendPublisher()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.WorldSharing.cs");
        var localOnly = RequiredIndex(source, "if (world.SharingMode == WorldSharingMode.LocalOnly)");
        var peerRuntime = RequiredIndex(source, "var peerShareRuntime = _peerRuntime;", localOnly);
        var initialShare = RequiredIndex(source, "peerShareRuntime.InitialShare.ShareAsync(", peerRuntime);

        Assert.True(localOnly < peerRuntime);
        Assert.True(peerRuntime < initialShare);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitialWorldPublisher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new WorldAccessDialog(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshPeerShareKeepsExactEnvironmentPreflightBeforeAuthorityMutation()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.WorldSharing.cs");
        var localOnly = RequiredIndex(source, "if (world.SharingMode == WorldSharingMode.LocalOnly)");
        var selection = RequiredIndex(source, "SelectInstallationForWorldAsync(local, adapter)", localOnly);
        var ready = RequiredIndex(source, "if (!selection.Verification.IsReady)", selection);
        var share = RequiredIndex(source, "peerShareRuntime.InitialShare.ShareAsync(", ready);

        Assert.True(localOnly < selection);
        Assert.True(selection < ready);
        Assert.True(ready < share);
    }

    [Fact]
    public void FreshPeerShareContainsNoBackendPublicationDependency()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.WorldSharing.cs");

        Assert.Contains("peerShareRuntime.InitialShare.ShareAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitialWorldPublisher", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenRevisionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SharingMode = WorldSharingMode.Shared", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PrePeerSharedWorldIsRefusedInsteadOfPublishedBackToBackend()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.WorldSharing.cs");
        var gate = RequiredIndex(source, "if (world.SharingMode == WorldSharingMode.Shared)");
        var message = RequiredIndex(
            source,
            "predates SafeWorld peer authority. Migrate or re-import it before sharing or writable play.",
            gate);

        Assert.True(gate < message);
        Assert.DoesNotContain("InitialWorldPublisher", source[gate..], StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", source[gate..], StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSharePresentationDependsOnPeerRuntimeNotBackend()
    {
        var source = Read("src/SharedWorlds.Desktop/MainWindow.WorldSharing.cs");
        var update = RequiredIndex(source, "private void UpdateWorldSharingActionState()");
        var localOnly = RequiredIndex(source, "if (world.SharingMode == WorldSharingMode.LocalOnly)", update);
        var prePeer = RequiredIndex(source, "if (world.SharingMode == WorldSharingMode.Shared)", localOnly);
        var block = source[localOnly..prePeer];

        Assert.Contains("var available = _peerRuntime is not null;", block, StringComparison.Ordinal);
        Assert.Contains("No backend upload is required", block, StringComparison.Ordinal);
        Assert.DoesNotContain("_remoteRuntime", block, StringComparison.Ordinal);
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

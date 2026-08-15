using System.Xml.Linq;

namespace SharedWorlds.Architecture.Tests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Core_DoesNotReferenceConcreteProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coreProject = Path.Combine(repositoryRoot, "src", "SharedWorlds.Core", "SharedWorlds.Core.csproj");
        Assert.Empty(ReadProjectReferences(coreProject));
    }

    [Fact]
    public void Infrastructure_ReferencesOnlyCore()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coreProject = Path.Combine(repositoryRoot, "src", "SharedWorlds.Core", "SharedWorlds.Core.csproj");
        var infrastructureProject = Path.Combine(repositoryRoot, "src", "SharedWorlds.Infrastructure", "SharedWorlds.Infrastructure.csproj");
        AssertReferencesOnly(infrastructureProject, coreProject);
    }

    [Fact]
    public void EachGameAdapter_ReferencesOnlyCore()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coreProject = Path.Combine(repositoryRoot, "src", "SharedWorlds.Core", "SharedWorlds.Core.csproj");
        var adaptersRoot = Path.Combine(repositoryRoot, "src", "SharedWorlds.GameAdapters");
        var adapterProjects = Directory.EnumerateFiles(adaptersRoot, "*.csproj", SearchOption.AllDirectories).ToArray();
        Assert.NotEmpty(adapterProjects);
        foreach (var adapterProject in adapterProjects)
        {
            AssertReferencesOnly(adapterProject, coreProject);
        }
    }

    [Fact]
    public void PeerProductStartup_DoesNotEnterLegacyBackendRuntime()
    {
        var repositoryRoot = FindRepositoryRoot();
        var startup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.UnifiedStartup.cs"));
        Assert.Contains("InitializeStewardPeerRuntime();", startup);
        Assert.DoesNotContain("InitializeStewardRemoteSessionAsync(", startup);
        Assert.DoesNotContain("_remoteRuntime", startup);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldCatalog", startup);
        Assert.DoesNotContain("InitializeOwnedPrivateWorldBringHere", startup);
        Assert.DoesNotContain("InitializeWorldInvitationsUiAsync", startup);
        Assert.DoesNotContain("RehomeInvitationsToGlobalLobby", startup);
    }

    [Fact]
    public void PeerProductConstructor_UsesLocalStorageWithoutLegacyPublicationObserver()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.xaml.cs"));
        var constructor = source.IndexOf("public MainWindow()", StringComparison.Ordinal);
        var constructorEnd = source.IndexOf("private void InitializeLiveRegionAnnouncements()", constructor, StringComparison.Ordinal);
        Assert.True(constructor >= 0);
        Assert.True(constructorEnd > constructor);
        var body = source[constructor..constructorEnd];
        Assert.Contains("_storage = localStorage;", body);
        Assert.DoesNotContain("OwnedWorldLocationObservedWorldStorage", body);
        Assert.DoesNotContain("RequestOwnedWorldLocationPublication", body);
    }

    [Fact]
    public void PeerProductWorldRouting_UsesOnlyLocalAndPeerAuthority()
    {
        var repositoryRoot = FindRepositoryRoot();
        var routing = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.WorldRouting.cs"));
        Assert.Contains("await _storage.ListWorldsAsync(cancellationToken)", routing);
        Assert.Contains("localWorld.PeerAuthority is not null", routing);
        Assert.Contains("RequirePeerRuntime(world)", routing);
        Assert.DoesNotContain("_remoteRuntime", routing);
        Assert.DoesNotContain("_remoteWorldIds", routing);
        Assert.DoesNotContain("StewardDesktopRemoteRuntime", routing);
        Assert.DoesNotContain("SharedWorlds.Infrastructure.Remote", routing);
        Assert.DoesNotContain("HttpClient", routing);
    }

    [Fact]
    public void PeerProductSharingUi_HasNoLegacyBackendPublicationOrAccessPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharing = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.WorldSharing.cs"));
        Assert.Contains("PeerWorldAccessDialog", sharing);
        Assert.Contains("InitialShare.ShareAsync", sharing);
        Assert.DoesNotContain("_remoteRuntime", sharing);
        Assert.DoesNotContain("_remoteWorldIds", sharing);
        Assert.DoesNotContain("new WorldAccessDialog(", sharing);
        Assert.DoesNotContain("InitialWorldPublisher", sharing);
    }

    [Fact]
    public void PeerProductGameUi_HostsThroughLocalOrPeerLifecycleOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var games = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.UnifiedGames.cs"));
        Assert.Contains("GetManagedHostAdapterForWorld(world, adapter)", games);
        Assert.Contains("managedHostAdapter,", games);
        Assert.DoesNotContain("_remoteRuntime", games);
        Assert.DoesNotContain("_remoteWorldIds", games);
        Assert.DoesNotContain("_lastRemoteWorldLoadError", games);
        Assert.DoesNotContain("Shared Worlds are temporarily unavailable", games);
    }

    [Fact]
    public void PeerProductDeletion_DoesNotDeletePersistentPeerAuthorityOrCallLegacyRuntime()
    {
        var repositoryRoot = FindRepositoryRoot();
        var deletion = File.ReadAllText(Path.Combine(repositoryRoot, "src", "SharedWorlds.Desktop", "MainWindow.WorldDeletion.cs"));
        Assert.Contains("world?.PeerAuthority is not null", deletion);
        Assert.Contains("must be left through Manage access", deletion);
        Assert.DoesNotContain("_remoteRuntime", deletion);
        Assert.DoesNotContain("_remoteWorldIds", deletion);
        Assert.DoesNotContain("_remoteIncompleteWorldIds", deletion);
    }

    [Fact]
    public void SafeWorldRepository_DoesNotContainLegacyBackendClientOrCompatibilityTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        var infrastructureRoot = Path.Combine(repositoryRoot, "src", "SharedWorlds.Infrastructure");
        var infrastructureTestsRoot = Path.Combine(repositoryRoot, "tests", "SharedWorlds.Infrastructure.Tests");

        Assert.False(Directory.Exists(Path.Combine(infrastructureRoot, "Remote")));
        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "src", "SharedWorlds.LegacyRemote")));
        Assert.False(Directory.Exists(Path.Combine(infrastructureTestsRoot, "Remote")));
        Assert.False(Directory.Exists(Path.Combine(infrastructureTestsRoot, "BackendSimulation")));
        Assert.Empty(Directory.EnumerateFiles(infrastructureTestsRoot, "Steward*.cs", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.EnumerateFiles(infrastructureTestsRoot, "OwnedWorldLocation*.cs", SearchOption.TopDirectoryOnly));
        Assert.False(File.Exists(Path.Combine(infrastructureTestsRoot, "LocalOwnedWorldLocationPublicationJournalTests.cs")));
    }

    [Fact]
    public void SafeWorldRepository_DoesNotContainLegacyBackendProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var backendSourceDirectories = new[]
        {
            "SharedWorlds.Backend",
            "SharedWorlds.Backend.Api",
            "SharedWorlds.Backend.PostgreSql",
            "SharedWorlds.Backend.ObjectStorage.S3",
        };
        foreach (var directory in backendSourceDirectories)
        {
            Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "src", directory)));
        }

        var backendTestDirectories = new[]
        {
            "SharedWorlds.Backend.Tests",
            "SharedWorlds.Backend.Api.Tests",
            "SharedWorlds.Backend.PostgreSql.Tests",
            "SharedWorlds.Backend.ObjectStorage.S3.Tests",
            "SharedWorlds.BringHere.EndToEndTests",
        };
        foreach (var directory in backendTestDirectories)
        {
            Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "tests", directory)));
        }

        Assert.False(File.Exists(Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "legacy-backend-ci.yml")));
    }

    [Fact]
    public void SafeWorldRepository_DoesNotContainRetiredCliOrBackendPackaging()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "src", "SharedWorlds.Cli")));
        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "beta", "server")));

        var packages = File.ReadAllText(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        Assert.DoesNotContain("AWSSDK.S3", packages, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.TestHost", packages, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", packages, StringComparison.Ordinal);
    }

    private static void AssertReferencesOnly(string projectPath, params string[] allowedProjects)
    {
        var actual = ReadProjectReferences(projectPath);
        var expected = allowedProjects.Select(Path.GetFullPath).OrderBy(path => path, PathComparer()).ToArray();
        Assert.Equal(expected, actual);
    }

    private static string[] ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Cannot resolve project directory for '{projectPath}'.");
        return document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .OrderBy(path => path, PathComparer())
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SharedWorlds.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    private static StringComparer PathComparer()
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

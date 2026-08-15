using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class LegacyRemoteDistributionBoundaryTests
{
    [Fact]
    public void ProductInfrastructureExcludesLegacyRemoteSources()
    {
        var project = ReadRepositoryFile("src/SharedWorlds.Infrastructure/SharedWorlds.Infrastructure.csproj");

        Assert.Contains("<Compile Remove=\"Remote/**/*.cs\" />", project, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopDoesNotReferenceLegacyRemoteAssembly()
    {
        var project = ReadRepositoryFile("src/SharedWorlds.Desktop/SharedWorlds.Desktop.csproj");

        Assert.DoesNotContain("SharedWorlds.LegacyRemote", project, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDesktopPublishRejectsLegacyRemoteAssembly()
    {
        var targets = ReadRepositoryFile("src/SharedWorlds.Desktop/Directory.Build.targets");

        Assert.Contains("AfterTargets=\"Publish\"", targets, StringComparison.Ordinal);
        Assert.Contains("Exists('$(PublishDir)SharedWorlds.LegacyRemote.dll')", targets, StringComparison.Ordinal);
        Assert.Contains("SafeWorld product publish must not contain SharedWorlds.LegacyRemote.dll", targets, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryWindowsPackageCiAlsoChecksPublishedBytes()
    {
        var workflow = ReadRepositoryFile(".github/workflows/ci.yml");

        Assert.Contains("-Filter 'SharedWorlds.LegacyRemote.dll'", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "Ordinary peer product package unexpectedly contains SharedWorlds.LegacyRemote.dll.",
            workflow,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

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

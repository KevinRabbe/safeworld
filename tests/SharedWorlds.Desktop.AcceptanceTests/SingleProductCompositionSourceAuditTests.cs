using Xunit;

namespace SharedWorlds.Desktop.AcceptanceTests;

public sealed class SingleProductCompositionSourceAuditTests
{
    [Fact]
    public void LegacyCliDoesNotReintroduceParallelLifecycleOrStorageComposition()
    {
        var repositoryRoot = FindRepositoryRoot();
        var cliDirectory = Path.Combine(repositoryRoot, "src", "SharedWorlds.Cli");
        if (!Directory.Exists(cliDirectory))
        {
            return;
        }

        var sourceFiles = Directory
            .EnumerateFiles(cliDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            sourceFiles.Length == 0,
            "SafeWorld has one supported product lifecycle/storage composition surface. " +
            "The retired SharedWorlds.Cli must not regain source-backed lifecycle or durable-storage authority. " +
            "Found: " + string.Join(", ", sourceFiles));

        var projectPath = Path.Combine(cliDirectory, "SharedWorlds.Cli.csproj");
        if (!File.Exists(projectPath))
        {
            return;
        }

        var project = File.ReadAllText(projectPath);
        Assert.DoesNotContain("<OutputType>Exe</OutputType>", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SharedWorlds.Cli.Retired", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductInfrastructureExcludesLegacyRemoteSourcesAndDesktopCannotReferenceLegacyAssembly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var infrastructureProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedWorlds.Infrastructure",
            "SharedWorlds.Infrastructure.csproj"));
        var legacyProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "SharedWorlds.LegacyRemote",
            "SharedWorlds.LegacyRemote.csproj");
        var desktopProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedWorlds.Desktop",
            "SharedWorlds.Desktop.csproj"));

        Assert.Contains("<Compile Remove=\"Remote/**/*.cs\" />", infrastructureProject, StringComparison.Ordinal);
        Assert.True(File.Exists(legacyProjectPath), "Historical Remote sources must have an explicit non-product assembly owner.");

        var legacyProject = File.ReadAllText(legacyProjectPath);
        Assert.Contains("SharedWorlds.LegacyRemote", legacyProject, StringComparison.Ordinal);
        Assert.Contains("../SharedWorlds.Infrastructure/Remote/**/*.cs", legacyProject, StringComparison.Ordinal);
        Assert.Contains("../SharedWorlds.Infrastructure/SharedWorlds.Infrastructure.csproj", legacyProject, StringComparison.Ordinal);

        Assert.DoesNotContain("SharedWorlds.LegacyRemote", desktopProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SharedWorlds.Infrastructure/Remote", desktopProject, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src")) &&
                Directory.Exists(Path.Combine(current.FullName, "tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root for the single-product source audit.");
    }
}

namespace SharedWorlds.Architecture.Tests;

public sealed class RepositorySolutionBoundaryTests
{
    [Fact]
    public void RootSolutionContainsOnlyExistingSafeWorldProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var solutionPath = Path.Combine(repositoryRoot, "SharedWorlds.sln");
        var solution = File.ReadAllText(solutionPath);

        Assert.DoesNotContain("SharedWorlds.Backend", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedWorlds.Cli", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedWorlds.LegacyRemote", solution, StringComparison.Ordinal);

        var projectLines = File.ReadLines(solutionPath)
            .Where(line => line.StartsWith("Project(\"", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(projectLines);

        foreach (var line in projectLines)
        {
            var parts = line.Split("\", \"", StringSplitOptions.None);
            Assert.True(parts.Length >= 3, $"Unexpected solution project line: {line}");
            var relativeProjectPath = parts[1].Replace('\\', Path.DirectorySeparatorChar);
            Assert.True(
                File.Exists(Path.Combine(repositoryRoot, relativeProjectPath)),
                $"Root solution references missing project '{relativeProjectPath}'.");
        }
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

        throw new InvalidOperationException("Could not locate SafeWorld repository root.");
    }
}

namespace SharedWorlds.Architecture.Tests;

public sealed class RepositoryBackendResidueBoundaryTests
{
    [Fact]
    public void SafeWorldRepositoryDoesNotCarryLegacyBackendDocumentationOrDockerResidue()
    {
        var repositoryRoot = FindRepositoryRoot();
        var docsRoot = Path.Combine(repositoryRoot, "docs");

        Assert.Empty(Directory.EnumerateFiles(docsRoot, "BE*.md", SearchOption.TopDirectoryOnly));
        Assert.False(File.Exists(Path.Combine(docsRoot, "BACKEND_ROADMAP.md")));
        Assert.False(File.Exists(Path.Combine(docsRoot, "CLOSED_ALPHA_LIVE_DEPLOYMENT_REQUEST.md")));
        Assert.False(File.Exists(Path.Combine(docsRoot, "E4_LIVE_ACCEPTANCE_DEPLOYMENT.md")));
        Assert.False(File.Exists(Path.Combine(docsRoot, "bring-here-postgresql-authority.md")));
        Assert.False(File.Exists(Path.Combine(docsRoot, "bring-here-postgresql-test-plan.md")));
        Assert.False(File.Exists(Path.Combine(repositoryRoot, ".dockerignore")));
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

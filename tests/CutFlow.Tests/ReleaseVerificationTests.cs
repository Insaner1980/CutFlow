using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ReleaseVerificationTests
{
    [TestMethod]
    public void ReleaseCommand_CleansGeneratedOutputAndRunsExplicitX64Verification()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "Verify-CutFlowRelease.ps1"));

        Assert.Contains(".sonarqube", script);
        Assert.Contains("src\\CutFlow\\bin", script);
        Assert.Contains("src\\CutFlow\\obj", script);
        Assert.Contains("tests\\CutFlow.Tests\\bin", script);
        Assert.Contains("tests\\CutFlow.Tests\\obj", script);
        Assert.Contains("Remove-Item -LiteralPath $generatedPath -Recurse -Force", script);
        Assert.Contains("Refusing to remove a generated directory outside the CutFlow repository", script);
        Assert.Contains("\"restore\", $solutionPath, \"-p:Platform=x64\"", script);
        Assert.Contains("\"test\", $solutionPath, \"-c\", \"Release\", \"-p:Platform=x64\", \"--no-restore\"", script);
        Assert.Contains("\"build\", $solutionPath, \"-c\", \"Release\", \"-p:Platform=x64\", \"--no-restore\", \"--no-incremental\"", script);
        Assert.Contains("Register-CutFlowDevelopment.ps1", script);
        Assert.Contains("-Configuration Release", script);
        Assert.IsFalse(script.Contains("Certificate", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(script.Contains("GetEnvironmentVariable", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReleaseDocumentation_RequiresFreshCheckoutLaunchAcceptance()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        var acceptance = File.ReadAllText(Path.Combine(repositoryRoot, "MANUAL_ACCEPTANCE.md"));

        Assert.Contains(".\\scripts\\Verify-CutFlowRelease.ps1", readme);
        Assert.Contains("-RegisterAndLaunch", readme);
        Assert.Contains("fresh checkout", acceptance);
        Assert.Contains("opens to Home", acceptance);
        Assert.Contains("development certificate", acceptance);
        Assert.Contains("environment variable", acceptance);
        Assert.Contains("manually generated fixture", acceptance);
    }

    [TestMethod]
    public void Documentation_DescribesCurrentPersistenceAndHiddenTrackContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(repositoryRoot, "PROJECT.md"));
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));

        Assert.Contains("project.json.<guid>.tmp", project);
        Assert.Contains("Publish the new `ModifiedAt` to the live document only after the file commit succeeds", project);
        Assert.Contains("rejected as invalid rather than silently dropping items", project);
        Assert.Contains("Hidden V1 is rendered as project-background filler", project);
        Assert.Contains("Hidden V1 is exported as project-background filler", readme);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CutFlow repository root.");
    }
}

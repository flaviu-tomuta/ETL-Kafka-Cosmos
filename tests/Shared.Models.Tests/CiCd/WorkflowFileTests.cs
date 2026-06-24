namespace Shared.Models.Tests.CiCd;

public sealed class WorkflowFileTests
{
    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no CLAUDE.md found)");
    }

    private static string WorkflowPath(string fileName)
        => Path.Combine(FindRepoRoot(), ".github", "workflows", fileName);

    // ─── ci.yml ──────────────────────────────────────────────────────────────

    [Fact]
    public void CiWorkflow_FileExists()
    {
        Assert.True(File.Exists(WorkflowPath("ci.yml")),
            ".github/workflows/ci.yml must exist");
    }

    [Fact]
    public void CiWorkflow_TriggersOnPushToMain()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("push:", content);
        Assert.Contains("- main", content);
        Assert.Contains("workflow_dispatch", content);
    }

    [Fact]
    public void CiWorkflow_HasDotnetVersionAndBuildConfigurationEnvVars()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("DOTNET_VERSION:", content);
        Assert.Contains("BUILD_CONFIGURATION:", content);
        Assert.Contains("Release", content);
    }

    [Fact]
    public void CiWorkflow_HasRestoreBuildAndTestSteps()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("dotnet restore", content);
        Assert.Contains("--no-restore", content);
        Assert.Contains("--no-build", content);
    }

    [Fact]
    public void CiWorkflow_TestStep_CollectsXPlatCodeCoverage()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("XPlat Code Coverage", content);
        Assert.Contains("--results-directory", content);
        Assert.Contains("./coverage", content);
    }

    [Fact]
    public void CiWorkflow_UploadsCoverageReportArtifact()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("coverage-report", content);
        Assert.Contains("./coverage", content);
    }

    [Fact]
    public void CiWorkflow_UploadsVersionedBuildArtifactWithSevenDayRetention()
    {
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("function-app-${{ github.sha }}", content);
        Assert.Contains("retention-days: 7", content);
    }

    [Fact]
    public void CiWorkflow_HasCommentedOutDeployJobWithFunctionsAction()
    {
        // AC5: deploy job stub exists as a comment so it can be uncommented without workflow redesign
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        Assert.Contains("Azure/functions-action@v1", content);
        Assert.Contains("AZURE_FUNCTIONAPP_PUBLISH_PROFILE", content);
        Assert.Contains("onboarding-func-dev", content);
    }

    [Fact]
    public void CiWorkflow_DeployJob_IsCommentedOut()
    {
        // The deploy job must be a comment block, not active YAML
        string content = File.ReadAllText(WorkflowPath("ci.yml"));
        // Every line referencing the deploy action must start with '#'
        string[] lines = content.Split('\n');
        bool foundDeployAction = false;
        foreach (string line in lines)
        {
            if (!line.Contains("Azure/functions-action@v1")) continue;
            foundDeployAction = true;
            Assert.True(line.TrimStart().StartsWith('#'),
                $"Deploy job line must be commented out: {line}");
        }
        Assert.True(foundDeployAction, "Deploy job stub (Azure/functions-action@v1) must be present in ci.yml");
    }

    // ─── pr-checks.yml ───────────────────────────────────────────────────────

    [Fact]
    public void PrChecksWorkflow_FileExists()
    {
        Assert.True(File.Exists(WorkflowPath("pr-checks.yml")),
            ".github/workflows/pr-checks.yml must exist");
    }

    [Fact]
    public void PrChecksWorkflow_TriggersOnPullRequestToMain()
    {
        string content = File.ReadAllText(WorkflowPath("pr-checks.yml"));
        Assert.Contains("pull_request:", content);
        Assert.Contains("- main", content);
    }

    [Fact]
    public void PrChecksWorkflow_HasRestoreBuildAndTestSteps()
    {
        // AC1: dotnet restore, dotnet build --no-restore, and dotnet test --no-build all pass as PR gates
        string content = File.ReadAllText(WorkflowPath("pr-checks.yml"));
        Assert.Contains("dotnet restore", content);
        Assert.Contains("--no-restore", content);
        Assert.Contains("--no-build", content);
    }

    [Fact]
    public void PrChecksWorkflow_HasJobNamedBuildAndTest()
    {
        // Branch protection status check name is "PR Checks / Build and Test"
        string content = File.ReadAllText(WorkflowPath("pr-checks.yml"));
        Assert.Contains("Build and Test", content);
    }

    [Fact]
    public void PrChecksWorkflow_DoesNotContainPublishOrArtifactUpload()
    {
        // pr-checks.yml has no Publish and no artifact upload steps (fast PR gate only)
        string content = File.ReadAllText(WorkflowPath("pr-checks.yml"));
        Assert.DoesNotContain("dotnet publish", content);
        Assert.DoesNotContain("retention-days", content);
    }
}

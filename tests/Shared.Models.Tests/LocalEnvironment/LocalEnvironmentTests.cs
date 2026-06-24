using System.Text.Json;
using Xunit;

namespace Shared.Models.Tests.LocalEnvironment;

public sealed class LocalEnvironmentTests
{
    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (no CLAUDE.md found)");
    }

    private static string LocalEnvPath(string fileName)
        => Path.Combine(FindRepoRoot(), "local-env", fileName);

    private static string FunctionLocalSettings(string functionProject)
        => Path.Combine(FindRepoRoot(), "src", functionProject, "local.settings.json");

    // ─── docker-compose.yml ───────────────────────────────────────────────────

    [Fact]
    public void DockerComposeFile_Exists()
    {
        Assert.True(File.Exists(LocalEnvPath("docker-compose.yml")),
            "local-env/docker-compose.yml must exist");
    }

    [Fact]
    public void DockerComposeFile_ContainsKafkaService_OnPort9092()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("confluentinc/cp-kafka:7.6.0", content);
        Assert.Contains("9092:9092", content);
    }

    [Fact]
    public void DockerComposeFile_ContainsZookeeperService()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("confluentinc/cp-zookeeper:7.6.0", content);
    }

    [Fact]
    public void DockerComposeFile_ContainsCosmosEmulatorService_OnPort8081()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview", content);
        Assert.Contains("8081:8081", content);
    }

    [Fact]
    public void DockerComposeFile_CosmosEmulator_HasPlatformLinuxAmd64AndMemLimit2g()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("linux/amd64", content);
        Assert.Contains("mem_limit: 2g", content);
    }

    [Fact]
    public void DockerComposeFile_ContainsSqlServerService_OnPort1433()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("mcr.microsoft.com/mssql/server:2022-latest", content);
        Assert.Contains("1433:1433", content);
    }

    [Fact]
    public void DockerComposeFile_ContainsServiceBusEmulatorService_OnPorts5672And5300()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("mcr.microsoft.com/azure-messaging/servicebus-emulator", content);
        Assert.Contains("5672:5672", content);
        Assert.Contains("5300:5300", content);
    }

    [Fact]
    public void DockerComposeFile_ServiceBus_MountsConfigJson()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("Config.json", content);
        Assert.Contains("/ServiceBus_Emulator/ConfigFiles/Config.json", content);
    }

    [Fact]
    public void DockerComposeFile_ContainsAzuriteService_OnPorts10000To10002()
    {
        string content = File.ReadAllText(LocalEnvPath("docker-compose.yml"));
        Assert.Contains("mcr.microsoft.com/azure-storage/azurite", content);
        Assert.Contains("10000:10000", content);
        Assert.Contains("10001:10001", content);
        Assert.Contains("10002:10002", content);
    }

    // ─── Config.json ─────────────────────────────────────────────────────────

    [Fact]
    public void ConfigJson_Exists()
    {
        Assert.True(File.Exists(LocalEnvPath("Config.json")),
            "local-env/Config.json must exist");
    }

    [Fact]
    public void ConfigJson_IsValidJson()
    {
        string content = File.ReadAllText(LocalEnvPath("Config.json"));
        JsonDocument doc = JsonDocument.Parse(content);
        Assert.NotNull(doc);
    }

    [Fact]
    public void ConfigJson_ContainsAllFourQueues()
    {
        string content = File.ReadAllText(LocalEnvPath("Config.json"));
        Assert.Contains("onboarding-retry", content);
        Assert.Contains("onboarding-deadletter", content);
        Assert.Contains("amendment-retry", content);
        Assert.Contains("amendment-deadletter", content);
    }

    [Fact]
    public void ConfigJson_RetryQueues_HaveDeadLetterOnMessageExpiration()
    {
        string content = File.ReadAllText(LocalEnvPath("Config.json"));
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement queues = doc.RootElement
            .GetProperty("UserConfig")
            .GetProperty("Namespaces")[0]
            .GetProperty("Queues");

        int retryQueuesWithDeadLetter = 0;
        foreach (JsonElement queue in queues.EnumerateArray())
        {
            string name = queue.GetProperty("Name").GetString()!;
            if (!name.EndsWith("-retry")) continue;
            bool deadLetter = queue
                .GetProperty("Properties")
                .GetProperty("DeadLetterOnMessageExpiration")
                .GetBoolean();
            if (deadLetter) retryQueuesWithDeadLetter++;
        }

        Assert.Equal(2, retryQueuesWithDeadLetter);
    }

    // ─── local.settings.json ─────────────────────────────────────────────────

    [Fact]
    public void OnboardingLocalSettings_ConnectsToKafkaAtLocalhost9092()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Onboarding.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("KafkaBootstrapServers").GetString();
        Assert.Equal("localhost:9092", value);
    }

    [Fact]
    public void OnboardingLocalSettings_ConnectsToCosmosAtLocalhost8081()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Onboarding.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("CosmosDbConnection").GetString();
        Assert.NotNull(value);
        Assert.Contains("https://localhost:8081", value);
    }

    [Fact]
    public void OnboardingLocalSettings_AzureWebJobsStorage_IsUseDevelopmentStorage()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Onboarding.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("AzureWebJobsStorage").GetString();
        Assert.Equal("UseDevelopmentStorage=true", value);
    }

    [Fact]
    public void OnboardingLocalSettings_ApplicationInsightsConnectionString_IsEmpty()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Onboarding.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("APPLICATIONINSIGHTS_CONNECTION_STRING").GetString();
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void AmendmentLocalSettings_ConnectsToKafkaAtLocalhost9092()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Amendment.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("KafkaBootstrapServers").GetString();
        Assert.Equal("localhost:9092", value);
    }

    [Fact]
    public void AmendmentLocalSettings_ConnectsToCosmosAtLocalhost8081()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Amendment.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("CosmosDbConnection").GetString();
        Assert.NotNull(value);
        Assert.Contains("https://localhost:8081", value);
    }

    [Fact]
    public void AmendmentLocalSettings_AzureWebJobsStorage_IsUseDevelopmentStorage()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Amendment.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("AzureWebJobsStorage").GetString();
        Assert.Equal("UseDevelopmentStorage=true", value);
    }

    [Fact]
    public void AmendmentLocalSettings_ApplicationInsightsConnectionString_IsEmpty()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Amendment.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("APPLICATIONINSIGHTS_CONNECTION_STRING").GetString();
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void AmendmentLocalSettings_ServiceBusConnection_DoesNotContainUnresolvedPlaceholder()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FunctionLocalSettings("Amendment.Function")));
        string? value = doc.RootElement.GetProperty("Values").GetProperty("ServiceBusConnection").GetString();
        Assert.NotNull(value);
        Assert.DoesNotContain("<emulator-key>", value);
    }
}

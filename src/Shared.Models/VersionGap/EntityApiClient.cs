using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Models.Contracts;
using Shared.Models.Models;

namespace Shared.Models.VersionGap;

public sealed class EntityApiClient : IEntityApiClient
{
    private readonly HttpClient _httpClient;

    public EntityApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri(configuration["ApiBaseUrl"] ?? "http://localhost");
    }

    public async Task<EntityData> GetEntityAtVersionAsync(string entityId, int version)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/entities/{entityId}/versions/{version}");
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EntityData>(json) ?? new EntityData();
    }
}

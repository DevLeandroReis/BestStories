using System.Net.Http.Json;
using System.Text.Json;

namespace BestStories.IntegrationTests;

internal sealed record StoryDto(string Title, string? Uri, string? PostedBy, DateTimeOffset Time, int Score, int CommentCount);

internal static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<StoryDto>> GetFromJsonArrayAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<StoryDto>>(Options) ?? [];
    }
}

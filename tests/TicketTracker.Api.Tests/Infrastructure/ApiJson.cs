using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicketTracker.Api.Tests.Infrastructure;

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        var value = JsonSerializer.Deserialize<T>(body, Options);
        Assert.NotNull(value);
        return value;
    }

    public static async Task<ProblemBody> ReadProblemAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
        var value = JsonSerializer.Deserialize<ProblemBody>(body, Options);
        Assert.NotNull(value);
        return value;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T payload) =>
        client.PostAsJsonAsync(url, payload, Options);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T payload) =>
        client.PutAsJsonAsync(url, payload, Options);

    public static string NewProjectKey()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return string.Create(8, alphabet, (chars, letters) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = letters[Random.Shared.Next(letters.Length)];
            }
        });
    }
}

public sealed class ProblemBody
{
    public string? Type { get; set; }

    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }

    public string? TraceId { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}

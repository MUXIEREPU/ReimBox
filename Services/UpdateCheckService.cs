using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ReimbursementAssistant.Services;

public sealed class UpdateCheckService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/MUXIEREPU/ReimBox/releases/latest";
    private readonly HttpClient _client;

    public UpdateCheckService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ReimBox", CurrentVersion.ToString()));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Version CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
        var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() ?? "" : "";
        var notes = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? "" : "";
        var latestVersion = ParseVersion(tag);
        return new UpdateCheckResult(latestVersion is not null && latestVersion > CurrentVersion, CurrentVersion, latestVersion, tag, url, notes);
    }

    private static Version? ParseVersion(string tag)
    {
        var normalized = tag.Trim().TrimStart('v', 'V');
        var separator = normalized.IndexOfAny(['-', '+']);
        if (separator >= 0) normalized = normalized[..separator];
        return Version.TryParse(normalized, out var version) ? version : null;
    }
}

public sealed record UpdateCheckResult(bool HasUpdate, Version CurrentVersion, Version? LatestVersion, string Tag, string ReleaseUrl, string ReleaseNotes);

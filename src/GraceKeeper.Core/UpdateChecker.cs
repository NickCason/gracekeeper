using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GraceKeeper.Core;

public sealed record UpdateInfo(Version Version, string ReleaseUrl);

public sealed class UpdateChecker
{
    private readonly HttpClient _client;
    private readonly string _apiUrl;

    public UpdateChecker(HttpClient client, string apiUrl)
    {
        _client = client;
        _apiUrl = apiUrl;
    }

    public async Task<UpdateInfo?> CheckAsync(Version currentVersion)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _apiUrl);
            req.Headers.UserAgent.ParseAdd("GraceKeeper-UpdateChecker");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var resp = await _client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var tag = doc.RootElement.GetProperty("tag_name").GetString();
            var url = doc.RootElement.GetProperty("html_url").GetString();
            if (tag is null || url is null) return null;

            var v = tag.TrimStart('v');
            if (!Version.TryParse(v, out var latest)) return null;
            return latest > currentVersion ? new UpdateInfo(latest, url) : null;
        }
        catch
        {
            return null;  // silent failure (air-gapped or network down)
        }
    }
}

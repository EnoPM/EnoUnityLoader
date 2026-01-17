using System.Text.Json.Serialization;

namespace EnoUnityLoader.Updater.GitHub;

internal sealed class GitHubRelease
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];

    public Version? GetVersion()
    {
        var versionString = TagName.TrimStart('v', 'V');
        return Version.TryParse(versionString, out var version) ? version : null;
    }
}

using System.Text.Json;

namespace CtrNfSave.Updates;

public sealed record ReleaseAsset(string Name, string Url);
public sealed record UpdateRelease(UpdateVersion Version, string Body, string PageUrl, IReadOnlyList<ReleaseAsset> Assets)
{
    public static UpdateRelease? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        if (!UpdateVersion.TryParse(root.GetProperty("tag_name").GetString(), out var version) || version.Suffix is not null) return null;
        string body = root.TryGetProperty("body", out var value) ? value.GetString() ?? "" : "";
        var assets = root.GetProperty("assets").EnumerateArray().Select(a => new ReleaseAsset(a.GetProperty("name").GetString() ?? "", a.GetProperty("browser_download_url").GetString() ?? "")).ToArray();
        return new(version, body[..Math.Min(body.Length, 16000)], $"https://github.com/vzeol/ctrnf-save-editor/releases/tag/v{version}", assets);
    }
    public (ReleaseAsset Zip, ReleaseAsset Checksum)? SelectAssets()
    {
        string name = $"ctrnf-save-editor-{Version}-win-x64.zip";
        var zip = Assets.Where(a => a.Name == name).ToArray();
        var checksum = Assets.Where(a => a.Name == name + ".sha256").ToArray();
        return zip.Length == 1 && checksum.Length == 1 && AllowedDownload(zip[0].Url) && AllowedDownload(checksum[0].Url) ? (zip[0], checksum[0]) : null;
    }
    public static bool AllowedDownload(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}

public sealed record UpdateSettings
{
    [System.Text.Json.Serialization.JsonPropertyName("checkUpdatesAtStartup")]
    public bool CheckUpdatesAtStartup { get; init; } = true;
    [System.Text.Json.Serialization.JsonPropertyName("skippedVersion")]
    public string? SkippedVersion { get; init; }
    public static UpdateSettings Parse(string json)
    {
        try { return JsonSerializer.Deserialize<UpdateSettings>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
    public static UpdateSettings Read(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(); }
    }
    public string ToJson() => JsonSerializer.Serialize(this);
}

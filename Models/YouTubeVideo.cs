using System.Text.Json.Serialization;

namespace ZVTube.Models;

public class YouTubeVideo
{
    public string id { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string uploader { get; set; } = string.Empty;
    public long view_count { get; set; }
    public string upload_date { get; set; } = string.Empty;

    [JsonIgnore]
    public string Title => title;

    [JsonIgnore]
    public string Uploader => uploader;

    [JsonIgnore]
    public string Views => view_count.ToString("N0");

    [JsonIgnore]
    public string UploadDate =>
        string.IsNullOrWhiteSpace(upload_date) || upload_date.Length != 8
            ? string.Empty
            : $"{upload_date[..4]}.{upload_date.Substring(4, 2)}.{upload_date.Substring(6, 2)}";

    [JsonIgnore]
    public string Url => $"https://www.youtube.com/watch?v={id}";
}

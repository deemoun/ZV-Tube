namespace YouTubeDownloader.Models
{
    public class YouTubeVideo
    {
        public string id { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string uploader { get; set; } = string.Empty;
        public long view_count { get; set; }
        public string upload_date { get; set; } = string.Empty;

        public string Title => title;
        public string Uploader => uploader;
        public string Views => view_count.ToString("N0");

        public string UploadDate =>
            string.IsNullOrWhiteSpace(upload_date) || upload_date.Length != 8
                ? ""
                : $"{upload_date[..4]}.{upload_date.Substring(4, 2)}.{upload_date.Substring(6, 2)}";
    }
}

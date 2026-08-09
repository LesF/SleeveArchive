namespace SleeveArchive.Models;

public class Album
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int? VinylCondition { get; set; }
    public int? CoverCondition { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? MusicBrainzId { get; set; }
}

public class MusicBrainzReleaseItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Date { get; set; }
    public string? Country { get; set; }
    public string? Disambiguation { get; set; }
    public bool HasCoverArt { get; set; }
    public string? CoverArtUrl => !string.IsNullOrEmpty(Id) ? $"https://coverartarchive.org/release/{Id}/front-250.jpg" : null;
    public string? LargeCoverArtUrl => !string.IsNullOrEmpty(Id) ? $"https://coverartarchive.org/release/{Id}/front-500.jpg" : null;
}

public class QueryResult
{
    public List<Album> Albums { get; set; } = new();
    public int TotalCount { get; set; }
}


using System.Net.Http.Headers;
using System.Text.Json;
using SleeveArchive.Models;

namespace SleeveArchive.Services;

public class MusicBrainzService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static MusicBrainzService()
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SleeveArchive/1.0.0 (contact@sleevearchive.com)");
        }
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public MusicBrainzService()
    {
    }

    public async Task<List<MusicBrainzReleaseItem>> SearchReleasesAsync(string query, int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<MusicBrainzReleaseItem>();

        try
        {
            var encodedQuery = Uri.EscapeDataString(query.Trim());
            var url = $"https://musicbrainz.org/ws/2/release/?query={encodedQuery}&fmt=json&limit={limit}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return new List<MusicBrainzReleaseItem>();
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            var results = new List<MusicBrainzReleaseItem>();

            if (doc.RootElement.TryGetProperty("releases", out var releasesArray) && releasesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var release in releasesArray.EnumerateArray())
                {
                    var item = new MusicBrainzReleaseItem();

                    if (release.TryGetProperty("id", out var idProp))
                    {
                        item.Id = idProp.GetString() ?? string.Empty;
                    }

                    if (release.TryGetProperty("title", out var titleProp))
                    {
                        item.Title = titleProp.GetString() ?? string.Empty;
                    }

                    // Extract artist from artist-credit array
                    if (release.TryGetProperty("artist-credit", out var artistCredit) && artistCredit.ValueKind == JsonValueKind.Array)
                    {
                        var artistNames = new List<string>();
                        foreach (var credit in artistCredit.EnumerateArray())
                        {
                            if (credit.TryGetProperty("name", out var nameProp) && !string.IsNullOrEmpty(nameProp.GetString()))
                            {
                                artistNames.Add(nameProp.GetString()!);
                            }
                            else if (credit.TryGetProperty("artist", out var artistObj) &&
                                     artistObj.TryGetProperty("name", out var artNameProp) &&
                                     !string.IsNullOrEmpty(artNameProp.GetString()))
                            {
                                artistNames.Add(artNameProp.GetString()!);
                            }
                        }
                        item.Artist = string.Join(", ", artistNames);
                    }

                    if (release.TryGetProperty("date", out var dateProp))
                    {
                        item.Date = dateProp.GetString();
                    }

                    if (release.TryGetProperty("country", out var countryProp))
                    {
                        item.Country = countryProp.GetString();
                    }

                    if (release.TryGetProperty("disambiguation", out var disambigProp))
                    {
                        item.Disambiguation = disambigProp.GetString();
                    }

                    // Check cover art archive flag
                    if (release.TryGetProperty("cover-art-archive", out var coverArtProp))
                    {
                        if (coverArtProp.TryGetProperty("front", out var frontProp) && frontProp.ValueKind == JsonValueKind.True)
                        {
                            item.HasCoverArt = true;
                        }
                        else if (coverArtProp.TryGetProperty("count", out var countProp) && countProp.GetInt32() > 0)
                        {
                            item.HasCoverArt = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(item.Id) && !string.IsNullOrEmpty(item.Title))
                    {
                        results.Add(item);
                    }
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching MusicBrainz: {ex.Message}");
            return new List<MusicBrainzReleaseItem>();
        }
    }

    public static string GetCoverArtUrl(string? mbid, int size = 500)
    {
        if (string.IsNullOrWhiteSpace(mbid)) return string.Empty;
        return size <= 250
            ? $"https://coverartarchive.org/release/{mbid.Trim()}/front-250.jpg"
            : $"https://coverartarchive.org/release/{mbid.Trim()}/front-500.jpg";
    }
}

using Microsoft.Data.Sqlite;
using SleeveArchive.Models;

namespace SleeveArchive.Services;

public class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "music_catalog.db");
    }

    public async Task InitializeAsync()
    {
        if (!File.Exists(_dbPath))
        {
            try
            {
                using Stream assetStream = await FileSystem.OpenAppPackageFileAsync("music_catalog.db");
                using FileStream fileStream = new FileStream(_dbPath, FileMode.Create, FileAccess.Write);
                await assetStream.CopyToAsync(fileStream);
            }
            catch
            {
                // If package file not found or on clean initialization, create the schema directly
                await EnsureTableCreatedAsync();
            }
        }

        // Schema migration check: ensure musicbrainz_id column exists
        await EnsureMusicBrainzColumnExistsAsync();
    }

    private async Task EnsureTableCreatedAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var sql = @"
            CREATE TABLE IF NOT EXISTS albums (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                artist TEXT NOT NULL,
                vinyl_condition INTEGER,
                cover_condition INTEGER,
                status TEXT NOT NULL,
                musicbrainz_id TEXT
            );";
        using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task EnsureMusicBrainzColumnExistsAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        bool columnExists = false;
        using (var cmd = new SqliteCommand("PRAGMA table_info(albums);", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1); // column name is at index 1
                if (string.Equals(columnName, "musicbrainz_id", StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            using var alterCmd = new SqliteCommand("ALTER TABLE albums ADD COLUMN musicbrainz_id TEXT;", connection);
            await alterCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<QueryResult> QueryAlbumsAsync(string search, string status, int page, int pageSize)
    {
        var result = new QueryResult();
        var albums = new List<Album>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Construct where clauses
        var conditions = new List<string>();
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(title LIKE @search OR artist LIKE @search)");
            parameters.Add(new SqliteParameter("@search", $"%{search.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            conditions.Add("status = @status");
            parameters.Add(new SqliteParameter("@status", status));
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        // 1. Get total count
        string countQuery = $"SELECT COUNT(*) FROM albums {whereClause}";
        using (var countCmd = new SqliteCommand(countQuery, connection))
        {
            countCmd.Parameters.AddRange(parameters.Select(p => CloneParameter(p)).ToArray());
            var countResult = await countCmd.ExecuteScalarAsync();
            result.TotalCount = Convert.ToInt32(countResult);
        }

        // 2. Get paginated results
        string selectQuery = $"SELECT id, title, artist, vinyl_condition, cover_condition, status, musicbrainz_id FROM albums {whereClause} ORDER BY id DESC LIMIT @limit OFFSET @offset";
        using (var selectCmd = new SqliteCommand(selectQuery, connection))
        {
            selectCmd.Parameters.AddRange(parameters.Select(p => CloneParameter(p)).ToArray());
            selectCmd.Parameters.Add(new SqliteParameter("@limit", pageSize));
            selectCmd.Parameters.Add(new SqliteParameter("@offset", (page - 1) * pageSize));

            using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var album = new Album
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Artist = reader.GetString(2),
                    VinylCondition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    CoverCondition = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Status = reader.GetString(5),
                    MusicBrainzId = reader.IsDBNull(6) ? null : reader.GetString(6)
                };
                albums.Add(album);
            }
        }

        result.Albums = albums;
        return result;
    }

    public async Task<Album?> GetAlbumByIdAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        string selectQuery = "SELECT id, title, artist, vinyl_condition, cover_condition, status, musicbrainz_id FROM albums WHERE id = @id LIMIT 1";
        using var cmd = new SqliteCommand(selectQuery, connection);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Album
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Artist = reader.GetString(2),
                VinylCondition = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                CoverCondition = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Status = reader.GetString(5),
                MusicBrainzId = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }

        return null;
    }

    public async Task<int> AddAlbumAsync(Album album)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        string insertQuery = @"
            INSERT INTO albums (title, artist, vinyl_condition, cover_condition, status, musicbrainz_id)
            VALUES (@title, @artist, @vinyl_condition, @cover_condition, @status, @musicbrainz_id);
            SELECT last_insert_rowid();";

        using var cmd = new SqliteCommand(insertQuery, connection);
        cmd.Parameters.AddWithValue("@title", album.Title.Trim());
        cmd.Parameters.AddWithValue("@artist", album.Artist.Trim());
        cmd.Parameters.AddWithValue("@vinyl_condition", (object?)album.VinylCondition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cover_condition", (object?)album.CoverCondition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", album.Status ?? "Owned");
        cmd.Parameters.AddWithValue("@musicbrainz_id", (object?)album.MusicBrainzId ?? DBNull.Value);

        var scalar = await cmd.ExecuteScalarAsync();
        var newId = Convert.ToInt32(scalar);
        album.Id = newId;
        return newId;
    }

    public async Task<bool> UpdateAlbumAsync(Album album)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        string updateQuery = @"
            UPDATE albums
            SET title = @title,
                artist = @artist,
                vinyl_condition = @vinyl_condition,
                cover_condition = @cover_condition,
                status = @status,
                musicbrainz_id = @musicbrainz_id
            WHERE id = @id;";

        using var cmd = new SqliteCommand(updateQuery, connection);
        cmd.Parameters.AddWithValue("@id", album.Id);
        cmd.Parameters.AddWithValue("@title", album.Title.Trim());
        cmd.Parameters.AddWithValue("@artist", album.Artist.Trim());
        cmd.Parameters.AddWithValue("@vinyl_condition", (object?)album.VinylCondition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cover_condition", (object?)album.CoverCondition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", album.Status ?? "Owned");
        cmd.Parameters.AddWithValue("@musicbrainz_id", (object?)album.MusicBrainzId ?? DBNull.Value);

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAlbumAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        string deleteQuery = "DELETE FROM albums WHERE id = @id;";
        using var cmd = new SqliteCommand(deleteQuery, connection);
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    private SqliteParameter CloneParameter(SqliteParameter original)
    {
        return new SqliteParameter(original.ParameterName, original.Value);
    }
}

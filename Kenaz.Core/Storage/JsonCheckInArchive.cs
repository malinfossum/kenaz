using System.Text.Json;

namespace Kenaz.Core;

/// <summary>
/// Reads and writes a portable, versioned JSON export of all check-ins. Separate from
/// <see cref="ICheckInRepository"/> (the live store) so that seam stays list-in / list-out.
/// Writes are atomic (temp file + move); imports are validated record-by-record.
/// </summary>
public class JsonCheckInArchive
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    public static string DefaultExportPath(DateTimeOffset exportedAt)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fileName = $"kenaz-backup-{exportedAt:yyyyMMdd-HHmmss}.json";
        return Path.Combine(documents, "Kenaz", fileName);
    }

    public void Export(string path, IReadOnlyList<CheckIn> checkIns, DateTimeOffset exportedAt)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new ExportDocumentDto
        {
            SchemaVersion = CurrentSchemaVersion,
            ExportedAt = exportedAt,
            CheckIns = checkIns.Select(ToDto).ToList(),
        };

        var json = JsonSerializer.Serialize(document, Options);

        var tempPath = path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public ImportResult Import(string path)
    {
        if (!File.Exists(path))
        {
            throw new ImportException("I couldn't find a file at that path.");
        }

        ExportDocumentDto? document;
        try
        {
            var json = File.ReadAllText(path);
            document = JsonSerializer.Deserialize<ExportDocumentDto>(json, Options);
        }
        catch (JsonException)
        {
            throw new ImportException("That file isn't a readable Kenaz export.");
        }
        catch (IOException)
        {
            throw new ImportException("I couldn't read that file — check the path and try again.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new ImportException("I don't have permission to read that file.");
        }

        if (document is null)
        {
            throw new ImportException("That file isn't a readable Kenaz export.");
        }

        if (document.SchemaVersion > CurrentSchemaVersion)
        {
            throw new ImportException("That export was made by a newer version of Kenaz.");
        }

        var records = new List<CheckIn>();
        var seenDates = new HashSet<DateOnly>();
        var skipped = 0;

        foreach (var dto in document.CheckIns ?? new List<CheckInDto>())
        {
            if (seenDates.Contains(dto.Date))
            {
                continue;
            }

            try
            {
                var checkIn = new CheckIn(dto.Date, dto.Mood, dto.Energy, dto.Sleep, dto.Note, dto.CreatedAt, dto.UpdatedAt);
                records.Add(checkIn);
                seenDates.Add(dto.Date);
            }
            catch (ArgumentException)
            {
                skipped++;
            }
        }

        return new ImportResult(records, skipped);
    }

    private static CheckInDto ToDto(CheckIn checkIn)
    {
        return new CheckInDto
        {
            Date = checkIn.Date,
            Mood = checkIn.Mood,
            Energy = checkIn.Energy,
            Sleep = checkIn.Sleep,
            Note = checkIn.Note,
            CreatedAt = checkIn.CreatedAt,
            UpdatedAt = checkIn.UpdatedAt,
        };
    }
}

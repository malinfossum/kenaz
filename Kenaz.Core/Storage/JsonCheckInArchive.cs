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

using System.Globalization;
using System.Text.Json;

namespace Kenaz.Core;

/// <summary>
/// Reads and writes check-ins as a JSON file. Up through M3 this was the live store;
/// from M4 onward the live store is <see cref="SqliteCheckInRepository"/> and this
/// class is used only as the corrupt-safe reader of the legacy file during a one-shot
/// JSON → SQLite migration (see <see cref="JsonToSqliteMigrator"/>). Writes are atomic
/// (temp file + move) so a crash never leaves a half-written file, and loads are
/// recoverable: corrupt files are set aside and every record is re-validated and
/// de-duped by date.
/// </summary>
public class JsonCheckInRepository : ICheckInRepository
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    private readonly string _filePath;

    public JsonCheckInRepository(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Kenaz", "checkins.json");
    }

    public IReadOnlyList<CheckIn> LoadAll()
    {
        if (!File.Exists(_filePath))
        {
            return new List<CheckIn>();
        }

        List<CheckInDto>? dtos;
        try
        {
            var json = File.ReadAllText(_filePath);
            dtos = JsonSerializer.Deserialize<List<CheckInDto>>(json, Options);
        }
        catch (JsonException)
        {
            BackUpCorruptFile();
            return new List<CheckIn>();
        }

        if (dtos is null)
        {
            return new List<CheckIn>();
        }

        var checkIns = new List<CheckIn>();
        var seenDates = new HashSet<DateOnly>();
        foreach (var dto in dtos)
        {
            if (seenDates.Contains(dto.Date))
            {
                continue;
            }

            try
            {
                var checkIn = new CheckIn(dto.Date, dto.Mood, dto.Energy, dto.Sleep, dto.Note, dto.CreatedAt, dto.UpdatedAt);
                checkIns.Add(checkIn);
                seenDates.Add(dto.Date);
            }
            catch (ArgumentException)
            {
                // Drop a record the file tried to smuggle past CheckIn's invariants.
            }
        }

        return checkIns;
    }

    public void SaveAll(IReadOnlyList<CheckIn> checkIns)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dtos = checkIns.Select(ToDto).ToList();
        var json = JsonSerializer.Serialize(dtos, Options);

        var tempPath = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
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

    private void BackUpCorruptFile()
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = _filePath + $".corrupt-{timestamp}.bak";
        File.Move(_filePath, backupPath, overwrite: true);
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

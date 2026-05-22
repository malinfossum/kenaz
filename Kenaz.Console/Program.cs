using System.Globalization;
using Kenaz.Core;
using static System.Console;

namespace Kenaz.Console;

internal static class Program
{
    private static void Main()
    {
        var repository = new JsonCheckInRepository(JsonCheckInRepository.DefaultFilePath());
        var journal = new WellbeingJournal(repository, () => DateTimeOffset.Now);

        WriteLine("Kenaz — your daily check-in. Bring it into the light.");

        var running = true;
        while (running)
        {
            ShowMenu();

            var choice = ReadLine();
            if (choice is null)
            {
                break;
            }

            switch (choice.Trim())
            {
                case "1":
                    CheckInToday(journal);
                    break;
                case "2":
                    ShowTodayVsWeek(journal);
                    break;
                case "3":
                    ShowHistory(journal);
                    break;
                case "4":
                    ExportCheckIns(journal);
                    break;
                case "0":
                    running = false;
                    break;
                default:
                    WriteLine("I didn't catch that — please choose 1, 2, 3, 4, or 0.");
                    break;
            }
        }

        WriteLine();
        WriteLine("Take care. See you next time.");
    }

    private static void ShowMenu()
    {
        WriteLine();
        WriteLine("What would you like to do?");
        WriteLine("  1) Check in for today");
        WriteLine("  2) Today vs your last 7 days");
        WriteLine("  3) See your history");
        WriteLine("  4) Export your check-ins");
        WriteLine("  0) Exit");
        Write("> ");
    }

    private static void CheckInToday(WellbeingJournal journal)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        WriteLine();
        if (journal.GetByDate(today) is not null)
        {
            WriteLine("You've already checked in today — let's update it.");
        }

        var mood = ReadOptionalScale("Mood");
        var energy = ReadOptionalScale("Energy");
        var sleep = ReadOptionalSleep();
        var note = ReadOptionalNote();

        try
        {
            journal.AddOrUpdate(today, mood, energy, sleep, note);
            WriteLine();
            WriteLine("Saved. Thank you for showing up for yourself today.");
        }
        catch (ArgumentException)
        {
            WriteLine();
            WriteLine("Nothing saved yet — add at least one of mood, energy, sleep, or a note.");
        }
    }

    private static void ShowTodayVsWeek(WellbeingJournal journal)
    {
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var todayCheckIn = journal.GetByDate(today);

        WriteLine();
        if (journal.Last7Days(now).Count == 0)
        {
            WriteLine("Not enough check-ins yet — check in a few days and your patterns will show up here.");
            return;
        }

        WriteLine(todayCheckIn is null
            ? "You haven't checked in today yet. Here's your last 7 days:"
            : "Today vs your last 7 days:");

        WriteLine($"  Mood     today {Scale(todayCheckIn?.Mood)}    7-day avg {Average(journal.Average(c => c.Mood, 7, now))}");
        WriteLine($"  Energy   today {Scale(todayCheckIn?.Energy)}    7-day avg {Average(journal.Average(c => c.Energy, 7, now))}");
        WriteLine($"  Sleep    today {Hours(todayCheckIn?.Sleep)}    7-day avg {AverageHours(journal.Average(c => c.Sleep, 7, now))}");
        WriteLine();
        WriteLine(StreakMessage(journal.StreakDays(now)));
    }

    private static void ShowHistory(WellbeingJournal journal)
    {
        var history = journal.History();

        WriteLine();
        if (history.Count == 0)
        {
            WriteLine("No check-ins yet — your history will appear here once you start.");
            return;
        }

        WriteLine("Your check-ins, newest first:");
        foreach (var checkIn in history)
        {
            WriteLine($"  {checkIn.Date:yyyy-MM-dd}   mood {Scale(checkIn.Mood)}   energy {Scale(checkIn.Energy)}   sleep {Hours(checkIn.Sleep)}");
            if (!string.IsNullOrWhiteSpace(checkIn.Note))
            {
                WriteLine($"               note: {checkIn.Note}");
            }
        }
    }

    private static void ExportCheckIns(WellbeingJournal journal)
    {
        var checkIns = journal.History();

        WriteLine();
        if (checkIns.Count == 0)
        {
            WriteLine("There's nothing to export yet — check in first, then your data is yours to take.");
            return;
        }

        var now = DateTimeOffset.Now;
        var path = JsonCheckInArchive.DefaultExportPath(now);

        WriteLine("Heads up: the export file is unencrypted — keep it somewhere private.");

        try
        {
            new JsonCheckInArchive().Export(path, checkIns, now);
            WriteLine($"Saved {checkIns.Count} check-in(s) to:");
            WriteLine($"  {path}");
        }
        catch (IOException)
        {
            WriteLine("I couldn't write the file just now — check the folder and try again.");
        }
    }

    private static int? ReadOptionalScale(string label)
    {
        while (true)
        {
            Write($"{label} (1-10, Enter to skip): ");
            var input = ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value >= 1 && value <= 10)
            {
                return value;
            }

            WriteLine("  Please enter a whole number from 1 to 10, or press Enter to skip.");
        }
    }

    private static decimal? ReadOptionalSleep()
    {
        while (true)
        {
            Write("Hours of sleep (0-24, e.g. 7 or 7,5, Enter to skip): ");
            var input = ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            var normalized = input.Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && value >= 0 && value <= 24)
            {
                return value;
            }

            WriteLine("  Please enter hours from 0 to 24 (e.g. 7 or 7,5), or press Enter to skip.");
        }
    }

    private static string? ReadOptionalNote()
    {
        Write("Anything you want to note (Enter to skip): ");
        var input = ReadLine();
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    private static string Scale(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : "—";
    }

    private static string Hours(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.#", CultureInfo.CurrentCulture) + " h" : "—";
    }

    private static string Average(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.0", CultureInfo.CurrentCulture) : "not enough yet";
    }

    private static string AverageHours(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.0", CultureInfo.CurrentCulture) + " h" : "not enough yet";
    }

    private static string StreakMessage(int streak)
    {
        if (streak == 0)
        {
            return "No streak yet — every check-in is a fresh start.";
        }

        if (streak == 1)
        {
            return "You're on a 1-day streak. A gentle beginning.";
        }

        return $"You're on a {streak}-day streak. Lovely consistency.";
    }
}

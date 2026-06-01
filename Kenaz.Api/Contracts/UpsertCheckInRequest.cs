namespace Kenaz.Api;

/// <summary>The PUT body. All fields optional; CheckIn's constructor (via the journal) is the validator.</summary>
public record UpsertCheckInRequest(int? Mood, int? Energy, decimal? Sleep, string? Note);

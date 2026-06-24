/** Returns an error message string, or null when valid. Mirrors C# CheckIn.Validate. */
export function validateCheckIn({ mood, energy, sleep, note }) {
	if (mood != null && (mood < 1 || mood > 10)) return "Mood must be between 1 and 10 when provided."
	if (energy != null && (energy < 1 || energy > 10)) return "Energy must be between 1 and 10 when provided."
	if (sleep != null && (sleep < 0 || sleep > 24)) return "Sleep must be between 0 and 24 hours when provided."
	if (mood == null && energy == null && sleep == null && !(note && note.trim())) {
		return "A check-in needs at least one of: mood, energy, sleep, or a note."
	}
	return null
}

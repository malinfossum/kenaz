/** Reconcile incoming check-ins into existing by date. Strictly-newer UpdatedAt wins; ties keep existing.
 *  Compares timestamps as instants (parsed to epoch ms), not as strings. */
export function merge(existing, incoming) {
	const byDate = new Map(existing.map((c) => [c.date, c]))
	let added = 0
	let updated = 0
	let unchanged = 0
	for (const candidate of incoming) {
		const current = byDate.get(candidate.date)
		if (!current) {
			byDate.set(candidate.date, candidate)
			added++
		} else if (Date.parse(candidate.updatedAt) > Date.parse(current.updatedAt)) {
			byDate.set(candidate.date, candidate)
			updated++
		} else {
			unchanged++
		}
	}
	return { records: [...byDate.values()], added, updated, unchanged }
}

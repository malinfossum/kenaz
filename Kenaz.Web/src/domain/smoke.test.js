import { expect, test } from "vitest"
import { WEEK_DAYS } from "./constants.js"

test("vitest runs and constants load", () => {
	expect(WEEK_DAYS).toBe(7)
})

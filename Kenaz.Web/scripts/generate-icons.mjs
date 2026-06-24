/* Generate Kenaz PWA icons with zero dependencies (built-in zlib only).
   A soft accent-colored disc (the torch's light) on a near-black field.
   Run: node scripts/generate-icons.mjs   (from Kenaz.Web/)
   Placeholder-grade but valid + installable; swap in a designed icon anytime. */

import { mkdirSync, writeFileSync } from "node:fs"
import { deflateSync } from "node:zlib"

const crcTable = (() => {
	const t = new Uint32Array(256)
	for (let n = 0; n < 256; n++) {
		let c = n
		for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
		t[n] = c >>> 0
	}
	return t
})()

function crc32(buf) {
	let c = 0xffffffff
	for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8)
	return (c ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
	const len = Buffer.alloc(4)
	len.writeUInt32BE(data.length, 0)
	const body = Buffer.concat([Buffer.from(type, "latin1"), data])
	const crc = Buffer.alloc(4)
	crc.writeUInt32BE(crc32(body), 0)
	return Buffer.concat([len, body, crc])
}

function encodePng(size, rgba) {
	const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])
	const ihdr = Buffer.alloc(13)
	ihdr.writeUInt32BE(size, 0)
	ihdr.writeUInt32BE(size, 4)
	ihdr[8] = 8 // bit depth
	ihdr[9] = 6 // color type: RGBA
	// rows prefixed with filter byte 0 (none)
	const stride = size * 4
	const raw = Buffer.alloc(size * (stride + 1))
	for (let y = 0; y < size; y++) {
		raw[y * (stride + 1)] = 0
		rgba.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride)
	}
	const idat = deflateSync(raw, { level: 9 })
	return Buffer.concat([
		sig,
		chunk("IHDR", ihdr),
		chunk("IDAT", idat),
		chunk("IEND", Buffer.alloc(0)),
	])
}

function icon(size, radiusFrac) {
	const bg = [10, 13, 16, 255] // #0a0d10 near-black
	const fg = [124, 154, 179, 255] // #7c9ab3 accent
	const rgba = Buffer.alloc(size * size * 4)
	const cx = (size - 1) / 2
	const cy = (size - 1) / 2
	const r = (size / 2) * radiusFrac
	for (let y = 0; y < size; y++) {
		for (let x = 0; x < size; x++) {
			const i = (y * size + x) * 4
			const inside = Math.hypot(x - cx, y - cy) <= r
			const c = inside ? fg : bg
			rgba[i] = c[0]
			rgba[i + 1] = c[1]
			rgba[i + 2] = c[2]
			rgba[i + 3] = c[3]
		}
	}
	return encodePng(size, rgba)
}

mkdirSync("public/icons", { recursive: true })
// "any" icons fill most of the canvas; the maskable disc stays inside the ~40% safe radius.
writeFileSync("public/icons/icon-192.png", icon(192, 0.72))
writeFileSync("public/icons/icon-512.png", icon(512, 0.72))
writeFileSync("public/icons/icon-maskable-512.png", icon(512, 0.56))
console.log("icons written to public/icons/")

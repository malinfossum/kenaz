/* ======================================================================
   src/utils/dom.js — SAFE DOM CONSTRUCTION
   el() is the View's building block. String children become text nodes,
   so user-supplied text (notes) is escaped by construction — never innerHTML.
   ====================================================================== */

/**
 * Create an element.
 * @param {string} tag
 * @param {object} props  class, dataset:{}, aria-*, role/for attributes, or DOM properties
 * @param {...(Node|string|null)} children  strings become escaped text nodes
 */
export function el(tag, props = {}, ...children) {
	const node = document.createElement(tag)

	for (const [key, value] of Object.entries(props)) {
		if (value == null) continue
		if (typeof value === "boolean") {
			// Boolean attributes (disabled, required, …) must be passed as real booleans, not strings —
			// the string "false" is truthy and would wrongly set the attribute.
			node.toggleAttribute(key, value)
			continue
		}
		if (key === "class") node.className = value
		else if (key === "dataset") Object.assign(node.dataset, value)
		else if (key === "for" || key === "role" || key.startsWith("aria-") || key.startsWith("data-"))
			node.setAttribute(key, value)
		else if (key in node) node[key] = value
		else node.setAttribute(key, value)
	}

	for (const child of children) {
		if (child == null) continue
		node.append(child instanceof Node ? child : document.createTextNode(String(child)))
	}

	return node
}

/** Remove all children of a node. */
export function clear(node) {
	node.replaceChildren()
}

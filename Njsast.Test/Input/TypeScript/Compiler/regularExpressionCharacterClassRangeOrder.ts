// @target: esnext

// The characters in the following regular expressions are ASCII-lookalike characters found in Unicode, including:
// - 𝘈 (U+1D608 Mathematical Sans-Serif Italic Capital A)
// - 𝘡 (U+1D621 Mathematical Sans-Serif Italic Capital Z)
//
// See https://en.wikipedia.org/wiki/Mathematical_Alphanumeric_Symbols
const regexes: RegExp[] = [
	/[𝘈-𝘡][𝘡-𝘈]/,
	/[𝘈-𝘡][𝘡-𝘈]/u,
	/[𝘈-𝘡][𝘡-𝘈]/v,

	/[\u{1D608}-\u{1D621}][\u{1D621}-\u{1D608}]/,
	/[\u{1D608}-\u{1D621}][\u{1D621}-\u{1D608}]/u,
	/[\u{1D608}-\u{1D621}][\u{1D621}-\u{1D608}]/v,

	/[\uD835\uDE08-\uD835\uDE21][\uD835\uDE21-\uD835\uDE08]/,
	/[\uD835\uDE08-\uD835\uDE21][\uD835\uDE21-\uD835\uDE08]/u,
	/[\uD835\uDE08-\uD835\uDE21][\uD835\uDE21-\uD835\uDE08]/v,
];

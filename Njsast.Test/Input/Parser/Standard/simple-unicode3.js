"𠮷".length === 2;
"𠮷".match(/./u)[0].length === 2;
"𠮷" === "\uD842\uDFB7";
"𠮷" === "\u{20BB7}";
"𠮷".codePointAt(0) == 0x20bb7;
for (let codepoint of "𠮷") console.log(codepoint);

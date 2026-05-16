let defs = Object.create(null);

defs["en"] = {
    pluralFn(n, ord) {
        var s = String(n).split("."), v0 = !s[1], t0 = Number(s[0]) == n, n10 = t0 && s[0].slice(-1), n100 = t0 && s[0].slice(-2);
        if (ord) return n10 == 1 && n100 != 11 ? "one" : n10 == 2 && n100 != 12 ? "two" : n10 == 3 && n100 != 13 ? "few" : "other";
        return n == 1 && v0 ? "one" : "other";
    },
    td: ",",
    dd: ".",
    oq: '"',
    cq: '"'
};

export function setRules(locale, params) {
    defs[locale] = {
        pluralFn: params[0],
        td: params[1],
        dd: params[2],
        oq: params[3] ?? '"',
        cq: params[4] ?? '"'
    };
}

export function getLanguageFromLocale(locale) {
    let idx = locale.indexOf("-");
    if (idx >= 0) return locale.substr(0, idx);
    return locale;
}

export function getRules(locale) {
    let d = defs[locale];
    if (!d) {
        d = defs[getLanguageFromLocale(locale)];
        if (!d) {
            d = defs["en"];
        }
    }
    return d;
}


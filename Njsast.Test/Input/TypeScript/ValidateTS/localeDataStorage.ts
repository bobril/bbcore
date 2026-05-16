export interface ILocaleRules {
    pluralFn: (val: number, ordinal: boolean) => string;
    td: string; // thousand delimiter
    dd: string; // decimal delimiter
    oq: string; // opening quote
    cq: string; // closing quote
}

let defs: {
    en: ILocaleRules;
    [locale: string]: ILocaleRules;
} = Object.create(null);

defs["en"] = {
    pluralFn(n: number, ord: boolean) {
        var s = String(n).split("."),
            v0 = !s[1],
            t0 = Number(s[0]) == n,
            n10: any = t0 && s[0]!.slice(-1),
            n100: any = t0 && s[0]!.slice(-2);
        if (ord)
            return n10 == 1 && n100 != 11
                ? "one"
                : n10 == 2 && n100 != 12
                ? "two"
                : n10 == 3 && n100 != 13
                ? "few"
                : "other";
        return n == 1 && v0 ? "one" : "other";
    },
    td: ",",
    dd: ".",
    oq: "\"",
    cq: "\"",
};

export function setRules(locale: string, params: any[]) {
    defs[locale] = { pluralFn: params[0], td: params[1], dd: params[2], oq: params[3] ?? "\"", cq: params[4] ?? "\"" };
}

export function getLanguageFromLocale(locale: string): string {
    let idx = locale.indexOf("-");
    if (idx >= 0) return locale.substr(0, idx);
    return locale;
}

export function getRules(locale: string): ILocaleRules {
    let d = defs[locale];
    if (!d) {
        d = defs[getLanguageFromLocale(locale)];
        if (!d) {
            d = defs["en"];
        }
    }
    return d;
}

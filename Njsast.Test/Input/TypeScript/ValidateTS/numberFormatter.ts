import { ILocaleRules } from "./localeDataStorage";
import { RuntimeFunctionGenerator } from "./RuntimeFunctionGenerator";

const escapeRegExpMatcher = /[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g;

export function escapeRegExp(str: string): string {
    return str.replace(escapeRegExpMatcher, "\\$&");
}

export function buildFormatter(rules: ILocaleRules, format: string, interpret = false): (val: number) => string {
    if (format == "0b" || format == "0 b") {
        const suffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        const space = format == "0 b" ? "\xa0" : "";
        return (val: number) => {
            let suffix = "";
            for (let power = 0; power <= suffixes.length; power++) {
                var min = Math.pow(1000, power);
                var max = Math.pow(1000, power + 1);
                if (val === 0 || (val >= min && val < max)) {
                    suffix += suffixes[power];
                    if (min > 0) {
                        val = val / min;
                    }
                    break;
                }
            }
            return val.toFixed(0) + space + suffix;
        };
    }
    if (format.indexOf("%") >= 0) {
        const nested = buildFormatter(rules, format.replace("%", ""), interpret);
        return (val: number) => {
            return nested(val * 100) + "%";
        };
    }
    let decOpt = false;
    if (format.indexOf("[.]") >= 0) {
        format = format.replace("[.]", ".");
        decOpt = true;
    }
    let negPar = false;
    if (/\(.+\)/.test(format)) {
        negPar = true;
    }
    let hasThousands = false;
    if (format.indexOf(",") >= 0) {
        hasThousands = true;
    }
    let maxDec = 0;
    let minDec = 0;
    let pos = format.indexOf(".");
    if (pos >= 0) {
        let inOpt = false;
        while (++pos < format.length) {
            let ch = format.charCodeAt(pos);
            if (ch == 48) {
                // '0'
                maxDec++;
                if (!inOpt) minDec++;
            } else if (ch == 91) {
                // '['
                inOpt = true;
            } else break;
        }
    }
    if (decOpt && minDec < 2) {
        decOpt = false;
        minDec = 0;
    }
    if (interpret) {
        return (val: number) => {
            let locIsNeg = false;
            if (val < 0) {
                locIsNeg = true;
                val = -val;
            }
            let loc = val.toFixed(maxDec);
            let locBefore = loc;
            let locDec = "";

            if (maxDec > 0) {
                locBefore = loc.slice(0, loc.length - maxDec - 1);
                locDec = loc.slice(loc.length - maxDec);
                if (minDec < maxDec) {
                    locDec = locDec.replace(new RegExp(`0{1,${maxDec - minDec}}$`), "");
                }
                if (decOpt) {
                    if (locDec == Array(minDec + 1).join("0")) locDec = "";
                }
                if (locDec != "") locDec = rules.dd + locDec;
            }
            if (hasThousands) {
                locBefore = locBefore.replace(/(\d)(?=(\d{3})+(?!\d))/g, "$1" + rules.td);
            }
            loc = locBefore + locDec;
            if (negPar) {
                if (locIsNeg) loc = "(" + loc + ")";
            } else {
                if (locIsNeg) loc = "-" + loc;
            }
            return loc;
        };
    }
    let g = new RuntimeFunctionGenerator();
    const arg = g.addArg(0);
    const loc = g.addLocal();
    const locBefore = g.addLocal();
    const locDec = g.addLocal();
    const locIsNeg = g.addLocal();
    g.addBody(`var ${locIsNeg}=false;if (${arg}<0) {${locIsNeg}=true; ${arg}=-${arg};};`);
    g.addBody(`var ${locBefore},${locDec}='',${loc}=${arg}.toFixed(${maxDec});`);
    if (maxDec == 0) {
        g.addBody(`${locBefore}=${loc};`);
    } else {
        g.addBody(`${locBefore}=${loc}.slice(0,${loc}.length-${maxDec + 1});`);
        g.addBody(`${locDec}=${loc}.slice(${loc}.length-${maxDec});`);
        if (minDec < maxDec) {
            g.addBody(`${locDec}=${locDec}.replace(/0{1,${maxDec - minDec}}$/,'');`);
        }
        if (decOpt) {
            g.addBody(`if (${locDec}=='${Array(minDec + 1).join("0")}') ${locDec}='';`);
        }
        g.addBody(`if (${locDec}!='') ${locDec}='${rules.dd}'+${locDec};`);
    }
    if (hasThousands) {
        g.addBody(`${locBefore}=${locBefore}.replace(/(\\d)(?=(\\d{3})+(?!\\d))/g,'$1${rules.td}');`);
    }
    g.addBody(`${loc}=${locBefore}+${locDec};`);
    if (negPar) {
        g.addBody(`if (${locIsNeg}) ${loc}='('+${loc}+')';`);
    } else {
        g.addBody(`if (${locIsNeg}) ${loc}='-'+${loc};`);
    }
    g.addBody(`return ${loc};`);
    return g.build() as (val: number) => string;
}

export function buildUnformat(rules: ILocaleRules): (val: string) => number {
    const tdMatcher = new RegExp(escapeRegExp(rules.td), "g");
    const dd = rules.dd;
    return (val: string) => {
        var coef = 1;
        var perctI = val.indexOf("%");
        if (perctI >= 0) {
            val = val.replace("%", "");
            coef = 0.01;
        }
        var openParI = val.indexOf("(");
        if (openParI >= 0) {
            var closeParI = val.indexOf(")");
            if (closeParI > openParI) {
                coef = -coef;
                val = val.substring(openParI + 1, closeParI);
            }
        }
        return parseFloat(val.replace(tdMatcher, "").replace(dd, ".")) * coef;
    };
}

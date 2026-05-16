import { MsgAst } from "./msgFormatParser";
import { isString, isArray } from "bobril";

export function extractUsedParams(msgAst: any): string[] {
    let params = Object.create(null);
    extractUsedParamsRec(params, msgAst);
    return Object.keys(params).sort();
}

function extractUsedParamsRec(usedParams: { [name: string]: boolean }, msgAst: MsgAst) {
    if (isString(msgAst)) {
        return;
    }
    if (isArray(msgAst)) {
        for (let i = 0; i < msgAst.length; i++) {
            let item = msgAst[i]!;
            extractUsedParamsRec(usedParams, item);
        }
        return;
    }
    switch (msgAst.type) {
        case "arg":
            usedParams[msgAst.id] = true;
            return;
        case "hash":
            return;
        case "concat":
            extractUsedParamsRec(usedParams, msgAst.values);
            return;
        case "el":
            usedParams[msgAst.id] = true;
            if (msgAst.value != undefined) extractUsedParamsRec(usedParams, msgAst.value);
            return;
        case "format":
            usedParams[msgAst.id] = true;
            let type = msgAst.format.type;
            switch (type) {
                case "plural":
                case "select": {
                    let options = msgAst.format.options;
                    for (let i = 0; i < options.length; i++) {
                        extractUsedParamsRec(usedParams, options[i]!.value!);
                    }
                    break;
                }
                case "number":
                case "date":
                case "time": {
                    let options = msgAst.format.options;
                    if (options) {
                        for (let i = 0; i < options.length; i++) {
                            let opt = options[i]!;
                            if (typeof opt.value === "object") {
                                extractUsedParamsRec(usedParams, opt.value);
                            }
                        }
                    }
                    break;
                }
            }
            return;
    }
}

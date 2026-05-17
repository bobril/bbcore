"use strict";
exports.extractUsedParams = extractUsedParams;

const bobril_1 = require("bobril");

function extractUsedParams(msgAst) {
    let params = Object.create(null);
    extractUsedParamsRec(params, msgAst);
    return Object.keys(params).sort();
}

function extractUsedParamsRec(usedParams, msgAst) {
    if (bobril_1.isString(msgAst)) {
        return;
    }
    if (bobril_1.isArray(msgAst)) {
        for (let i = 0; i < msgAst.length; i++) {
            let item = msgAst[i];
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
          case "select":
            {
                let options = msgAst.format.options;
                for (let i = 0; i < options.length; i++) {
                    extractUsedParamsRec(usedParams, options[i].value);
                }
                break;
            }

          case "number":
          case "date":
          case "time":
            {
                let options = msgAst.format.options;
                if (options) {
                    for (let i = 0; i < options.length; i++) {
                        let opt = options[i];
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


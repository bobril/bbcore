"use strict";
exports.cva = void 0;

const cva = config => {
    const {variants, defaultVariants} = config;
    if (variants == undefined) {
        return props => [ config.base, props?.style ];
    }
    const variantKeys = Object.keys(variants);
    const defaultVariantMap = new Map();
    if (defaultVariants) {
        for (const key of variantKeys) {
            defaultVariantMap.set(key, defaultVariants[key]);
        }
    }
    return props => {
        const variantMap = new Map(defaultVariantMap);
        if (props) {
            for (const key of variantKeys) {
                const value = props[key];
                if (value != undefined) {
                    variantMap.set(key, value);
                }
            }
        }
        let res = [ config?.base ];
        for (let [key, value] of variantMap) {
            if (typeof value === "boolean") {
                value = value ? "true" : "false";
            }
            const varStyle = variants[key][value];
            if (varStyle != undefined) {
                res.push(varStyle);
            }
        }
        config.compoundVariants?.forEach(cvConfig => Object.entries(cvConfig).every(([cvKey, cvSelector]) => {
            const selector = variantMap.get(cvKey);
            if (selector == undefined) return true;
            return Array.isArray(cvSelector) ? cvSelector.includes(selector) : selector === cvSelector;
        }) && res.push(cvConfig.style));
        res.push(props?.style);
        return res;
    };
};

exports.cva = cva;


"use strict";
const items = getSheetNames().map((item, sheetNameIndex) => item === undefined ? item : {
    label: `${sheetNameIndex} - ${item}`,
    value: sheetNameIndex
}).filter(isNotUndefinedOrNullPredicate);


const items = getSheetNames()
    .map<WithRequired<INumberItem, "label"> | undefined>((item, sheetNameIndex) =>
        item === undefined
            ? item
            : {
                  label: `${sheetNameIndex} - ${item}`,
                  value: sheetNameIndex,
              },
    )
    .filter(isNotUndefinedOrNullPredicate);

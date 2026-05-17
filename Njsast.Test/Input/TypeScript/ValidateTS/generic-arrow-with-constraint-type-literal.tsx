const WarningValidationControl = <
    TComponentData extends IFormElementData<TValue, TParam> & {
        bobwaiValidationControlData: IData<TComponentData, TValue, TParam>;
    },
    TValue extends ValueType,
    TParam,
>(
    data: TComponentData & {
        bobwaiValidationControlData: IData<TComponentData, TValue, TParam>;
    },
): b.IBobrilNode => {
    return { tag: "div" };
};

const initialInputParameter = b.assign<
    ModuleStepParameterV6Dto,
    Pick<ArrayScriptInputParameterWithItemsIds, "arrayItemsIds">
>(
    enhanceModuleStepParameterV6Dto({
        parameterId: "inputParameter1",
    }),
    {
        arrayItemsIds: ["1", "2"],
    },
);

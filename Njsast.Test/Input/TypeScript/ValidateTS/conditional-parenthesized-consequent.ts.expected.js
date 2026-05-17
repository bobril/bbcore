"use strict";
const value = prop(isEditing ? state.content.fileGuid === undefined ? "" : state.content.fileGuid : "", ctx, (value) => actions.setValue({
    fileGuid: value
}));

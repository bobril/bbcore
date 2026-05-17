"use strict";
exports.renderInput = renderInput;

exports.renderActions = renderActions;

exports.renderChildFragment = renderChildFragment;

exports.renderObjectSpread = renderObjectSpread;

function renderInput(attrs) {
    return b.createElement("input", {
        type: "checkbox",
        ...attrs
    });
}

function renderActions(close) {
    return [ b.createElement(Button, {
        label: "Save",
        onClick: () => close()
    }), b.createElement(Button, {
        label: "Cancel",
        onClick: () => close()
    }) ];
}

function renderChildFragment() {
    return b.createElement(Stack, null, b.createElement(b.Fragment, null), b.createElement(Label, {
        text: "Name"
    }));
}

function renderObjectSpread(comment, programId) {
    return b.createElement(Button, {
        comment,
        programId
    });
}

import * as Monaco from "monaco-editor";

// Force usage of Monaco namespace object so the bundler cannot tree-shake it away.
const tsDefaults = Monaco.languages.typescript.typescriptDefaults;

document.addEventListener("DOMContentLoaded", () => {
    console.log("Monaco module used:", Monaco, tsDefaults);
    const div = document.createElement("div");
    div.textContent = "hello";
    document.body.appendChild(div);
});

import * as FakeEditor from "./src/fake-editor";

document.addEventListener("DOMContentLoaded", () => {
    console.log("Loaded fake editor:", (FakeEditor as any).loadLanguage());
});

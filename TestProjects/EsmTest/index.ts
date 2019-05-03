import * as b from "bobril";
import * as monaco from "monaco-editor";

(window as any).MonacoEnvironment = {
  getWorkerUrl: function(moduleId: string, label: string) {
    console.log("GetWorkerUrl", moduleId, label);
    if (label === "json") {
      return "./json.worker.js";
    }
    if (label === "css") {
      return "./css.worker.js";
    }
    if (label === "html") {
      return "./html.worker.js";
    }
    if (label === "typescript" || label === "javascript") {
      return b.asset("resource:./ts.worker.js");
    }
    return b.asset("resource:./editor.worker.js");
  }
};

class MonacoApp extends b.Component<{}> {
  editor: monaco.editor.IStandaloneCodeEditor;

  render() {
    return b.styledDiv([], { width: "100%", height: "100%" });
  }
  postInitDom() {
    this.editor = monaco.editor.create(b.getDomNode(this.me) as HTMLElement, {
      value: ["function x() {", '\tconsole.log("Hello world!");', "}"].join(
        "\n"
      ),
      language: "typescript"
    });
  }
  postUpdateDomEverytime() {
    this.editor.layout();
  }
}

const app = b.component(MonacoApp);

b.selectorStyleDef("html, body", {
  width: "100%",
  height: "100%",
  padding: 0,
  margin: 0
});

b.init(() => app());

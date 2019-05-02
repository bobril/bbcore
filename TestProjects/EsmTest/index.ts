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
      return "./ts.worker.js";
    }
    return "./editor.worker.js";
  }
};

class MonacoApp extends b.Component<{}> {
  render() {
    return b.styledDiv([]);
  }
  postInitDom() {
    monaco.editor.create(b.getDomNode(this.me) as HTMLElement, {
      value: ["function x() {", '\tconsole.log("Hello world!");', "}"].join(
        "\n"
      ),
      language: "javascript"
    });
  }
}

const app = b.component(MonacoApp);
b.init(() => app());

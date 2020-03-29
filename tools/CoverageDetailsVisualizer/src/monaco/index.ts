import * as b from "bobril";
import * as monaco from "monaco-editor";

(window as any).MonacoEnvironment = {
    getWorkerUrl: function(moduleId: string, label: string) {
        console.log("GetWorkerUrl", moduleId, label);
        if (label === "json") {
            return b.asset("project:worker:node_modules/monaco-editor/esm/vs/language/json/json.worker.js");
        }
        if (label === "css") {
            return b.asset("project:worker:node_modules/monaco-editor/esm/vs/language/css/css.worker.js");
        }
        if (label === "html") {
            return b.asset("project:worker:node_modules/monaco-editor/esm/vs/language/html/html.worker.js");
        }
        if (label === "typescript" || label === "javascript") {
            return b.asset("project:worker:node_modules/monaco-editor/esm/vs/language/typescript/ts.worker.js");
        }
        return b.asset("project:worker:node_modules/monaco-editor/esm/vs/editor/editor.worker.js");
    }
};

interface IMonacoEditorData {
    style?: b.IBobrilStyles;
    language: string;
    theme?: "vs" | "vs-dark" | "hc-black" | string;
    onInit?: (editor: monaco.editor.IStandaloneCodeEditor) => void;
}

class MonacoComponentClass extends b.Component<IMonacoEditorData> {
    editor?: monaco.editor.IStandaloneCodeEditor;
    currentTheme?: string;

    render(data: IMonacoEditorData) {
        return b.styledDiv([], data.style);
    }
    postInitDom() {
        this.currentTheme = this.data.theme;
        this.editor = monaco.editor.create(b.getDomNode(this.me) as HTMLElement, {
            language: this.data.language,
            theme: this.data.theme,
            scrollbar: {}
        });
        this.data.onInit?.(this.editor);
        this.editor.layout();
    }
    postUpdateDomEverytime() {
        if (this.data.theme && this.data.theme != this.currentTheme) {
            this.currentTheme = this.data.theme;
            monaco.editor.setTheme(this.data.theme);
        }
        this.editor.layout();
    }
}

export const MonacoComponent = b.component(MonacoComponentClass);

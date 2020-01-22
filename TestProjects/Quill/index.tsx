import * as b from "bobril";
import QuillLib from "quill";
import * as q from "quill";
import Delta from "quill-delta";

b.asset("node_modules/quill/dist/quill.snow.css");

interface IQuillProps {
  options?: q.QuillOptionsStatic;
  style?: b.IBobrilStyles;
}

declare module "bobril" {
  export interface IBubblingAndBroadcastEvents {
    onQuillTextChange?(event: {
      target: b.IBobrilCacheNode;
      instance: q.Quill;
      delta: Delta;
      oldContent: Delta;
      source: string;
    });
  }
}

function Quill(this: b.IBobrilCtx, prop: IQuillProps) {
  b.useEffect(() => {
    var instance = new QuillLib(b.getDomNode(this.me) as Element, prop.options);
    instance.on("text-change", (delta, oldContent, source) => {
      b.bubble(this.me, "onQuillTextChange", {
        instance,
        delta,
        oldContent,
        source
      });
    });
  }, []);
  return <div style={prop.style}></div>;
}

b.selectorStyleDef("html, body", {
  width: "100%",
  height: "100%",
  margin: 0,
  padding: 0
});

b.init(() => {
  let lastChange = b.useState("");
  b.useEvents({
    onQuillTextChange: ({ delta }) => {
      lastChange(JSON.stringify(delta));
    }
  });
  return (
    <>
      <Quill
        style={{ height: 300 }}
        options={{
          modules: {
            toolbar: [
              [{ header: [1, 2, false] }],
              ["bold", "italic", "underline"],
              ["image", "code-block"]
            ]
          },
          placeholder: "Write some text ...",
          theme: "snow"
        }}
      />
      <p>Json of last delta: {lastChange()}</p>
    </>
  );
});

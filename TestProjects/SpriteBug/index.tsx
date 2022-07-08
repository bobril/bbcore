import * as b from "bobril";

var Sprite = b.component(
  b.createComponent({
    init() {
      b.selectorStyleDef(".icon", [b.sprite("./light.png", () => "#ff0044")]);
    },
    render(_, me) {
      me.children = <div className="icon"></div>;
    },
  })
);

function App() {
  let visible = b.useState(false);
  return (
    <>
      <button onClick={() => (visible(true), false)}>Show</button>
      <div>{visible() && <Sprite></Sprite>}</div>
    </>
  );
}

b.init(() => <App></App>);

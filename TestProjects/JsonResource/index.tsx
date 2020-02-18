import * as b from "bobril";
import { observable } from "bobx";
export const data = observable<string>([]);
const test = b.asset("./test.json");
async function init(): Promise<void> {
  data.push(await (await fetch(test)).text());
}

b.init(() => {
  return (
    <>
      <button onClick={() => (init(), true)}>Load</button>
      <div>{data.slice()}</div>
    </>
  );
});

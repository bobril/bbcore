import * as b from "bobril";

import Sample, { metadata } from "./sample.mdxb";

console.log(metadata);

b.init(() => <Sample name="boris" />);

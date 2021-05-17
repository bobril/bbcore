import * as b from "bobril";
import * as mdxCodeBlock from "@bobril/mdx/highlighter";
import * as styles from "@bobril/highlighter/styles";

import Sample, { metadata } from "./sample.mdxb";

console.log(metadata);

mdxCodeBlock.setDefaultCodeBlock(styles.docco);

b.init(() => <Sample name="boris" />);

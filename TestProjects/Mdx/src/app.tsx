import * as b from "bobril";
import * as mdxCodeBlock from "@bobril/mdx/highlighter";
import * as styles from "@bobril/highlighter/styles";

import mdFeatures from "./mdFeatures/.mdxb";

//import Sample from "./sample.mdxb";

mdxCodeBlock.setDefaultCodeBlock(styles.docco);

function Lazy({ import: from }: { import: () => Promise<b.IComponentFactory<any>> }) {
    var factory = b.useState<b.IComponentFactory<any> | undefined>(undefined);
    b.useEffect(() => {
        from().then((f) => factory(f));
    });
    return factory() != undefined ? factory()!() : <div>Loading ...</div>;
}

function Features() {
    var i = b.useState(0);
    var f = mdFeatures[i()];
    return (
        <>
            <button onClick={() => i((i() + mdFeatures.length - 1) % mdFeatures.length)}>Prev</button>
            <button onClick={() => i((i() + 1) % mdFeatures.length)}>Next</button>
            <h1>{f[1].name}</h1>
            <Lazy import={f[0]} />
        </>
    );
}

b.init(() => <Features />);

//b.init(() => <Sample name="boris" />);

import * as b from "bobril";
import "@bobril/mdx";
import mdFeatures from "./mdFeatures/.mdxb";

//import Sample from "./sample.mdxb";

const mdFeaturesLazy = mdFeatures.map(
    (f) =>
        [
            b.lazy(async () => {
                await import("./initHighlighter");
                return await f[0]();
            }),
            f[1],
        ] as const
);

function Features() {
    var i = b.useState(0);
    var f = mdFeaturesLazy[i()];
    var Content = f[0];
    return (
        <div style={{ display: "flex", minHeight: "100vh" }}>
            <nav
                style={{
                    width: "250px",
                    padding: "20px",
                    borderRight: "1px solid #eaeaea",
                    backgroundColor: "#f8f9fa",
                }}
            >
                {mdFeaturesLazy.map((feature, idx) => (
                    <button
                        key={idx}
                        onClick={(e) => {
                            i(idx);
                            e.stopPropagation();
                        }}
                        style={{
                            display: "block",
                            width: "100%",
                            padding: "10px",
                            margin: "5px 0",
                            textAlign: "left",
                            border: "none",
                            borderRadius: "4px",
                            backgroundColor: idx === i() ? "#e9ecef" : "transparent",
                            cursor: "pointer",
                        }}
                    >
                        {feature[1].name}
                    </button>
                ))}
            </nav>
            <main
                style={{
                    flex: 1,
                    padding: "40px",
                    backgroundColor: "#ffffff",
                }}
            >
                <h1
                    style={{
                        marginTop: 0,
                        marginBottom: "30px",
                        color: "#212529",
                    }}
                >
                    {f[1].name}
                </h1>
                <b.Suspense fallback={<div>Loading ...</div>}>
                    <Content />
                </b.Suspense>
            </main>
        </div>
    );
}

b.injectCss(`
    html {
        margin: 0;
    }
    body {
        margin: 0;
    }
`);

b.init(() => <Features />);

//b.init(() => <Sample name="boris" />);

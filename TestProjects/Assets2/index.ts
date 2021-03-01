import * as b from "bobril";

const iframeContent = "embedded-iframe/index.htm";

b.init(() => ({ tag: "iframe", attrs: { src: iframeContent } }));

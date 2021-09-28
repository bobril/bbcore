import * as b from "bobril";
import { DirectoryPage } from "./pages/dir/index";
import { FilePage } from "./pages/file/index";
import { RootPage } from "./pages/root/index";

b.selectorStyleDef("html, body", {
    width: "100%",
    height: "100%",
    padding: 0,
    margin: 0,
});

b.routes({
    handler: (data: b.IRouteHandlerData) => <RootPage page={data.activeRouteHandler} />,
    children: [
        {
            name: "dir",
            url: "/dir/*",
            handler: (data: b.IRouteHandlerData) => <DirectoryPage name={data.routeParams.splat!} />,
        },
        {
            name: "file",
            url: "/file/*",
            handler: (data: b.IRouteHandlerData) => <FilePage name={data.routeParams.splat!} />,
        },
        {
            name: "rootdir",
            url: "/root",
            handler: (_data: b.IRouteHandlerData) => <DirectoryPage name="*" />,
            isDefault: true,
            isNotFound: true,
        },
    ],
});

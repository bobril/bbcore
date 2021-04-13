import * as b from "bobril";

export function Counter() {
    var c = b.useState(0);
    b.useEffect(() => {
        setInterval(() => c(c() + 1), 1000);
    }, []);
    return <i>{c()}</i>;
}

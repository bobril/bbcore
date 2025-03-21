import * as b from "bobril";

export function Counter() {
    var c = b.useState(0);
    b.useEffect(() => {
        const interval = setInterval(() => c(c() + 1), 1000);
        return () => clearInterval(interval);
    }, []);
    return <i>{c()}</i>;
}

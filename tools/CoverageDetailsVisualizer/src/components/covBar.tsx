import * as b from "bobril";

export function CovBar({ value }: { value: string }) {
    if (value == "N/A")
        return (
            <div
                style={{ width: "100%", overflow: "visible", backgroundColor: "rgba(0,0,0,20%)", textAlign: "center" }}
            >
                N/A
            </div>
        );
    let p = parseInt(value);
    let color = p >= 80 ? "rgba(0,255,0,20%)" : p >= 50 ? "rgba(240,240,0,40%)" : "rgba(255,0,0,20%)";
    return (
        <div style={{ width: value, overflow: "visible", backgroundColor: color }}>
            <div
                style={{ width: "4rem", textAlign: "right", borderColor: color, borderWidth: 1, borderStyle: "solid" }}
            >
                {value}
            </div>
        </div>
    );
}

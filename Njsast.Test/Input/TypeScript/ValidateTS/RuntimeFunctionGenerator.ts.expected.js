export class RuntimeFunctionGenerator {
    constructor() {
        this.constants = [];
        this.body = [];
        this.argCount = 0;
        this.localCount = 0;
    }
    addConstant(value) {
        let cc = this.constants;
        for (let i = 0; i < cc.length; i++) {
            if (cc[i] === value) return "c_" + i;
        }
        cc.push(value);
        return "c_" + (cc.length - 1);
    }
    addArg(index) {
        if (index >= this.argCount) this.argCount = index + 1;
        return "a_" + index;
    }
    addBody(...texts) {
        [].push.apply(this.body, texts);
    }
    addLocal() {
        return "l_" + this.localCount++;
    }
    build() {
        let innerParams = [];
        for (let i = 0; i < this.argCount; i++) {
            innerParams.push("a_" + i);
        }
        if (this.constants.length > 0) {
            let params = [];
            for (let i = 0; i < this.constants.length; i++) {
                params.push("c_" + i);
            }
            this.body.unshift("return function(", innerParams.join(","), ") {\n");
            this.body.push("\n}");
            params.push(this.body.join(""));
            return Function.apply(null, params).apply(null, this.constants);
        }
        innerParams.push(this.body.join(""));
        return Function.apply(null, innerParams);
    }
}


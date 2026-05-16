enum Mode {
    A = 1,
    B = A + 2,
    C,
    Template = `value`,
    AfterTemplate,
    1 = "one",
    StringValue = "text",
    StringAlias = StringValue,
    ["computed-name"] = 5,
    AfterComputed,
    "a\"b" = 7,
    ["escaped\\name"] = 8
}
console.log(Mode.B, Mode.C, Mode.Template, Mode[0]);

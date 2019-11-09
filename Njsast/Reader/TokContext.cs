using System;

namespace Njsast.Reader
{
    public sealed class TokContext
    {
        public static readonly TokContext BStat = new TokContext("{", false);
        public static readonly TokContext BExpr = new TokContext("{", true);
        public static readonly TokContext BTmpl = new TokContext("${", false);
        public static readonly TokContext PStat = new TokContext("(", false);
        public static readonly TokContext PExpr = new TokContext("(", true);
        public static readonly TokContext QTmpl = new TokContext("`", true, true, p => p.TryReadTemplateToken());
        public static readonly TokContext FStat = new TokContext("function", false);
        public static readonly TokContext FExpr = new TokContext("function", true);
        public static readonly TokContext FExprGen = new TokContext("function", true, false, null, true);
        public static readonly TokContext FGen = new TokContext("function", false, false, null, true);

        public TokContext(string token, bool isExpr, bool preserveSpace = false, Action<Parser>? @override = null, bool generator = false)
        {
            Token = token;
            IsExpression = isExpr;
            PreserveSpace = preserveSpace;
            Override = @override;
            Generator = generator;
        }

        public string Token { get; }
        public bool IsExpression { get; }
        public bool PreserveSpace { get; }
        public Action<Parser>? Override { get; }
        public bool Generator { get; }
    }
}

using Njsast.Ast;
using Njsast.ConstEval;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Njsast.Bobril
{
    public class GatherBobrilSourceInfo
    {
        public static SourceInfo Gather(AstNode toplevel, IConstEvalCtx ctx,
            Func<IConstEvalCtx, string, string> stringResolver)
        {
            var evalCtx = new BobrilSpecialEvalWithPath(ctx, stringResolver);
            var gatherer = new GatherTreeWalker(evalCtx);
            gatherer.Walk(toplevel);
            return gatherer.SourceInfo;
        }

        public class BobrilSpecialEval : IConstEvalCtx
        {
            readonly IConstEvalCtx _ctx;

            public BobrilSpecialEval(IConstEvalCtx ctx)
            {
                _ctx = ctx;
            }

            public JsModule ResolveRequire(string name)
            {
                return _ctx.ResolveRequire(name);
            }

            public object? ConstValue(IConstEvalCtx ctx, JsModule module, object export)
            {
                if (module.Name == "bobril" && export is string expName)
                {
                    if (expName == "asset" || expName == "styleDef" || expName == "styleDefEx" || expName == "sprite")
                        return new JsModuleExport(module.Name, expName);
                }

                if (module.Name == "bobril-g11n" && export is string expName2)
                {
                    if (expName2 == "t" || expName2 == "f" || expName2 == "dt")
                        return new JsModuleExport(module.Name, expName2);
                }

                return _ctx.ConstValue(ctx, module, export);
            }

            virtual public IConstEvalCtx StripPathResolver()
            {
                return this;
            }

            virtual public string ConstStringResolver(string str)
            {
                return str;
            }

            virtual public IConstEvalCtx CreateForSourceName(string sourceName)
            {
                return new BobrilSpecialEval(_ctx.CreateForSourceName(sourceName));
            }

            public bool AllowEvalObjectWithJustConstKeys => true;

            public string SourceName => _ctx.SourceName;

            public bool JustModuleExports
            {
                get => _ctx.JustModuleExports;
                set => _ctx.JustModuleExports = value;
            }
        }

        class BobrilSpecialEvalWithPath : BobrilSpecialEval
        {
            BobrilSpecialEval _stripped;
            Func<IConstEvalCtx, string, string> _stringResolver;

            public BobrilSpecialEvalWithPath(IConstEvalCtx ctx, Func<IConstEvalCtx, string, string> stringResolver) :
                base(ctx)
            {
                _stripped = new BobrilSpecialEval(ctx);
                _stringResolver = stringResolver;
            }

            public override IConstEvalCtx StripPathResolver()
            {
                return _stripped;
            }

            public override string ConstStringResolver(string str)
            {
                return _stringResolver(_stripped, str);
            }

            public override IConstEvalCtx CreateForSourceName(string sourceName)
            {
                return new BobrilSpecialEvalWithPath(_stripped.CreateForSourceName(sourceName), _stringResolver);
            }
        }

        class GatherTreeWalker : TreeWalker
        {
            readonly IConstEvalCtx _evalCtxWithPath;
            readonly IConstEvalCtx _evalCtx;
            internal SourceInfo SourceInfo;

            public GatherTreeWalker(IConstEvalCtx evalCtx)
            {
                _evalCtxWithPath = evalCtx;
                _evalCtx = evalCtx.StripPathResolver();
                SourceInfo = new SourceInfo();
            }

            protected override void Visit(AstNode node)
            {
                if (node.IsProcessEnv() is {} prop)
                {
                    var processEnv = new SourceInfo.ProcessEnv
                    {
                        Name = prop,
                        StartLine = node.Start.Line,
                        StartCol = node.Start.Column,
                        EndLine = node.End.Line,
                        EndCol = node.End.Column
                    };
                    if (SourceInfo.ProcessEnvs == null)
                    {
                        SourceInfo.ProcessEnvs = new List<SourceInfo.ProcessEnv>();
                    }

                    SourceInfo.ProcessEnvs.Add(processEnv);
                }
                else if (node is AstCall call)
                {
                    if (call.Expression is AstSymbol expSymbol && call.Args.Count == 1)
                    {
                        var def = expSymbol.Thedef!;
                        if (def.Global && def.Name == "require")
                        {
                            var arg = call.Args[0];
                            if (arg is AstString str)
                            {
                                var imp = new SourceInfo.Import
                                {
                                    Name = str.Value,
                                    StartLine = str.Start.Line,
                                    StartCol = str.Start.Column,
                                    EndLine = str.End.Line,
                                    EndCol = str.End.Column
                                };
                                if (SourceInfo.Imports == null)
                                {
                                    SourceInfo.Imports = new List<SourceInfo.Import>();
                                }

                                SourceInfo.Imports.Add(imp);
                                if (str.Value == "bobril" && SourceInfo.BobrilImport == null)
                                {
                                    SourceInfo.BobrilImport = ExpressionName();
                                }
                                else if (str.Value == "bobril-g11n" && SourceInfo.BobrilG11NImport == null)
                                {
                                    SourceInfo.BobrilG11NImport = ExpressionName();
                                }
                            }
                        }

                        return;
                    }

                    _evalCtx.JustModuleExports = true;
                    var fn = call.Expression.ConstValue(_evalCtx);
                    if (fn != null)
                    {
                        _evalCtx.JustModuleExports = false;
                        if (fn is JsModuleExport exp)
                        {
                            if (exp.ModuleName == "bobril")
                            {
                                if (exp.ExportName == "asset" && call.Args.Count >= 1)
                                {
                                    var nameArg = call.Args[0];
                                    var asset = new SourceInfo.Asset
                                    {
                                        StartLine = nameArg.Start.Line,
                                        StartCol = nameArg.Start.Column,
                                        EndLine = nameArg.End.Line,
                                        EndCol = nameArg.End.Column,
                                        Name = nameArg.ConstValue(_evalCtxWithPath) as string
                                    };
                                    if (SourceInfo.Assets == null)
                                    {
                                        SourceInfo.Assets = new List<SourceInfo.Asset>();
                                    }

                                    SourceInfo.Assets.Add(asset);
                                }
                                else if (exp.ExportName == "styleDef" || exp.ExportName == "styleDefEx")
                                {
                                    var styleDef = new SourceInfo.StyleDef
                                    {
                                        IsEx = exp.ExportName == "styleDefEx"
                                    };
                                    var argBeforeName =
                                        call.Args[Math.Min(1u + (styleDef.IsEx ? 1u : 0), call.Args.Count - 1)];
                                    styleDef.ArgCount = call.Args.Count;
                                    styleDef.BeforeNameLine = argBeforeName.End.Line;
                                    styleDef.BeforeNameCol = argBeforeName.End.Column;
                                    if (call.Args.Count == 3 + (styleDef.IsEx ? 1 : 0))
                                    {
                                        styleDef.UserNamed = true;
                                        var nameArg = call.Args.Last;
                                        styleDef.StartLine = nameArg.Start.Line;
                                        styleDef.StartCol = nameArg.Start.Column;
                                        styleDef.EndLine = nameArg.End.Line;
                                        styleDef.EndCol = nameArg.End.Column;
                                        styleDef.Name = nameArg.ConstValue(_evalCtx) as string;
                                    }
                                    else
                                    {
                                        styleDef.Name = ExpressionName();
                                    }

                                    if (SourceInfo.StyleDefs == null)
                                    {
                                        SourceInfo.StyleDefs = new List<SourceInfo.StyleDef>();
                                    }

                                    SourceInfo.StyleDefs.Add(styleDef);
                                }
                                else if (exp.ExportName == "sprite")
                                {
                                    var sprite = new SourceInfo.Sprite
                                    {
                                        Width = -1,
                                        Height = -1,
                                        X = -1,
                                        Y = -1,
                                        HasColor = call.Args.Count >= 2,
                                        StartLine = call.Start.Line,
                                        StartCol = call.Start.Column,
                                        EndLine = call.End.Line,
                                        EndCol = call.End.Column
                                    };
                                    for (var i = 0u; i < call.Args.Count; i++)
                                    {
                                        var arg = call.Args[i];
                                        switch (i)
                                        {
                                            case 0:
                                                sprite.NameStartLine = arg.Start.Line;
                                                sprite.NameStartCol = arg.Start.Column;
                                                sprite.NameEndLine = arg.End.Line;
                                                sprite.NameEndCol = arg.End.Column;
                                                break;
                                            case 1:
                                                sprite.ColorStartLine = arg.Start.Line;
                                                sprite.ColorStartCol = arg.Start.Column;
                                                sprite.ColorEndLine = arg.End.Line;
                                                sprite.ColorEndCol = arg.End.Column;
                                                break;
                                        }

                                        var res = arg.ConstValue(i == 0 ? _evalCtxWithPath : _evalCtx);
                                        if (res != null)
                                            switch (i)
                                            {
                                                case 0:
                                                    sprite.Name = res as string;
                                                    break;
                                                case 1:
                                                    sprite.Color = res as string;
                                                    break;
                                                case 2:
                                                    if (Runtime.TypeConverter.GetJsType(res) == Runtime.JsType.Number)
                                                        sprite.Width = Runtime.TypeConverter.ToInt32(res);
                                                    break;
                                                case 3:
                                                    if (Runtime.TypeConverter.GetJsType(res) == Runtime.JsType.Number)
                                                        sprite.Height = Runtime.TypeConverter.ToInt32(res);
                                                    break;
                                                case 4:
                                                    if (Runtime.TypeConverter.GetJsType(res) == Runtime.JsType.Number)
                                                        sprite.X = Runtime.TypeConverter.ToInt32(res);
                                                    break;
                                                case 5:
                                                    if (Runtime.TypeConverter.GetJsType(res) == Runtime.JsType.Number)
                                                        sprite.Y = Runtime.TypeConverter.ToInt32(res);
                                                    break;
                                                default:
                                                    ReportErrorInJSNode(SourceInfo, call, -6,
                                                        "b.sprite cannot have more than 6 parameters");
                                                    break;
                                            }
                                    }

                                    if (SourceInfo.Sprites == null)
                                    {
                                        SourceInfo.Sprites = new List<SourceInfo.Sprite>();
                                    }

                                    SourceInfo.Sprites.Add(sprite);
                                }
                            }
                            else if (exp.ModuleName == "bobril-g11n")
                            {
                                if (exp.ExportName == "f" && call.Args.Count == 2)
                                {
                                    var messageArg = call.Args[0];
                                    var tr = new SourceInfo.Translation
                                    {
                                        JustFormat = true,
                                        StartLine = messageArg.Start.Line,
                                        StartCol = messageArg.Start.Column,
                                        EndLine = messageArg.End.Line,
                                        EndCol = messageArg.End.Column,
                                        Message = messageArg.ConstValue(_evalCtx) as string
                                    };
                                    var paramsArg = call.Args[1].ConstValue(_evalCtx);
                                    if (!(paramsArg is AstNull) && !(paramsArg is AstUndefined))
                                    {
                                        tr.WithParams = true;
                                        var pars = paramsArg as IDictionary<object, object>;
                                        if (pars != null)
                                        {
                                            tr.KnownParams = pars.Keys.Select(a => a as string).Where(a => a != null)
                                                .ToList();
                                        }
                                    }

                                    if (SourceInfo.Translations == null)
                                        SourceInfo.Translations = new List<SourceInfo.Translation>();
                                    SourceInfo.Translations.Add(tr);
                                }
                                else if ((exp.ExportName == "t" || exp.ExportName == "dt") && call.Args.Count >= 1 &&
                                         call.Args.Count <= 3)
                                {
                                    var messageArg = call.Args[0];
                                    var tr = new SourceInfo.Translation
                                    {
                                        StartLine = messageArg.Start.Line,
                                        StartCol = messageArg.Start.Column,
                                        EndLine = messageArg.End.Line,
                                        EndCol = messageArg.End.Column,
                                        Message = messageArg.ConstValue(_evalCtx) as string
                                    };
                                    if (call.Args.Count >= 2)
                                    {
                                        var paramsArg = call.Args[1];
                                        var paramsValue = paramsArg.ConstValue(_evalCtx);
                                        if (!(paramsValue is AstNull) && !(paramsValue is AstUndefined))
                                        {
                                            tr.WithParams = true;
                                            if (paramsValue is IDictionary<object, object> pars)
                                            {
                                                tr.KnownParams = pars.Keys.Select(a => a is double d?d.ToString(CultureInfo.InvariantCulture):a as string)
                                                    .Where(a => a != null).ToList();
                                            }
                                        }

                                        if (call.Args.Count >= 3)
                                        {
                                            var hintArg = call.Args[2];
                                            tr.StartHintLine = paramsArg.End.Line;
                                            tr.StartHintCol = paramsArg.End.Column;
                                            tr.EndHintLine = hintArg.End.Line;
                                            tr.EndHintCol = hintArg.End.Column;
                                            var hintStr = hintArg.ConstValue(_evalCtx) as string;
                                            tr.Hint = hintStr;
                                        }
                                    }

                                    if (SourceInfo.Translations == null)
                                        SourceInfo.Translations = new List<SourceInfo.Translation>();
                                    SourceInfo.Translations.Add(tr);
                                }
                            }
                        }
                    }
                    else
                    {
                        _evalCtx.JustModuleExports = false;
                    }
                }
            }

            string? ExpressionName()
            {
                var parent = Parent();
                if (parent is AstVarDef varDef)
                {
                    if (varDef.Name is AstSymbol astSymbol)
                    {
                        return astSymbol.Name;
                    }
                }

                if (parent is AstAssign assign)
                {
                    if (assign.Left is AstDot dot)
                    {
                        if (dot.Property is string) return (string) dot.Property;
                    }

                    if (assign.Left is AstSymbol astSymbol)
                    {
                        return astSymbol.Name;
                    }
                }

                return null;
            }

            void ReportErrorInJSNode(SourceInfo sourceInfo, AstNode node, int code, string message)
            {
                if (sourceInfo.Diagnostics == null)
                {
                    sourceInfo.Diagnostics = new List<Diagnostic>();
                }

                var d = new Diagnostic
                {
                    Code = code,
                    Text = message,
                    IsError = true,
                    StartLine = node.Start.Line,
                    StartCol = node.Start.Column,
                    EndLine = node.End.Line,
                    EndCol = node.End.Column
                };
                sourceInfo.Diagnostics.Add(d);
            }
        }
    }
}

using Njsast.Ast;
using Njsast.ConstEval;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

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
                    if (expName == "asset" || expName == "styleDef" || expName == "styleDefEx" || expName == "sprite" ||
                        expName == "createElement")
                        return new JsModuleExport(module.Name, expName);
                }

                if (module.Name == "bobril-g11n" && export is string expName2)
                {
                    if (expName2 == "t" || expName2 == "f" || expName2 == "dt" || expName2 == "T")
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
                    if (call.Expression is AstSymbol expSymbol && !(call.Expression is AstThis) && call.Args.Count == 1)
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
                                if (exp.ExportName == "createElement" && call.Args.Count >= 2 &&
                                    IsComponentT(call.Args[0]))
                                {
                                    ProcessVdomTranslation(call);
                                    StopDescending();
                                    return;
                                }

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
                                        if (paramsArg is IDictionary<object, object> pars)
                                        {
                                            tr.KnownParams = pars.Keys.Select(a => a as string).Where(a => a != null)
                                                .Select(a => a!).ToList();
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
                                                tr.KnownParams = pars.Keys.Select(a =>
                                                        a is double d
                                                            ? d.ToString(CultureInfo.InvariantCulture)
                                                            : a as string)
                                                    .Where(a => a != null).Select(a => a!).ToList();
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

            struct VdomContext
            {
                public StringBuilder SB;
                public int ElementCounter;
                public int InsertPropLine;
                public int InsertPropCol;
                public bool InsertPropComma;
            }

            void ProcessVdomTranslation(AstCall call)
            {
                // Convert this input:
                // b.createElement(g.T, { p1: "param1" }, "Before", b.createElement("hr", null), g.t("{p1}"))
                // Into this output:
                // g.t("Before{1/}{p1}", { p1: "param1", 1:function(){return b.createElement("hr", null)}})
                var tr = new SourceInfo.VdomTranslation();
                tr.Replacements = new List<SourceInfo.Replacement>();
                tr.KnownParams = new List<string>();
                tr.StartLine = call.Start.Line;
                tr.StartCol = call.Start.Column;
                tr.EndLine = call.End.Line;
                tr.EndCol = call.End.Column;
                tr.Replacements.Add(new SourceInfo.Replacement
                {
                    Type = SourceInfo.ReplacementType.Normal,
                    StartLine = call.Expression.Start.Line,
                    StartCol = call.Expression.Start.Column,
                    EndLine = call.Expression.End.Line,
                    EndCol = call.Expression.End.Column,
                    Text = $"{SourceInfo.BobrilG11NImport}.t"
                });
                tr.Replacements.Add(new SourceInfo.Replacement
                {
                    Type = SourceInfo.ReplacementType.MessageId,
                    StartLine = call.Args[0].Start.Line,
                    StartCol = call.Args[0].Start.Column,
                    EndLine = call.Args[0].End.Line,
                    EndCol = call.Args[0].End.Column
                });
                if (SourceInfo.VdomTranslations == null)
                {
                    SourceInfo.VdomTranslations = new List<SourceInfo.VdomTranslation>();
                }

                VdomContext ctx = new VdomContext();
                ctx.SB = new StringBuilder();
                ctx.ElementCounter = 0;
                Walk(call.Args[1]);
                if (call.Args[1] is AstNull)
                {
                    ctx.InsertPropLine = call.Args[1].Start.Line;
                    ctx.InsertPropCol = call.Args[1].Start.Column;
                    tr.Replacements.Add(new SourceInfo.Replacement
                    {
                        Type = SourceInfo.ReplacementType.Normal,
                        StartLine = ctx.InsertPropLine,
                        StartCol = ctx.InsertPropCol,
                        EndLine = ctx.InsertPropLine,
                        EndCol = ctx.InsertPropCol,
                        Text = "{"
                    });
                    ctx.InsertPropComma = false;
                }
                else if (call.Args[1] is AstObject astObject)
                {
                    ProcessVdomPropsAsObject(call, astObject, tr, ref ctx);
                }
                else if (call.Args[1] is AstCall astCall &&
                         astCall.Expression.IsSymbolDef().IsGlobalSymbol() == "__assign" && astCall.Args.Count > 1 &&
                         astCall.Args[0] is AstObject astObject2)
                {
                    ProcessVdomPropsAsObject(astCall, astObject2, tr, ref ctx);
                }
                else throw new NotImplementedException("g.T has too complex props: " + call.Args[1].PrintToString());

                tr.Replacements.Add(new SourceInfo.Replacement
                {
                    Type = SourceInfo.ReplacementType.Normal,
                    StartLine = call.Args[1].End.Line,
                    StartCol = call.Args[1].End.Column,
                    EndLine = call.Args[2].Start.Line,
                    EndCol = call.Args[2].Start.Column,
                });
                GatherVdomTranslation(call.Args.AsReadOnlySpan(2), tr, ref ctx);

                if (call.Args[1] is AstNull)
                {
                    tr.Replacements.Add(new SourceInfo.Replacement
                    {
                        Type = SourceInfo.ReplacementType.Normal,
                        StartLine = ctx.InsertPropLine,
                        StartCol = ctx.InsertPropCol,
                        EndLine = call.Args[1].End.Line,
                        EndCol = call.Args[1].End.Column,
                        Text = "}"
                    });
                }

                tr.Message = ctx.SB.ToString();
                SourceInfo.VdomTranslations.Add(tr);
            }

            void ProcessVdomPropsAsObject(AstCall astCall, AstObject astObject, SourceInfo.VdomTranslation tr,
                ref VdomContext ctx)
            {
                if (astObject.Properties.Count == 0)
                {
                    ctx.InsertPropLine = astCall.Args[1].End.Line;
                    ctx.InsertPropCol = astCall.Args[1].End.Column - 1;
                    ctx.InsertPropComma = false;
                }
                else
                {
                    for (var i = 0; i < astObject.Properties.Count; i++)
                    {
                        if (astObject.Properties[i] is AstObjectKeyVal keyVal)
                        {
                            var key = keyVal.Key.ConstValue(_evalCtx);
                            if (key as string != "hint") continue;
                            var val = keyVal.Value.ConstValue(_evalCtx) as string;
                            tr.Hint = val;
                            tr.Replacements!.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.Normal,
                                StartLine = astObject.Properties[i].Start.Line,
                                StartCol = astObject.Properties[i].Start.Column,
                                EndLine = NextStartOrEnd(i, astObject.Properties.AsReadOnlySpan()).Line,
                                EndCol = NextStartOrEnd(i, astObject.Properties.AsReadOnlySpan()).Col,
                            });
                        }
                    }

                    ctx.InsertPropLine = astObject.Properties.Last.End.Line;
                    ctx.InsertPropCol = astObject.Properties.Last.End.Column;
                    ctx.InsertPropComma = astObject.Properties.Count > (tr.Hint != null ? 1 : 0);
                }
            }

            bool IsSelfClosingVdomChild(ReadOnlySpan<AstNode> children)
            {
                for (var i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child is AstString str)
                    {
                        return false;
                    }

                    if (IsStringT(child, out var message))
                    {
                        return false;
                    }

                    if (IsCreateElement(child))
                    {
                        if (IsComponentT(((AstCall) child).Args[0]))
                        {
                            return false;
                        }

                        if (children.Length == 1 && !IsSelfClosingVdomChild(((AstCall) child).Args.AsReadOnlySpan(2)))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            bool IsCreateElement(AstNode astNode)
            {
                if (astNode is AstCall call && call.Args.Count >= 2)
                {
                    var c = DetectModuleExportOfCall(call.Expression);
                    return (c != null && c.ModuleName == "bobril" && c.ExportName == "createElement");
                }

                return false;
            }

            void GatherVdomTranslation(ReadOnlySpan<AstNode> children, SourceInfo.VdomTranslation tr,
                ref VdomContext ctx)
            {
                for (var i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child is AstString str)
                    {
                        ctx.SB.Append(str.Value.Replace("\\", "\\\\"));
                        tr.Replacements!.Add(new SourceInfo.Replacement
                        {
                            Type = SourceInfo.ReplacementType.Normal,
                            StartLine = child.Start.Line,
                            StartCol = child.Start.Column,
                            EndLine = NextStartOrEnd(i, in children).Line,
                            EndCol = NextStartOrEnd(i, in children).Col,
                        });
                        continue;
                    }

                    if (IsStringT(child, out var message))
                    {
                        ctx.SB.Append(message);
                        tr.Replacements!.Add(new SourceInfo.Replacement
                        {
                            Type = SourceInfo.ReplacementType.Normal,
                            StartLine = child.Start.Line,
                            StartCol = child.Start.Column,
                            EndLine = NextStartOrEnd(i, in children).Line,
                            EndCol = NextStartOrEnd(i, in children).Col,
                        });
                        continue;
                    }

                    if (IsCreateElement(child) && !IsComponentT(((AstCall) child).Args[0]))
                    {
                        var nestedChildren = ((AstCall) child).Args.AsReadOnlySpan(2);
                        if (children.Length == 1 &&
                            !IsSelfClosingVdomChild(nestedChildren))
                        {
                            Walk(((AstCall) child).Args[1]);
                            GatherVdomTranslation(nestedChildren, tr, ref ctx);
                            continue;
                        }

                        if (!IsSelfClosingVdomChild(nestedChildren))
                        {
                            Walk(((AstCall) child).Args[1]);
                            var idx = ++ctx.ElementCounter;
                            tr.Replacements!.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.Normal,
                                StartLine = ctx.InsertPropLine,
                                StartCol = ctx.InsertPropCol,
                                EndLine = ctx.InsertPropLine,
                                EndCol = ctx.InsertPropCol,
                                Text = (ctx.InsertPropComma ? ", " : "") + idx + ":function(__ch__){return "
                            });
                            ctx.InsertPropComma = true;
                            var (chStartLine, chStartCol, chEndLine, chEndCol) =
                                DetectChildrenStartEndVdom(nestedChildren);
                            tr.Replacements!.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.MoveToPlace,
                                PlaceLine = ctx.InsertPropLine,
                                PlaceCol = ctx.InsertPropCol,
                                StartLine = child.Start.Line,
                                StartCol = child.Start.Column,
                                EndLine = chStartLine,
                                EndCol = chStartCol
                            });
                            tr.Replacements!.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.Normal,
                                StartLine = ctx.InsertPropLine,
                                StartCol = ctx.InsertPropCol,
                                EndLine = ctx.InsertPropLine,
                                EndCol = ctx.InsertPropCol,
                                Text = "__ch__"
                            });
                            tr.Replacements!.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.MoveToPlace,
                                PlaceLine = ctx.InsertPropLine,
                                PlaceCol = ctx.InsertPropCol,
                                StartLine = chEndLine,
                                StartCol = chEndCol,
                                EndLine = child.End.Line,
                                EndCol = child.End.Column
                            });
                            tr.Replacements.Add(new SourceInfo.Replacement
                            {
                                Type = SourceInfo.ReplacementType.Normal,
                                StartLine = ctx.InsertPropLine,
                                StartCol = ctx.InsertPropCol,
                                EndLine = ctx.InsertPropLine,
                                EndCol = ctx.InsertPropCol,
                                Text = "}"
                            });
                            if (i + 1 < children.Length)
                            {
                                tr.Replacements.Add(new SourceInfo.Replacement
                                {
                                    Type = SourceInfo.ReplacementType.Normal,
                                    StartLine = child.End.Line,
                                    StartCol = child.End.Column,
                                    EndLine = NextStartOrEnd(i, in children).Line,
                                    EndCol = NextStartOrEnd(i, in children).Col,
                                });
                            }

                            ctx.SB.Append("{");
                            ctx.SB.Append(idx);
                            ctx.SB.Append("}");
                            GatherVdomTranslation(nestedChildren, tr, ref ctx);
                            ctx.SB.Append("{/");
                            ctx.SB.Append(idx);
                            ctx.SB.Append("}");

                            continue;
                        }
                    }

                    var idx2 = ++ctx.ElementCounter;
                    ctx.SB.Append("{");
                    ctx.SB.Append(idx2);
                    ctx.SB.Append("/}");
                    Walk(child);
                    tr.Replacements!.Add(new SourceInfo.Replacement
                    {
                        Type = SourceInfo.ReplacementType.Normal,
                        StartLine = ctx.InsertPropLine,
                        StartCol = ctx.InsertPropCol,
                        EndLine = ctx.InsertPropLine,
                        EndCol = ctx.InsertPropCol,
                        Text = (ctx.InsertPropComma ? ", " : "") + idx2 + ":function(){return "
                    });
                    ctx.InsertPropComma = true;
                    tr.Replacements!.Add(new SourceInfo.Replacement
                    {
                        Type = SourceInfo.ReplacementType.MoveToPlace,
                        PlaceLine = ctx.InsertPropLine,
                        PlaceCol = ctx.InsertPropCol,
                        StartLine = child.Start.Line,
                        StartCol = child.Start.Column,
                        EndLine = child.End.Line,
                        EndCol = child.End.Column,
                    });
                    tr.Replacements.Add(new SourceInfo.Replacement
                    {
                        Type = SourceInfo.ReplacementType.Normal,
                        StartLine = ctx.InsertPropLine,
                        StartCol = ctx.InsertPropCol,
                        EndLine = ctx.InsertPropLine,
                        EndCol = ctx.InsertPropCol,
                        Text = "}"
                    });
                    if (i + 1 < children.Length)
                    {
                        tr.Replacements.Add(new SourceInfo.Replacement
                        {
                            Type = SourceInfo.ReplacementType.Normal,
                            StartLine = child.End.Line,
                            StartCol = child.End.Column,
                            EndLine = NextStartOrEnd(i, in children).Line,
                            EndCol = NextStartOrEnd(i, in children).Col,
                        });
                    }
                }
            }

            (int StartLine, int StartCol, int EndLine, int EndCol) DetectChildrenStartEndVdom(
                in ReadOnlySpan<AstNode> children)
            {
                if (children.Length == 1)
                {
                    var child = children[0];
                    if (IsCreateElement(child) && !IsComponentT(((AstCall) child).Args[0]))
                    {
                        return DetectChildrenStartEndVdom(((AstCall) child).Args.AsReadOnlySpan(2));
                    }
                }

                return (children[0].Start.Line, children[0].Start.Column, children[^1].End.Line,
                    children[^1].End.Column);
            }

            static (int Line, int Col) NextStartOrEnd<T>(int idx, in ReadOnlySpan<T> children) where T : AstNode
            {
                if (idx == children.Length - 1)
                {
                    return (children[idx].End.Line, children[idx].End.Column);
                }

                return (children[idx + 1].Start.Line, children[idx + 1].Start.Column);
            }

            bool IsStringT(AstNode astNode, [NotNullWhen(true)] out string? str)
            {
                if (astNode is AstCall call && call.Args.Count == 1)
                {
                    var c = DetectModuleExportOfCall(call.Expression);
                    if (c != null && c.ModuleName == "bobril-g11n" && c.ExportName == "t")
                    {
                        if (call.Args[0].ConstValue(_evalCtx) is string param)
                        {
                            str = param;
                            return true;
                        }
                    }
                }

                str = null;
                return false;
            }

            JsModuleExport? DetectModuleExportOfCall(AstNode node)
            {
                try
                {
                    _evalCtx.JustModuleExports = true;
                    return node.ConstValue(_evalCtx) as JsModuleExport;
                }
                finally
                {
                    _evalCtx.JustModuleExports = false;
                }
            }

            bool IsComponentT(AstNode arg)
            {
                var c = DetectModuleExportOfCall(arg);
                return c != null && c.ModuleName == "bobril-g11n" && c.ExportName == "T";
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

using Njsast.Ast;
using Njsast.Output;

namespace Njsast.Scope
{
    public class MangleTreeWalker : TreeWalker
    {
        uint _labelIndex;
        StructList<SymbolDef> _toMangle;
        readonly ScopeOptions _options;
        readonly OutputOptions? _outputOptions;

        public MangleTreeWalker(ScopeOptions options, OutputOptions? outputOptions = null)
        {
            _options = options;
            _outputOptions = outputOptions;
            _toMangle = new StructList<SymbolDef>();
        }

        public void Mangle(AstToplevel topLevel)
        {
            var output = new OutputContext(_outputOptions);
            output.InitializeForFrequencyCounting();
            if (_options.FrequencyCounting) topLevel.Print(output);
            _options.Chars = output.FinishFrequencyCounting();
            Walk(topLevel);
            foreach (var s in _options.Reserved)
            {
                var idx = AstScope.Debase54(_options.Chars, s);
                if (idx >= 0) _options.ReservedOrIdentifier.Add((uint)idx);
            }
            foreach (var s in OutputContext.ReservedWords)
            {
                var idx = AstScope.Debase54(_options.Chars, s);
                if (idx >= 0) _options.ReservedOrIdentifier.Add((uint)idx);
            }
            for (var i = 0u; i < _toMangle.Count; i++)
            {
                _toMangle[i].Mangle(_options);
            }
        }

        protected override void Visit(AstNode node)
        {
            switch (node)
            {
                case AstLabeledStatement _:
                {
                    // _labelIndex is incremented when we get to the AstLabel
                    var saveLabelIndex = _labelIndex;
                    DescendOnce();
                    _labelIndex = saveLabelIndex;
                    break;
                }
                case AstScope scope:
                {
                    if (scope.Variables == null)
                        break;
                    foreach (var def in scope.Variables.Values)
                    {
                        if (_options.Reserved.Contains(def.Name)) continue;
                        _toMangle.Add(def);
                    }

                    break;
                }
                case IMayBeBlockScope blockScope when blockScope.IsBlockScope:
                {
                    if (blockScope.BlockScope?.Variables == null)
                        break;
                    foreach (var def in blockScope.BlockScope.Variables.Values)
                    {
                        if (_options.Reserved.Contains(def.Name)) continue;
                        _toMangle.Add(def);
                    }

                    break;
                }
                case AstLabel label:
                {
                    string name;
                    do name = AstScope.Base54(_options.Chars, _labelIndex++);
                    while (!OutputContext.IsIdentifier(name));
                    label.MangledName = name;
                    StopDescending();
                    break;
                }
                case AstSymbolCatch symbolCatch:
                    if (symbolCatch.Thedef == null)
                        break;
                    _toMangle.Add(symbolCatch.Thedef);
                    break;
            }
        }
    }
}

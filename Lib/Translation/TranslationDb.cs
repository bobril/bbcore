using Lib.DiskCache;
using Lib.ToolsDir;
using Lib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lib.Translation
{
    public class TranslationDb
    {
        public TranslationDb(IFsAbstraction fsAbstraction)
        {
            _fsAbstraction = fsAbstraction;
        }

        Dictionary<TranslationKey, uint> Key2Id = new Dictionary<TranslationKey, uint>();
        List<TranslationKey> Id2Key = new List<TranslationKey>();
        Dictionary<uint, uint> UsedIdMap = new Dictionary<uint, uint>();
        List<uint> UsedIds = new List<uint>();
        Dictionary<string, List<string>> Lang2ValueList = new Dictionary<string, List<string>>();
        readonly IFsAbstraction _fsAbstraction;
        bool _changed;
        Dictionary<string, string> _outputJsCache = new Dictionary<string, string>();

        public void LoadLangDbs(string dir)
        {
            try
            {
                foreach (var info in _fsAbstraction.GetDirectoryContent(dir))
                {
                    if (info.IsDirectory) continue;
                    if (!info.Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)) continue;
                    LoadLangDb(PathUtils.Join(dir, info.Name));
                }
            }
            catch
            {
                // ignore any errors
            }
            _changed = true;
        }

        enum LoaderState
        {
            Start,
            LangString,
            BeforeItem,
            End,
            Message,
            Hint,
            Flags,
            Value,
            AfterValue,
        }

        void LoadLangDb(string fileName)
        {
            var reader = new JsonTextReader(new StringReader(_fsAbstraction.ReadAllUtf8(fileName)));
            var state = LoaderState.Start;
            string lang = null;
            List<string> valueList = null;
            string message = null;
            string hint = null;
            var withParams = false;
            string value = null;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartArray:
                        switch (state)
                        {
                            case LoaderState.Start:
                                state = LoaderState.LangString;
                                break;
                            case LoaderState.BeforeItem:
                                state = LoaderState.Message;
                                break;
                            default:
                                throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                        }
                        break;
                    case JsonToken.String:
                        switch (state)
                        {
                            case LoaderState.LangString:
                                lang = (string)reader.Value;
                                valueList = AddLanguage(lang);
                                state = LoaderState.BeforeItem;
                                break;
                            case LoaderState.Message:
                                message = (string)reader.Value;
                                state = LoaderState.Hint;
                                break;
                            case LoaderState.Hint:
                                hint = (string)reader.Value;
                                state = LoaderState.Flags;
                                break;
                            case LoaderState.Value:
                                value = (string)reader.Value;
                                state = LoaderState.AfterValue;
                                break;
                            default:
                                throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                        }
                        break;
                    case JsonToken.Integer:
                        switch (state)
                        {
                            case LoaderState.Flags:
                                withParams = (((int)((long)reader.Value)) & 1) != 0;
                                state = LoaderState.Value;
                                break;
                            default:
                                throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                        }
                        break;
                    case JsonToken.Null:
                        switch (state)
                        {
                            case LoaderState.Hint:
                                hint = null;
                                state = LoaderState.Flags;
                                break;
                            default:
                                throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                        }
                        break;
                    case JsonToken.EndArray:
                        switch (state)
                        {
                            case LoaderState.BeforeItem:
                                state = LoaderState.End;
                                break;
                            case LoaderState.Value:
                                value = null;
                                goto case LoaderState.AfterValue;
                            case LoaderState.AfterValue:
                                state = LoaderState.BeforeItem;
                                var id = AddToDB(message, hint, withParams);
                                if (value != null)
                                {
                                    while (valueList.Count <= id) valueList.Add(null);
                                    valueList[(int)id] = value;
                                }
                                break;
                            default:
                                throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                        }
                        break;
                    default:
                        throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber + " Pos:" + reader.LinePosition);
                }
            }
            if (state != LoaderState.End)
            {
                throw new Exception("Unexpected end of file when in state " + state);
            }
        }

        public List<string> AddLanguage(string lang)
        {
            if (!Lang2ValueList.TryGetValue(lang, out List<string> valueList))
            {
                valueList = new List<string>();
                Lang2ValueList.Add(lang, valueList);
            }
            return valueList;
        }

        public uint AddToDB(string message, string hint, bool withParams)
        {
            if (hint == "") hint = null;
            var key = new TranslationKey(message, hint, withParams);
            if (!Key2Id.TryGetValue(key, out var id))
            {
                id = (uint)Id2Key.Count;
                Id2Key.Add(key);
                Key2Id.Add(key, id);
            }
            return id;
        }

        public uint MapId(uint id)
        {
            if (UsedIdMap.TryGetValue(id, out var res))
            {
                return res;
            }
            _changed = true;
            res = (uint)UsedIds.Count;
            UsedIds.Add(id);
            UsedIdMap.Add(id, res);
            return res;
        }

        public void BuildTranslationJs(IToolsDir tools, Dictionary<string, object> filesContent)
        {
            if (_changed)
            {
                _outputJsCache.Clear();
                foreach (var p in Lang2ValueList)
                {
                    var langInit = tools.GetLocaleDef(p.Key);
                    if (langInit == null) continue;
                    var sw = new StringWriter();
                    sw.Write(langInit);
                    var jw = new JsonTextWriter(sw);
                    jw.WriteStartArray();
                    for (var i = 0; i < UsedIds.Count; i++)
                    {
                        var idx = (int)UsedIds[i];
                        jw.WriteValue((idx < p.Value.Count) ? p.Value[idx] : Id2Key[idx].Message);
                    }
                    jw.WriteEndArray();
                    sw.Write(")");
                    _outputJsCache[p.Key.ToLowerInvariant() + ".js"] = sw.ToString();
                }
                _changed = false;
            }
            foreach (var i in _outputJsCache)
            {
                filesContent[i.Key] = i.Value;
            }
        }
    }
}

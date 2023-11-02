using Lib.DiskCache;
using Lib.ToolsDir;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BTDB.Collections;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;
using Njsast.SourceMap;

namespace Lib.Translation;

public class TranslationDb
{
    public TranslationDb(IFsAbstraction fsAbstraction, ILogger logger)
    {
        _fsAbstraction = fsAbstraction;
        _logger = logger;
    }

    Dictionary<TranslationKey, uint> Key2Id = new Dictionary<TranslationKey, uint>();
    List<TranslationKey> Id2Key = new List<TranslationKey>();
    Dictionary<uint, uint> UsedIdMap = new Dictionary<uint, uint>();
    List<uint> UsedIds = new List<uint>();
    List<List<string>> Locations = new List<List<string>>();
    Dictionary<string, List<string>> Lang2ValueList = new Dictionary<string, List<string>>();
    HashSet<string> _loadedLanguages = new HashSet<string>();
    readonly IFsAbstraction _fsAbstraction;
    bool _changed;
    Dictionary<string, string> _outputJsCache = new Dictionary<string, string>();

    ILogger _logger;

    public void LoadLangDbs(string dir)
    {
        try
        {
            foreach (var info in _fsAbstraction.GetDirectoryContent(dir))
            {
                if (info.IsDirectory) continue;
                if (!info.Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (info.Name.StartsWith("package.", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (info.Name.StartsWith("tsconfig.", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (info.Name.StartsWith("locations.", StringComparison.InvariantCultureIgnoreCase)) continue;
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

    public void LoadLangDb(string fileName)
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
                            throw new Exception("Unexpected token " + reader.TokenType + " Line:" +
                                                reader.LineNumber + " Pos:" + reader.LinePosition);
                    }

                    break;
                case JsonToken.String:
                    switch (state)
                    {
                        case LoaderState.LangString:
                            lang = (string)reader.Value;
                            _loadedLanguages.Add(lang);
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
                            throw new Exception("Unexpected token " + reader.TokenType + " Line:" +
                                                reader.LineNumber + " Pos:" + reader.LinePosition);
                    }

                    break;
                case JsonToken.Integer:
                    switch (state)
                    {
                        case LoaderState.Flags:
                            withParams = ((int)(long)reader.Value & 1) != 0;
                            state = LoaderState.Value;
                            break;
                        default:
                            throw new Exception("Unexpected token " + reader.TokenType + " Line:" +
                                                reader.LineNumber + " Pos:" + reader.LinePosition);
                    }

                    break;
                case JsonToken.Null:
                    switch (state)
                    {
                        case LoaderState.Hint:
                            hint = null;
                            state = LoaderState.Flags;
                            break;
                        case LoaderState.Value:
                            value = null;
                            state = LoaderState.AfterValue;
                            break;
                        default:
                            throw new Exception("Unexpected token " + reader.TokenType + " Line:" +
                                                reader.LineNumber + " Pos:" + reader.LinePosition);
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
                            while (valueList.Count <= id) valueList.Add(null);
                            if (value != null)
                            {
                                valueList[(int)id] = value;
                            }

                            break;
                        default:
                            throw new Exception("Unexpected token " + reader.TokenType + " Line:" +
                                                reader.LineNumber + " Pos:" + reader.LinePosition);
                    }

                    break;
                default:
                    throw new Exception("Unexpected token " + reader.TokenType + " Line:" + reader.LineNumber +
                                        " Pos:" + reader.LinePosition);
            }
        }

        if (state != LoaderState.End)
        {
            throw new Exception("Unexpected end of file when in state " + state);
        }
    }

    public void SaveLangDbs(string dir, bool justUsed)
    {
        foreach (var p in Lang2ValueList)
        {
            if (!_loadedLanguages.Contains(p.Key))
                continue;
            SaveLangDb(dir, p.Key, justUsed);
        }
    }

    public void SaveLocations(string dir, string projectRoot)
    {
        using var stream = new MemoryStream();
        using var jw = CreateJsonWriter(stream);
        jw.WriteStartArray();
        for (var i = 0; i < UsedIds.Count; i++)
        {
            jw.WriteStartArray();
            var idx = (int) UsedIds[i];
            jw.WriteStringValue(Id2Key[idx].Message);
            jw.WriteStringValue(Id2Key[idx].Hint);
            jw.WriteNumberValue(Id2Key[idx].WithParams ? 1 : 0);
            foreach (var s in Locations[i])
            {
                var pathLen = s.LastIndexOf(':', s.LastIndexOf(':') - 1);
                var p = PathUtils.Subtract(s[..pathLen], projectRoot);
                jw.WriteStringValue(p + s[pathLen..]);
            }

            jw.WriteEndArray();
        }

        jw.WriteEndArray();
        jw.Flush();
        _fsAbstraction.WriteAllBytes(PathUtils.Join(dir, "locations.json"), stream.ToArray());
    }

    public void SaveLangDb(string dir, string lang, bool justUsed)
    {
        var values = Lang2ValueList[lang];

        using var stream = File.Create(PathUtils.Join(dir, lang + ".json"));
        using var jw = CreateJsonWriter(stream);
        jw.WriteStartArray();
        jw.WriteStringValue(lang);
        if (justUsed)
        {
            for (var i = 0; i < UsedIds.Count; i++)
            {
                jw.WriteStartArray();
                var idx = (int)UsedIds[i];
                jw.WriteStringValue(Id2Key[idx].Message);
                jw.WriteStringValue(Id2Key[idx].Hint);
                jw.WriteNumberValue(Id2Key[idx].WithParams ? 1 : 0);
                if (idx < values.Count && values[idx] != null)
                    jw.WriteStringValue(values[idx]);
                jw.WriteEndArray();
            }
        }
        else
        {
            for (var idx = 0; idx < Id2Key.Count; idx++)
            {
                jw.WriteStartArray();
                jw.WriteStringValue(Id2Key[idx].Message);
                jw.WriteStringValue(Id2Key[idx].Hint);
                jw.WriteNumberValue(Id2Key[idx].WithParams ? 1 : 0);
                if (idx < values.Count && values[idx] != null)
                    jw.WriteStringValue(values[idx]);
                jw.WriteEndArray();
            }
        }

        jw.WriteEndArray();
        jw.Flush();
    }

    static Utf8JsonWriter CreateJsonWriter(Stream stream)
    {
        return new Utf8JsonWriter(stream, new()
        {
            Indented = true,
            SkipValidation = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public bool HasLanguage(string lang) => Lang2ValueList.ContainsKey(lang);

    public List<string> AddLanguage(string lang)
    {
        if (!Lang2ValueList.TryGetValue(lang, out List<string> valueList))
        {
            valueList = new List<string>();
            Lang2ValueList.Add(lang, valueList);
        }

        return valueList;
    }

    public bool ExportLanguages(string filePath, bool exportOnlyUntranslated = true,
        string? specificLanguage = null, string? specificPath = null)
    {
        if (!string.IsNullOrEmpty(specificPath))
            specificLanguage = GetLanguageFromSpecificPath(specificPath);

        var contentBuilder = new StringBuilder();
        List<string>? values = null;

        if (!string.IsNullOrEmpty(specificLanguage))
            values = Lang2ValueList[specificLanguage];

        for (var id = 0; id < Id2Key.Count; id++)
        {
            var trs = Id2Key[id];
            if (string.IsNullOrEmpty(specificLanguage) && string.IsNullOrEmpty(specificPath))
            {
                foreach (var language in _loadedLanguages)
                {
                    values = Lang2ValueList[language];
                    if (exportOnlyUntranslated && values[id] != null)
                        continue;

                    contentBuilder.Append(ExportLanguageItem(trs.Message, trs.Hint));
                    break;
                }
            }
            else
            {
                if (exportOnlyUntranslated && values[id] != null)
                    continue;
                contentBuilder.Append(ExportLanguageItem(trs.Message, trs.Hint));
            }
        }

        if (contentBuilder.Length > 0)
        {
            _fsAbstraction.WriteAllUtf8(filePath, contentBuilder.ToString());
            return true;
        }

        return false;
    }

    string GetLanguageFromSpecificPath(string specificPath)
    {
        var content = _fsAbstraction.ReadAllUtf8(specificPath);
        return JArray.Parse(content).First.ToString();
    }

    string ExportLanguageItem(string source, string? hint)
    {
        var content = string.Empty;
        var stringifyHint = hint;
        if (stringifyHint != null)
        {
            stringifyHint = JsonConvert.SerializeObject(hint);
            stringifyHint = stringifyHint.Substring(1, stringifyHint.Length - 2).Replace(@"\\n", @"\n");
        }

        var stringifySource = JsonConvert.SerializeObject(source);
        stringifySource = stringifySource.Substring(1, stringifySource.Length - 2).Replace(@"\\n", @"\n");

        content += $"S:{stringifySource}\n";
        content += $"I:{stringifyHint}\n";
        content += $"T:{stringifySource}\n";
        return content;
    }

    public uint AddToDB(string message, string? hint, bool withParams)
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

    public uint MapId(uint id, SourceCodePosition sourceCodePosition)
    {
        if (UsedIdMap.TryGetValue(id, out var res))
        {
            Locations[(int)res].Add(sourceCodePosition.ToString());
            return res;
        }

        _changed = true;
        res = (uint)UsedIds.Count;
        UsedIds.Add(id);
        Locations.Add(new() { sourceCodePosition.ToString() });
        UsedIdMap.Add(id, res);
        return res;
    }

    public void BuildTranslationJs(IToolsDir tools, RefDictionary<string, object> filesContent, string versionDir)
    {
        if (_changed)
        {
            _outputJsCache.Clear();
            foreach (var p in Lang2ValueList)
            {
                var langInit = tools.GetLocaleDef(p.Key);
                if (langInit == null) continue;
                var sw = new StringWriter();
                var posLoc1 = langInit.IndexOf("bobrilRegisterTranslations(", StringComparison.Ordinal) +
                              "bobrilRegisterTranslations(".Length;
                var posLoc2 = langInit.IndexOf(",", posLoc1, StringComparison.Ordinal);
                langInit = langInit.Substring(0, posLoc1) + "\'" + p.Key + "\'" + langInit.Substring(posLoc2);
                sw.Write(langInit);
                var jw = new JsonTextWriter(sw);
                jw.WriteStartArray();
                for (var i = 0; i < UsedIds.Count; i++)
                {
                    var idx = (int)UsedIds[i];
                    jw.WriteValue(((idx < p.Value.Count) ? p.Value[idx] : null) ?? Id2Key[idx].Message);
                }

                jw.WriteEndArray();
                sw.Write(")");
                _outputJsCache[p.Key.ToLowerInvariant() + ".js"] = sw.ToString();
            }

            // scope
            {
                var sw = new StringWriter();
                sw.Write("bobrilRegisterTranslations(\"\",[],");
                var jw = new JsonTextWriter(sw);
                jw.WriteStartArray();
                for (var i = 0; i < UsedIds.Count; i++)
                {
                    var idx = (int)UsedIds[i];
                    var key = Id2Key[idx];
                    var val = key.Message + "\x9" + (key.WithParams ? "1" : "0") + (key.Hint ?? "");
                    jw.WriteValue(val);
                }

                jw.WriteEndArray();
                sw.Write(")");
                _outputJsCache["l10nkeys.js"] = sw.ToString();
            }
            _changed = false;
        }

        foreach (var i in _outputJsCache)
        {
            var outfn = i.Key;
            if (versionDir != null)
                outfn = versionDir + "/" + outfn;
            filesContent.GetOrAddValueRef(outfn) = i.Value;
        }
    }

    MessageParser _messageParser = new MessageParser();

    public ErrorAst? CheckMessage(string message, List<string> knownParams)
    {
        var res = _messageParser.Parse(message);
        if (res is ErrorAst)
        {
            return (ErrorAst)res;
        }

        if (knownParams is { Count: > 0 })
        {
            var p = new HashSet<string>();
            res.GatherParams(p);
            if (!p.SetEquals(knownParams))
            {
                return new ErrorAst(
                    "Parameters does not match [" + string.Join(", ", p) + "] != [" +
                    string.Join(", ", knownParams) + "]", 0, 0, 0);
            }
        }

        return null;
    }

    public IEnumerable<string> GetLanguages() => _loadedLanguages;

    public bool ImportTranslatedLanguage(string pathFrom, string? pathTo = null)
    {
        var normalizedPath = PathUtils.Normalize(pathFrom);
        var language = Path.GetFileNameWithoutExtension(normalizedPath);
        if (pathTo != null)
        {
            normalizedPath = PathUtils.Normalize(pathTo);
            language = Path.GetFileNameWithoutExtension(normalizedPath);
        }

        try
        {
            if (!HasLanguage(language))
                throw new Exception($"Language {language} does not exist. Probably file name is not valid.");

            ImportTranslatedLanguageInternal(pathFrom, (source, hint, target) =>
            {
                var key = new TranslationKey(source, hint, true);
                if (Key2Id.TryGetValue(key, out var idt))
                {
                    var msg = _messageParser.Parse(target);
                    if (msg is ErrorAst errorMsg)
                    {
                        _logger?.Error("Skipping wrong translation entry:");
                        _logger?.Warn($"S: {source}");
                        _logger?.Warn($"I: {hint}");
                        _logger?.Warn($"T: {target}");
                        _logger?.Error($"Error in g11n format: {errorMsg.Message}");
                    }
                    else
                    {
                        var values = Lang2ValueList[language];
                        while (values.Count < idt) values.Add(null);
                        values[(int)idt] = target;
                    }
                }
                else
                {
                    key = new TranslationKey(source, hint, false);
                    if (Key2Id.TryGetValue(key, out var idf))
                    {
                        var values = Lang2ValueList[language];
                        while (values.Count < idf) values.Add(null);
                        values[(int)idf] = target;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.Error(ex.Message);
            return false;
        }

        return true;
    }

    void ImportTranslatedLanguageInternal(string pathFrom, Action<string, string?, string> action)
    {
        var content = _fsAbstraction.ReadAllUtf8(pathFrom);
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i += 3)
        {
            if (lines[i][0] != 'S' || lines[i][1] != ':')
                throw new Exception("Invalid file format (Line expected to start with S:). (" + lines[i] + ")");
            if (lines[i + 1][0] != 'I' || lines[i + 1][1] != ':')
                throw new Exception("Invalid file format (Line expected to start with I:). (" + lines[i + 1] + ")");
            if (lines[i + 2][0] != 'T' || lines[i + 2][1] != ':')
                throw new Exception("Invalid file format (Line expected to start with T:). (" + lines[i + 2] + ")");

            var source = lines[i].Substring(2);
            var hint = lines[i + 1].Substring(2);
            if (hint == "") hint = null;
            var target = lines[i + 2].Substring(2);

            source = Sanitize(source);
            hint = Sanitize(hint);
            target = Sanitize(target);

            action(source, hint, target);
        }
    }


    [return: NotNullIfNotNull("input")]
    static string? Sanitize(string? input) =>
        input?.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");

    public string GetLanguageFromSpecificFile(string specificPath)
    {
        var content = _fsAbstraction.ReadAllUtf8(specificPath);
        var parsed = JArray.Parse(content);
        return parsed.First!.ToString();
    }

    public bool UnionExportedLanguage(IList<string> files)
    {
        var keys = new HashSet<TranslationKey>();

        void AddData(string source, string hint, string target) =>
            keys.Add(new TranslationKey(source, hint, false));

        var outputFile = files.Last();
        var langFiles = files.SkipLast(1);

        foreach (var file in langFiles)
        {
            ImportTranslatedLanguageInternal(file, AddData);
        }

        var strBuilder = new StringBuilder();
        foreach (var key in keys)
        {
            var content = ExportLanguageItem(key.Message, key.Hint);
            strBuilder.Append(content);
        }

        if (strBuilder.Length == 0)
        {
            _logger?.Warn("Nothing to union.");
            return false;
        }

        try
        {
            _fsAbstraction.WriteAllUtf8(outputFile, strBuilder.ToString());
        }
        catch (Exception e)
        {
            _logger?.Error(e.Message);
            return false;
        }

        return true;
    }

    public bool SubtractExportedLanguage(string file1, string file2, string outputFile)
    {
        var keys = new HashSet<TranslationKey>();

        ImportTranslatedLanguageInternal(file1,
            (source, hint, target) => keys.Add(new TranslationKey(source, hint, false)));
        ImportTranslatedLanguageInternal(file2, (source, hint, target) =>
        {
            var key = new TranslationKey(source, hint, false);
            if (keys.Contains(key))
                keys.Remove(key);
        });

        var strBuilder = new StringBuilder();
        foreach (var key in keys)
        {
            var content = ExportLanguageItem(key.Message, key.Hint);
            strBuilder.Append(content);
        }

        if (strBuilder.Length == 0)
        {
            _logger?.Warn("The result is empty set. Nothing saved.");
            return false;
        }

        try
        {
            _fsAbstraction.WriteAllUtf8(outputFile, strBuilder.ToString());
        }
        catch (Exception e)
        {
            _logger?.Error(e.Message);
            return false;
        }

        return true;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Version = SemVer.Version;

namespace Lib.Registry
{
    public class PackageInfo
    {
        readonly string _content;
        Dictionary<string, Version> _distTags;

        public PackageInfo(string content)
        {
            _content = content;
        }

        enum State
        {
            Start,
            Main,
            Versions
        }

        public Dictionary<string, Version> DistTags()
        {
            if (_distTags == null)
            {
                _distTags = new Dictionary<string, Version>();
                foreach (var tuple in ParseDistTags())
                {
                    _distTags[tuple.Item1] = tuple.Item2;
                }
            }

            return _distTags;
        }

        public IEnumerable<(string, Version)> ParseDistTags()
        {
            var reader = new JsonTextReader(new StringReader(_content));
            var state = State.Start;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.None:
                        break;
                    case JsonToken.StartObject:
                        if (state == State.Start)
                        {
                            state = State.Main;
                            break;
                        }
                        else
                        {
                            throw new Exception("Wrong format");
                        }
                    case JsonToken.StartArray:
                        break;
                    case JsonToken.PropertyName:
                        if (state == State.Main)
                        {
                            if ((string) reader.Value == "dist-tags")
                            {
                                if (!reader.Read())
                                    throw new Exception("Wrong format");
                                state = State.Versions;
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }
                        else if (state == State.Versions)
                        {
                            var tag = (string) reader.Value;
                            reader.Read();
                            var ver = new Version((string) reader.Value, true);
                            yield return (tag, ver);
                        }

                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.Raw:
                        break;
                    case JsonToken.Integer:
                        break;
                    case JsonToken.Float:
                        break;
                    case JsonToken.String:
                        break;
                    case JsonToken.Boolean:
                        break;
                    case JsonToken.Null:
                        break;
                    case JsonToken.Undefined:
                        break;
                    case JsonToken.EndObject:
                        if (state == State.Versions)
                        {
                            state = State.Main;
                        }
                        else if (state == State.Main)
                        {
                            yield break;
                        }

                        break;
                    case JsonToken.EndArray:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void LazyParseVersions(Func<Version, bool> shouldReadVersion, Action<JsonReader> versionContent)
        {
            var reader = new JsonTextReader(new StringReader(_content));
            var state = State.Start;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.None:
                        break;
                    case JsonToken.StartObject:
                        if (state == State.Start)
                        {
                            state = State.Main;
                            break;
                        }
                        else
                        {
                            throw new Exception("Wrong format");
                        }
                    case JsonToken.StartArray:
                        break;
                    case JsonToken.PropertyName:
                        if (state == State.Main)
                        {
                            if ((string) reader.Value == "versions")
                            {
                                if (!reader.Read())
                                    throw new Exception("Wrong format");
                                state = State.Versions;
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }
                        else if (state == State.Versions)
                        {
                            var ver = new Version((string) reader.Value, true);
                            if (shouldReadVersion(ver))
                            {
                                reader.Read();
                                versionContent(reader);
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }

                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.Raw:
                        break;
                    case JsonToken.Integer:
                        break;
                    case JsonToken.Float:
                        break;
                    case JsonToken.String:
                        break;
                    case JsonToken.Boolean:
                        break;
                    case JsonToken.Null:
                        break;
                    case JsonToken.Undefined:
                        break;
                    case JsonToken.EndObject:
                        if (state == State.Versions)
                        {
                            state = State.Main;
                        }
                        else if (state == State.Main)
                        {
                            return;
                        }

                        break;
                    case JsonToken.EndArray:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}

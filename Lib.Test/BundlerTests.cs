using Lib.Utils;
using System;
using Xunit;
using System.Collections.Generic;
using Lib.Bundler;
using Njsast.Bundler;
using BundlerImpl = Lib.Bundler.BundlerImpl;

namespace Lib.Test
{
    [CollectionDefinition("Serial", DisableParallelization = true)]
    public class BundlerTests
    {
        string _bbdir;
        ToolsDir.ToolsDir _tools;

        public BundlerTests()
        {
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.CurrentDirectory), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"), new DummyLogger());
        }

        public class FakeCallbacks : IBundlerCallback
        {
            readonly BundlerTests _owner;
            public readonly Dictionary<string, string> Result = new Dictionary<string, string>();

            public FakeCallbacks(BundlerTests owner)
            {
                _owner = owner;
            }

            public string GenerateBundleName(string forName)
            {
                return "bundle.js";
            }

            public string ReadContent(string name)
            {
                if (name=="index.js")
                {
                    return "var lib=require(\"lib\"); lib.hello();";
                }
                else
                {
                    return "function hello() { console.log(\"Hello\"); } exports.hello = hello;";
                }
            }

            public string ResolveRequire(string name, string from)
            {
                return "lib.js";
            }

            public string TslibSource(bool withImport)
            {
                return BundlerHelpers.JsHeaders(withImport);
            }

            public void WriteBundle(string name, string content)
            {
                Result[name] = content;
            }

            public IList<string> GetPlainJsDependencies(string name)
            {
                return new string[0];
            }
        }

        [Fact]
        public void BasicTest()
        {
            var bundler = new BundlerImpl(_tools);
            bundler.MainFiles = new List<string> { "index.js" };
            bundler.Defines = new Dictionary<string, object> { { "DEBUG", false } };
            var callbacks = new FakeCallbacks(this);
            bundler.Callbacks = callbacks;
            bundler.Bundle();
            Assert.Equal("!function(o){\"use strict\";function n(){console.log(\"Hello\")}n()}();", callbacks.Result["bundle.js"]);
        }
    }
}

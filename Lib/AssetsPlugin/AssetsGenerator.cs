using Lib.DiskCache;
using Lib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lib.AssetsPlugin
{
    public class AssetsGenerator
    {
        const string AssetsFileName = "assets.ts";
        const string SpritesFileName = "sprites.ts";
        const string AssetsDirName = "assets";
        const string SrcDirName = "src";
        readonly IDiskCache _cache;

        public AssetsGenerator(IDiskCache cache)
        {
            _cache = cache;
        }

        public bool Run(string projectDir, bool generateSpritesFile)
        {
            var projDir = _cache.TryGetItem(projectDir) as IDirectoryCache;
            if (projDir == null) return false;
            _cache.UpdateIfNeeded(projDir);
            if (!(projDir.TryGetChild(AssetsDirName) is IDirectoryCache assetsDir)) return false;
            var srcPath = PathUtils.Join(projectDir, SrcDirName);

            var assets = InspectAssets(assetsDir, srcPath);
            var assetsContentBuilder = new AssetsContentBuilder();
            assetsContentBuilder.Build(assets);
            var changed = WriteContent(srcPath, AssetsFileName, assetsContentBuilder.Content, projectDir);

            if (generateSpritesFile)
            {
                var spritesContentBuilder = new SpritesContentBuilder();
                spritesContentBuilder.Build(assets);
                changed |= WriteContent(srcPath, SpritesFileName, spritesContentBuilder.Content, projectDir);
            }
            return changed;
        }

        IDictionary<string, object> InspectAssets(IDirectoryCache rootDir, string srcPath)
        {
            _cache.UpdateIfNeeded(rootDir);
            var assetsMap = new Dictionary<string, object>();
            var assetsFiles = rootDir.ToList();
            foreach (var assetFile in assetsFiles)
            {
                if (assetFile is IDirectoryCache dir)
                {
                    assetsMap[SanitizeKey(dir.Name)] = InspectAssets(dir, srcPath);
                }
                else
                {
                    assetsMap[SanitizeKey(PathUtils.ExtractQuality(assetFile.Name).Name)] = PathUtils.Subtract(PathUtils.ExtractQuality(assetFile.FullPath).Name, srcPath);
                }
            }
            return assetsMap;
        }

        static string SanitizeKey(string key)
        {
            return key.Replace('.', '_').Replace('-', '_').Replace(' ', '_');
        }

        bool WriteContent(string srcPath, string fileName, string content, string projectDir)
        {
            var filePath = PathUtils.Join(srcPath, fileName);
            if (_cache.TryGetItem(filePath) is IFileCache file && file.Utf8Content == content)
                return false;
            Console.WriteLine("AssetGenerator updating " + PathUtils.Subtract(filePath, projectDir));
            Directory.CreateDirectory(srcPath);
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
            return true;
        }
    }
}

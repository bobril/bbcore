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
        const string _assetsFileName = "assets.ts";
        const string _spritesFileName = "sprites.ts";
        const string _assetsDirName = "assets";
        const string _srcDirName = "src";
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
            var assetsDir = projDir.TryGetChild(_assetsDirName) as IDirectoryCache;
            if (assetsDir == null) return false;
            var srcPath = PathUtils.Join(projectDir, _srcDirName);

            var assets = InspectAssets(assetsDir, srcPath);
            var assetsContentBuilder = new AssetsContentBuilder();
            assetsContentBuilder.Build(assets);
            var changed = WriteContent(srcPath, _assetsFileName, assetsContentBuilder.Content, projectDir);

            if (generateSpritesFile)
            {
                var spritesContentBuilder = new SpritesContentBuilder();
                spritesContentBuilder.Build(assets);
                changed |= WriteContent(srcPath, _spritesFileName, spritesContentBuilder.Content, projectDir);
            }
            return changed;
        }

        IDictionary<string, object> InspectAssets(IDirectoryCache rootDir, string srcPath)
        {
            _cache.UpdateIfNeeded(rootDir);
            var assetsMap = new Dictionary<string, object>();
            var assetsFiles = rootDir.ToList();
            for (var j = 0; j < assetsFiles.Count; j++)
            {
                var assetFile = assetsFiles[j];
                if (assetFile is IDirectoryCache)
                {
                    assetsMap[SanitizeKey(assetFile.Name)] = InspectAssets(assetFile as IDirectoryCache, srcPath);
                }
                else
                {
                    assetsMap[SanitizeKey(PathUtils.ExtractQuality(assetFile.Name).Name)] = PathUtils.Subtract(PathUtils.ExtractQuality(assetFile.FullPath).Name, srcPath);
                }
            };
            return assetsMap;
        }

        string SanitizeKey(string key)
        {
            return key.Replace('.', '_').Replace('-', '_').Replace(' ', '_');
        }

        bool WriteContent(string srcPath, string fileName, string content, string projectDir)
        {
            var filePath = PathUtils.Join(srcPath, fileName);
            var file = _cache.TryGetItem(filePath) as IFileCache;
            if (file != null && file.Utf8Content == content)
                return false;
            Console.WriteLine("AssetGenerator updating " + PathUtils.Subtract(filePath, projectDir));
            Directory.CreateDirectory(srcPath);
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
            return true;
        }
    }
}

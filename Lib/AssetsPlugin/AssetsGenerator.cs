using Lib.DiskCache;
using Lib.Utils;
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

        public void Run(string projectDir, bool generateSpritesFile)
        {
            var projDir = _cache.TryGetItem(projectDir) as IDirectoryCache;
            if (projDir == null) return;
            var assetsDir = projDir.TryGetChildNoVirtual(_assetsDirName) as IDirectoryCache;
            if (assetsDir == null) return;
            var srcPath = PathUtils.Join(projectDir, _srcDirName);

            var assets = InspectAssets(assetsDir, srcPath);
            var assetsContentBuilder = new AssetsContentBuilder();
            assetsContentBuilder.Build(assets);
            WriteContent(srcPath, _assetsFileName, assetsContentBuilder.Content);

            if (generateSpritesFile)
            {
                var spritesContentBuilder = new SpritesContentBuilder();
                spritesContentBuilder.Build(assets);
                WriteContent(srcPath, _spritesFileName, spritesContentBuilder.Content);
            }
        }

        IDictionary<string, object> InspectAssets(IDirectoryCache rootDir, string srcPath)
        {
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
                    assetsMap[SanitizeKey(assetFile.Name)] = PathUtils.Subtract(assetFile.FullPath, srcPath);
                }
            };
            return assetsMap;
        }

        string SanitizeKey(string key)
        {
            return key.Replace('.', '_').Replace('-', '_').Replace(' ', '_');
        }

        void WriteContent(string srcPath, string fileName, string content)
        {
            var filePath = PathUtils.Join(srcPath, fileName);
            var file = _cache.TryGetItem(filePath) as IFileCache;
            if (file != null && file.Utf8Content == content)
                return;
            Directory.CreateDirectory(srcPath);
            File.WriteAllText(filePath, content, new UTF8Encoding(false));
        }
    }
}

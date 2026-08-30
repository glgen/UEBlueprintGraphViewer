using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.Encryption.Aes;
using System.Threading.Tasks;
using UEBlueprintGraphViewer.Assets;
using CUE4Parse.Compression;
using System;
using CUE4Parse.MappingsProvider.Jmap;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Pak.Objects;

namespace UEBlueprintGraphViewer
{
    public class PackageData : IDisposable
    {
        public List<GameFile> Assets;
        public DefaultFileProvider Provider;

        public PackageData(GameSettings game)
        {
            Provider = new DefaultFileProvider(game.PaksFolder, SearchOption.AllDirectories, new VersionContainer(game.UEVersion));
            Provider.MappingsContainer = new JmapTypeMappingsProvider(game.ObjectDump);
            Provider.Initialize();
            Provider.SubmitKey(new FGuid(), new FAesKey(game.EncryptionKey));
            Provider.TryChangeCulture("en");
            Provider.PostMount();
            Provider.LoadVirtualPaths();
            Provider.ReadScriptData = true;
            Provider.SkipReferencedTextures = true;

            OodleHelper.Initialize(Path.Combine(Directory.GetCurrentDirectory(), OodleHelper.OodleFileName));

            Assets = Provider.Files.Values.ToList();
        }

        public void Dispose()
        {
            Assets.Clear();
            Provider.Dispose();
        }

        public static Task<PackageData> LoadPackageAsync(GameSettings game)
        {
            return Task.Run(() => new PackageData(game) );
        }

        public Task<Asset> LoadAsset(string path, string name)
        {
            return Task.Run(() => new Asset(Provider.LoadPackage(path), name));
        }
        
        public Task<IPackage> LoadPackage(string path)
        {
            return Task.Run(() => Provider.LoadPackage(path));
        }
        public Task<IPackage> LoadImportsOnlyPackageAsync(GameFile file)
        {
            return Task.Run(() => LoadImportsOnlyPackage(file));
        }
        
        public virtual IPackage LoadImportsOnlyPackage(GameFile file)
        {
            if (!file.IsUePackage) throw new ArgumentException("cannot load non-UE package", nameof(file));
            var uasset = file.CreateReader();
            if (file is FPakEntry or OsGameFile)
                return new ImportsOnlyPackage(uasset, Provider);
            throw new NotImplementedException($"type {file.GetType()} is not supported");
        }
        
        public async Task<Asset> LoadAssetAndCheck(string path, string name)
        {
            Asset asset = await LoadAsset(path, name);
            if (!asset.IsBP) throw new AssetIsNotBlueprintException();
            return asset;
        }
    }
}

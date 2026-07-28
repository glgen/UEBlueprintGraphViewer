using System;
using System.IO;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;

namespace UEBlueprintGraphViewer.Assets;

// package class based on https://github.com/FabianFG/CUE4Parse/blob/4fb7435973fc57bfb78577c971d776f7577440cf/CUE4Parse/UE4/Assets/Package.cs
// but only load names and imports

[SkipObjectRegistration]
public sealed class ImportsOnlyPackage : AbstractUePackage
{
    public override FPackageFileSummary Summary { get; }
    public override FNameEntrySerialized[] NameMap { get; }
    public override int ImportMapLength => ImportMap.Length;
    public override int ExportMapLength => 0;

    public FObjectImport[] ImportMap { get; }
    
    public ImportsOnlyPackage(
        FArchive uasset,
        IFileProvider? provider = null)
        : base(uasset.Name.SubstringBeforeLast('.'), provider)
    {
        // We clone the version container because it can be modified with package specific versions when reading the summary
        uasset.Versions = (VersionContainer) uasset.Versions.Clone();

        FAssetArchive uassetAr = new FAssetArchive(uasset, this);

        Summary = new FPackageFileSummary(uassetAr);

        uassetAr.SeekAbsolute(Summary.NameOffset, SeekOrigin.Begin);
        NameMap = new FNameEntrySerialized[Summary.NameCount];
        uassetAr.ReadArray(NameMap, () => new FNameEntrySerialized(uassetAr));

        uassetAr.SeekAbsolute(Summary.ImportOffset, SeekOrigin.Begin);
        ImportMap = new FObjectImport[Summary.ImportCount];
        uassetAr.ReadArray(ImportMap, () => new FObjectImport(uassetAr));
    }
    
    public override int GetExportIndex(string name, StringComparison comparisonType = StringComparison.Ordinal)
    {
        throw new NotImplementedException();
    }

    public override ResolvedObject? ResolvePackageIndex(FPackageIndex? index)
    {
        throw new NotImplementedException();
    }
}
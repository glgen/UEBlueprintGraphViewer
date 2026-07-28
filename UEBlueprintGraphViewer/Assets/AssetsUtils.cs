using System;
using CUE4Parse.Utils;
using UEBlueprintGraphViewer.Engine;

namespace UEBlueprintGraphViewer.Assets;

public class AssetsUtils
{
    public static string FixAssetPath(string path)
    {
        // cursed redirections
        if (path.Starts("Engine/Content/"))
        {
            path = "/Engine/" + path[15..];
        }
        else if (path.Starts("Engine/Plugins/Marketplace/"))
        {
            path = path[26..].Replace("/Content/", "/", StringComparison.Ordinal);
        }
        else if (path.Contains("/Plugins/", StringComparison.Ordinal))
        {
            path = path.SubstringAfter("/Plugins").Replace("/Content/", "/", StringComparison.Ordinal);
        }
        else if (path.Contains("/Content/", StringComparison.Ordinal))
        {
            path = "/Game/" + path.SubstringAfter("/Content/");
        }

        return path;
    }
}
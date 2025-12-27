using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Localization;
using Windows.ApplicationModel.Resources.Core;

namespace Jellyfin.Helpers.Localization;

internal class ResourceManagerStringLocalizerFactory : IStringLocalizerFactory
{
    private static ResourceManager _resourceManager = ResourceManager.Current;

    public IStringLocalizer Create(Type resourceSource)
    {
        return new UwpStringLocalizer(_resourceManager.MainResourceMap.GetSubtree("Translations"));
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return new UwpStringLocalizer(_resourceManager.MainResourceMap.GetSubtree("Translations"));
    }

    public static IEnumerable<string> GetCultures()
    {
        return _resourceManager.MainResourceMap.GetSubtree("Translations").Values.First().ResolveAll().SelectMany(rc => rc.Qualifiers)
            .Where(q => q.QualifierName == "Language")
            .Select(q => q.QualifierValue)
            .Distinct();
    }
}

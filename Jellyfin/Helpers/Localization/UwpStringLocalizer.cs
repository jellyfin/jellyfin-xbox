using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Windows.ApplicationModel.Resources.Core;

namespace Jellyfin.Helpers.Localization;

internal class UwpStringLocalizer : IStringLocalizer
{
    private readonly ResourceMap _resourceMap;

    public UwpStringLocalizer(ResourceMap resourceMap)
    {
        _resourceMap = resourceMap;
    }

    public LocalizedString this[string name]
    {
        get
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            if (!_resourceMap.TryGetValue(name.Replace(".", "/"), out var resourceValue))
            {
                return new LocalizedString(name, name, true);
            }

            ResourceCandidate defaultCandidate = null;
            foreach (var item in resourceValue.ResolveAll())
            {
                if (item.IsDefault)
                {
                    defaultCandidate = item;
                }

                var quantifiers = item.Qualifiers;
                foreach (var quantifier in quantifiers)
                {
                    if (quantifier.QualifierName == "Language" && quantifier.QualifierValue.Equals(CultureInfo.CurrentUICulture.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return new LocalizedString(name, item.ValueAsString);
                    }
                }
            }

            return new LocalizedString(name, defaultCandidate?.ValueAsString ?? name, true);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var localizedString = this[name];
            if (localizedString.ResourceNotFound)
            {
                return localizedString;
            }

            return new LocalizedString(name, string.Format(localizedString.Value, arguments), localizedString.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        throw new NotImplementedException();
    }

    public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

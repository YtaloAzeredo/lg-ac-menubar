using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Tests;

[TestClass]
public sealed class CountryNamesTests
{
    [TestMethod]
    public void CoversEverySupportedCountry()
    {
        var codes = CountryNames.All.Select(entry => entry.Code).ToHashSet();

        foreach (var country in ThinQRegion.SupportedCountries)
        {
            Assert.Contains(country, codes);
        }

        Assert.HasCount(ThinQRegion.SupportedCountries.Count, CountryNames.All);
    }

    [TestMethod]
    public void NamesAreUnique()
    {
        var names = CountryNames.All.Select(entry => entry.Name).ToList();

        Assert.AreEqual(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void BrazilIsBrasil()
    {
        Assert.AreEqual("Brasil", CountryNames.All.Single(entry => entry.Code == "BR").Name);
    }

    [TestMethod]
    public void SortsWithPortugueseCollation()
    {
        var names = CountryNames.All.Select(entry => entry.Name).ToList();

        var australia = names.IndexOf("Austrália");
        var austria = names.IndexOf("Áustria");
        var azerbaijan = names.IndexOf("Azerbaijão");

        Assert.IsGreaterThanOrEqualTo(0, australia);
        Assert.IsLessThan(austria, australia);
        Assert.IsLessThan(azerbaijan, austria);
    }
}

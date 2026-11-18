namespace LGAC.Core.Tests;

[TestClass]
public class ThinQRegionTests
{
    [TestMethod]
    public void BrazilUsesAmericasServer()
    {
        Assert.AreEqual("aic", ThinQRegion.RegionFor("BR"));
        Assert.AreEqual("https://api-aic.lgthinq.com/", ThinQRegion.BaseUrlFor("BR")?.AbsoluteUri);
    }

    [TestMethod]
    public void OtherRegions()
    {
        Assert.AreEqual("aic", ThinQRegion.RegionFor("US"));
        Assert.AreEqual("eic", ThinQRegion.RegionFor("DE"));
        Assert.AreEqual("kic", ThinQRegion.RegionFor("KR"));
    }

    [TestMethod]
    public void AcceptsLowercase()
    {
        Assert.AreEqual("aic", ThinQRegion.RegionFor("br"));
    }

    [TestMethod]
    public void UnknownCountry()
    {
        Assert.IsNull(ThinQRegion.RegionFor("XX"));
        Assert.IsNull(ThinQRegion.BaseUrlFor("XX"));
    }

    [TestMethod]
    public void SupportedCountriesAreSorted()
    {
        var countries = ThinQRegion.SupportedCountries;
        CollectionAssert.Contains(countries.ToList(), "BR");
        CollectionAssert.AreEqual(countries.Order(StringComparer.Ordinal).ToList(), countries.ToList());
    }
}

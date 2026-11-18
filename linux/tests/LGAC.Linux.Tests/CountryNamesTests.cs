using LGAC.Core;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class CountryNamesTests
{
    private const string IcuOrder = "AF,ZA,AL,DE,AO,AG,SA,DZ,AR,AM,AW,AU,AT,AZ,BS,BH,BD,BB,BE,BZ,BJ,BY,BO,BA,BR,BG,BF,CV,CM,KH,CA,QA,KZ,TD,CL,CN,CY,CO,KR,CI,CR,HR,CU,DK,DJ,DM,EG,SV,AE,EC,SK,SI,ES,US,EE,ET,PH,FI,FR,GA,GM,GH,GE,GD,GR,GT,GY,GN,GQ,HT,HN,HK,HU,YE,IN,ID,IR,IQ,IE,IS,IL,IT,JM,JP,JO,XK,KW,LA,LV,LB,LR,LY,LT,LU,MK,MY,MW,ML,MT,MA,MU,MR,MX,MM,MD,ME,NP,NI,NE,NG,NO,NZ,OM,NL,PS,PA,PK,PY,PE,PL,PR,PT,KE,KG,GB,CF,CD,CG,DO,RO,RW,RU,LC,KN,ST,VC,SN,SL,RS,SG,SY,SO,LK,SD,SE,CH,SR,TH,TW,TZ,CZ,TG,TT,TN,TR,UA,UG,UY,UZ,VE,VN,ZM";

    [TestMethod]
    public void CoversEverySupportedCountry()
    {
        var codes = CountryNames.All.Select(entry => entry.Code).Order(StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(ThinQRegion.SupportedCountries.ToList(), codes);
    }

    [TestMethod]
    public void NamesAreUnique()
    {
        var names = CountryNames.All.Select(entry => entry.Name).ToList();

        Assert.AreEqual(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void BrazilIsCalledBrasil()
    {
        Assert.AreEqual("Brasil", CountryNames.All.Single(entry => entry.Code == "BR").Name);
    }

    [TestMethod]
    public void AccentsDoNotAffectOrdering()
    {
        var names = CountryNames.All.Select(entry => entry.Name).ToList();

        Assert.IsLessThan(names.IndexOf("Áustria"), names.IndexOf("Austrália"));
        Assert.IsLessThan(names.IndexOf("Azerbaijão"), names.IndexOf("Áustria"));
        Assert.IsLessThan(names.IndexOf("Índia"), names.IndexOf("Iêmen"));
        Assert.IsLessThan(names.IndexOf("Indonésia"), names.IndexOf("Índia"));
    }

    [TestMethod]
    public void MatchesPortugueseCollationOrder()
    {
        var actual = CountryNames.All.Select(entry => entry.Code).ToList();

        CollectionAssert.AreEqual(IcuOrder.Split(','), actual);
    }
}

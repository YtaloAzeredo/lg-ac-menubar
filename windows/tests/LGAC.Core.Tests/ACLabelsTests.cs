using System.Globalization;

namespace LGAC.Core.Tests;

[TestClass]
public class ACLabelsTests
{
    [TestMethod]
    public void JobModes()
    {
        var labels = new[] { "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO" }.Select(ACLabels.JobMode).ToArray();
        CollectionAssert.AreEqual(new[] { "Frio", "Quente", "Seco", "Ventilar", "Auto" }, labels);
        Assert.AreEqual("Energy saving", ACLabels.JobMode("ENERGY_SAVING"));
    }

    [TestMethod]
    public void WindStrengths()
    {
        var labels = new[] { "LOW", "MID", "HIGH", "AUTO" }.Select(ACLabels.WindStrength).ToArray();
        CollectionAssert.AreEqual(new[] { "Baixa", "Média", "Alta", "Auto" }, labels);
        Assert.AreEqual("Power", ACLabels.WindStrength("POWER"));
    }

    [TestMethod]
    public void Durations()
    {
        Assert.AreEqual("30 min", ACLabels.Duration(30));
        Assert.AreEqual("2 h", ACLabels.Duration(120));
        Assert.AreEqual("1h 20min", ACLabels.Duration(80));
    }

    [TestMethod]
    public void Temperatures()
    {
        Assert.AreEqual("23°", ACLabels.Temperature(23));
        Assert.AreEqual("23,5°", ACLabels.Temperature(23.5));
        Assert.AreEqual("22,5°", ACLabels.Temperature(22.5));
    }

    [TestMethod]
    public void TemperatureIgnoresMachineCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual("23,5°", ACLabels.Temperature(23.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void FormatsDoNotDependOnCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var name in new[] { "en-US", "" })
            {
                var culture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                Assert.AreEqual("23,5°", ACLabels.Temperature(23.5));
                Assert.AreEqual("22,5°", ACLabels.Temperature(22.5));
                Assert.AreEqual("23°", ACLabels.Temperature(23));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}

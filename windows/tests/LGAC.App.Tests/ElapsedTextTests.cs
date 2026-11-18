using LGAC.App.Views;

namespace LGAC.App.Tests;

[TestClass]
public sealed class ElapsedTextTests
{
    [TestMethod]
    [DataRow(0L, "0 seg")]
    [DataRow(12L, "12 seg")]
    [DataRow(60L, "1 min e 0 seg")]
    [DataRow(75L, "1 min e 15 seg")]
    [DataRow(3725L, "1 h e 2 min")]
    [DataRow(86400L, "1 dia")]
    [DataRow(90000L, "1 dia e 1 h")]
    [DataRow(172800L, "2 dias")]
    public void FormatsLikeTheMacApp(long seconds, string expected)
    {
        Assert.AreEqual(expected, ElapsedText.Format(TimeSpan.FromSeconds(seconds)));
    }

    [TestMethod]
    public void NegativeElapsedShowsZero()
    {
        Assert.AreEqual("0 seg", ElapsedText.Format(TimeSpan.FromSeconds(-5)));
    }
}

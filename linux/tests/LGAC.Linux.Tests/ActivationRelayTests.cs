using LGAC.Linux.Platform;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class ActivationRelayTests
{
    [TestMethod]
    public void RequestBeforeAttachActivatesOnceOnAttach()
    {
        var relay = new ActivationRelay();
        var activations = 0;

        relay.Request();
        relay.Request();
        Assert.AreEqual(0, activations);

        relay.Attach(() => activations++);

        Assert.AreEqual(1, activations);
    }

    [TestMethod]
    public void RequestAfterAttachActivatesEachTime()
    {
        var relay = new ActivationRelay();
        var activations = 0;
        relay.Attach(() => activations++);

        relay.Request();
        relay.Request();

        Assert.AreEqual(2, activations);
    }

    [TestMethod]
    public void AttachWithoutRequestDoesNotActivate()
    {
        var relay = new ActivationRelay();
        var activations = 0;

        relay.Attach(() => activations++);

        Assert.AreEqual(0, activations);
    }
}

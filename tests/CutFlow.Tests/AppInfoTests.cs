using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class AppInfoTests
{
    [TestMethod]
    public void ProductName_IsCutFlow()
    {
        Assert.AreEqual("CutFlow", AppInfo.ProductName);
    }
}

namespace Arduino.Tests;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestMethod1()
    {
        var config = Settings.GetConfig();
        Console.WriteLine("good");
    }
}

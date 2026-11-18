using System.Collections.Generic;
using NUnit.Framework;

public class TutorialPromptStateTests
{
    private class MemoryStore : TutorialPromptState.IStore
    {
        public readonly Dictionary<string, int> values = new Dictionary<string, int>();
        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int v) ? v : fallback;
        public void SetInt(string key, int value) => values[key] = value;
    }

    [SetUp]
    public void SetUp() => TutorialPromptState.UseStore(new MemoryStore());

    [TearDown]
    public void TearDown() => TutorialPromptState.UseStore(null);   // kembali ke PlayerPrefs asli

    [Test]
    public void FirstLaunch_ShouldShow()
    {
        Assert.IsTrue(TutorialPromptState.ShouldShow);
    }

    [Test]
    public void AfterMarkShown_ShouldNotShowAgain()
    {
        TutorialPromptState.MarkShown();

        Assert.IsFalse(TutorialPromptState.ShouldShow);
    }
}

using System.Collections.Generic;
using NUnit.Framework;

public class SoundSettingsTests
{
    private class MemoryStore : SoundSettings.IStore
    {
        public readonly Dictionary<string, int> values = new Dictionary<string, int>();
        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int v) ? v : fallback;
        public void SetInt(string key, int value) => values[key] = value;
    }

    private MemoryStore store;
    private int changes;

    [SetUp]
    public void SetUp()
    {
        store = new MemoryStore();
        SoundSettings.UseStore(store);
        changes = 0;
        SoundSettings.Changed += CountChange;
    }

    [TearDown]
    public void TearDown()
    {
        SoundSettings.Changed -= CountChange;
        SoundSettings.UseStore(null);   // kembali ke PlayerPrefs asli
    }

    private void CountChange() => changes++;

    [Test]
    public void BothAreOnByDefault()
    {
        Assert.IsTrue(SoundSettings.BgmOn);
        Assert.IsTrue(SoundSettings.SfxOn);
    }

    [Test]
    public void BgmAndSfxAreIndependent()
    {
        SoundSettings.BgmOn = false;

        Assert.IsFalse(SoundSettings.BgmOn);
        Assert.IsTrue(SoundSettings.SfxOn);
    }

    [Test]
    public void ChangeIsSavedInTheStore()
    {
        SoundSettings.SfxOn = false;

        var reloaded = new MemoryStore();
        foreach (var pair in store.values) reloaded.values[pair.Key] = pair.Value;
        SoundSettings.UseStore(reloaded);   // seperti membuka game lagi

        Assert.IsFalse(SoundSettings.SfxOn);
        Assert.IsTrue(SoundSettings.BgmOn);
    }

    [Test]
    public void ChangedFiresOnlyWhenTheValueChanges()
    {
        SoundSettings.BgmOn = true;    // sudah On
        Assert.AreEqual(0, changes);

        SoundSettings.BgmOn = false;
        SoundSettings.BgmOn = false;   // sama lagi
        Assert.AreEqual(1, changes);

        SoundSettings.BgmOn = true;
        Assert.AreEqual(2, changes);
    }
}

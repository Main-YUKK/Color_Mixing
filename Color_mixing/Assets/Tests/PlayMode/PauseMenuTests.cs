using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PauseMenuTests
{
    // Penyimpanan di memori: tes ini tidak boleh mengubah pengaturan suara asli di PlayerPrefs.
    private class MemoryStore : SoundSettings.IStore
    {
        private readonly System.Collections.Generic.Dictionary<string, int> values = new System.Collections.Generic.Dictionary<string, int>();
        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int v) ? v : fallback;
        public void SetInt(string key, int value) => values[key] = value;
    }

    [SetUp]
    public void SetUp() => SoundSettings.UseStore(new MemoryStore());

    [TearDown]
    public void TearDown()
    {
        PauseMenu.Close();
        SoundSettings.UseStore(null);
    }

    [UnityTest]
    public IEnumerator Open_ShowsMenu_AndClose_HidesIt()
    {
        Assert.IsFalse(PauseMenu.IsOpen);

        PauseMenu.Open();
        yield return null;
        Assert.IsTrue(PauseMenu.IsOpen);

        PauseMenu.Close();
        yield return null;
        Assert.IsFalse(PauseMenu.IsOpen);
    }

    [UnityTest]
    public IEnumerator Pausing_DoesNotStopTime()
    {
        float before = Time.timeScale;

        PauseMenu.Open();
        yield return null;

        Assert.AreEqual(before, Time.timeScale, "game tidak boleh berhenti untuk pemain lain");
    }

    [UnityTest]
    public IEnumerator OpeningTwice_BuildsOnlyOneMenu()
    {
        PauseMenu.Open();
        PauseMenu.Open();
        yield return null;

        Assert.IsTrue(PauseMenu.IsOpen);
        Assert.AreEqual(1, PauseMenu.Instance.transform.childCount);
    }

    [UnityTest]
    public IEnumerator ResumeButton_ClosesTheMenu()
    {
        PauseMenu.Open();
        yield return null;

        GameObject.Find("ResumeButton").GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.IsFalse(PauseMenu.IsOpen);
    }

    [UnityTest]
    public IEnumerator SoundButtons_FlipTheSettings()
    {
        PauseMenu.Open();
        yield return null;

        GameObject.Find("BgmButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(SoundSettings.BgmOn);
        Assert.IsTrue(SoundSettings.SfxOn);

        GameObject.Find("SfxButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(SoundSettings.SfxOn);

        GameObject.Find("BgmButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(SoundSettings.BgmOn);
    }
}

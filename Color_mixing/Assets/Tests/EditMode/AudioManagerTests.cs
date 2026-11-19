using NUnit.Framework;

public class AudioManagerTests
{
    [TestCase("MainMenu")]
    [TestCase("Lobby")]
    [TestCase("Tutorial")]
    [TestCase("SaveFiles")]
    public void MenuScenes_PlayBgmAndButtonSound(string sceneName)
    {
        Assert.IsTrue(AudioManager.IsMenuScene(sceneName));
    }

    // Scene game (dan Init) hening: BGM berhenti begitu host menekan Play.
    [TestCase("Init")]
    [TestCase("ClientArrival")]   // bukan scene menu (tanpa BGM lobby), tapi tombolnya bersuara: lihat HasButtonSound
    [TestCase("ColorMixing")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherScenes_AreSilent(string sceneName)
    {
        Assert.IsFalse(AudioManager.IsMenuScene(sceneName));
    }

    [TestCase("MainMenu", true)]
    [TestCase("Lobby", true)]
    [TestCase("ClientArrival", true)]
    [TestCase("Init", false)]
    [TestCase("ColorMixing", false)]   // additive di atas ClientArrival: scene aktifnya tetap ClientArrival
    [TestCase(null, false)]
    public void ButtonSound_OnMenuScenesAndClientArrival(string sceneName, bool expected)
    {
        Assert.AreEqual(expected, AudioManager.HasButtonSound(sceneName));
    }
}

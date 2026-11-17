using NUnit.Framework;

public class AudioManagerTests
{
    [TestCase("MainMenu")]
    [TestCase("Lobby")]
    [TestCase("Tutorial")]
    public void MenuScenes_PlayBgmAndButtonSound(string sceneName)
    {
        Assert.IsTrue(AudioManager.IsMenuScene(sceneName));
    }

    // Scene game (dan Init) hening: BGM berhenti begitu host menekan Play.
    [TestCase("Init")]
    [TestCase("ClientArrival")]
    [TestCase("ColorMixing")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherScenes_AreSilent(string sceneName)
    {
        Assert.IsFalse(AudioManager.IsMenuScene(sceneName));
    }
}

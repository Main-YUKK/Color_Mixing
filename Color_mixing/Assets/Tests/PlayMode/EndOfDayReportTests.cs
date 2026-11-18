using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public class EndOfDayReportTests
{
    private readonly System.Collections.Generic.List<Object> toDestroy = new System.Collections.Generic.List<Object>();
    private DayCycleManager cycle;
    private EndOfDayReport report;
    private GameObject panel, card, stamp, saved;
    private GameObject[] heads;
    private TextMeshProUGUI title, target;

    [TearDown]
    public void TearDown()
    {
        foreach (var o in toDestroy) if (o != null) Object.Destroy(o);
        toDestroy.Clear();
    }

    private GameObject New(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        toDestroy.Add(go);
        return go;
    }

    // animated = false: tidak ada Card / Stamp / Saved (perilaku lama).
    private void Build(bool animated)
    {
        cycle = New("Cycle").AddComponent<DayCycleManager>();

        var root = New("Report");
        root.SetActive(false);   // diaktifkan setelah semua field terisi
        report = root.AddComponent<EndOfDayReport>();
        panel = New("Panel", root.transform);
        title = New("Title", root.transform).AddComponent<TextMeshProUGUI>();
        target = New("Target", root.transform).AddComponent<TextMeshProUGUI>();
        heads = new[] { New("H1", root.transform), New("H2", root.transform), New("H3", root.transform) };

        PlayTestHelpers.SetField(report, "cycle", cycle);
        PlayTestHelpers.SetField(report, "panel", panel);
        PlayTestHelpers.SetField(report, "titleText", title);
        PlayTestHelpers.SetField(report, "targetText", target);
        PlayTestHelpers.SetField(report, "satisfiedHeads", heads);
        PlayTestHelpers.SetField(report, "showSeconds", 0.6f);

        if (animated)
        {
            card = New("Card", root.transform);
            stamp = New("Stamp", root.transform);
            saved = New("Saved", root.transform);
            PlayTestHelpers.SetField(report, "card", card.transform);
            PlayTestHelpers.SetField(report, "stamp", stamp);
            PlayTestHelpers.SetField(report, "savedMessage", saved);
            PlayTestHelpers.SetField(report, "cardInSeconds", 0.05f);
            PlayTestHelpers.SetField(report, "stampDelay", 0.2f);
            PlayTestHelpers.SetField(report, "stampSeconds", 0.05f);
            PlayTestHelpers.SetField(report, "savedDelay", 0.2f);
            PlayTestHelpers.SetField(report, "savedSeconds", 0.05f);
            PlayTestHelpers.SetField(report, "holdAfterSaved", 0.3f);
        }

        root.SetActive(true);
    }

    private void DayEnded(int day = 1, int yes = 3)
    {
        var m = typeof(EndOfDayReport).GetMethod("OnDayEnded", BindingFlags.Instance | BindingFlags.NonPublic);
        m.Invoke(report, new object[] { new DayResult { day = day, yesCount = yes } });
    }

    [UnityTest]
    public IEnumerator Texts_UseTheFormats()
    {
        Build(false);
        yield return null;

        DayEnded(day: 2, yes: 3);
        yield return null;

        Assert.AreEqual("Hari ke-2", title.text);
        Assert.AreEqual("3/3 puas", target.text);
    }

    [UnityTest]
    public IEnumerator SatisfiedHeads_MatchTheCount()
    {
        Build(false);
        yield return null;

        DayEnded(yes: 2);
        yield return null;

        Assert.IsTrue(heads[0].activeSelf);
        Assert.IsTrue(heads[1].activeSelf);
        Assert.IsFalse(heads[2].activeSelf);
    }

    [UnityTest]
    public IEnumerator WithoutAnimationObjects_ShowsThenHidesAfterShowSeconds()
    {
        Build(false);
        yield return null;

        DayEnded();
        yield return null;
        Assert.IsTrue(panel.activeSelf);

        yield return new WaitForSecondsRealtime(0.9f);
        Assert.IsFalse(panel.activeSelf, "setelah Show Seconds laporan ditutup");
    }

    [UnityTest]
    public IEnumerator Sequence_CardThenStampThenSavedThenClose()
    {
        Build(true);
        yield return null;

        DayEnded();
        yield return null;
        Assert.IsTrue(panel.activeSelf);
        Assert.IsFalse(stamp.activeSelf, "cap belum muncul saat kartu baru tampil");
        Assert.IsFalse(saved.activeSelf);

        yield return new WaitForSecondsRealtime(0.4f);   // cap sudah, pesan belum
        Assert.IsTrue(stamp.activeSelf);
        Assert.IsFalse(saved.activeSelf);

        yield return new WaitForSecondsRealtime(0.35f);  // pesan tersimpan muncul
        Assert.IsTrue(stamp.activeSelf);
        Assert.IsTrue(saved.activeSelf);

        yield return new WaitForSecondsRealtime(0.6f);   // ditahan lalu hari berikutnya
        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(stamp.activeSelf, "cap disembunyikan lagi untuk laporan berikutnya");
        Assert.IsFalse(saved.activeSelf);
    }

    [UnityTest]
    public IEnumerator Sequence_WithManualNextDay_WaitsForTheButton()
    {
        Build(true);
        PlayTestHelpers.SetField(report, "showSeconds", 0f);   // tidak otomatis
        yield return null;

        DayEnded();
        yield return new WaitForSecondsRealtime(1.2f);

        Assert.IsTrue(panel.activeSelf, "menunggu NextDay() dari tombol");
        Assert.IsTrue(saved.activeSelf);

        report.NextDay();
        Assert.IsFalse(panel.activeSelf);
    }

    [UnityTest]
    public IEnumerator Closing_MidSequence_StopsTheLaterSteps()
    {
        Build(true);
        yield return null;

        DayEnded();
        yield return new WaitForSecondsRealtime(0.1f);
        typeof(EndOfDayReport).GetMethod("Hide", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(report, null);   // hari berikutnya sudah mulai

        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsFalse(stamp.activeSelf, "cap tidak boleh muncul setelah laporan ditutup");
        Assert.IsFalse(saved.activeSelf);
    }
}

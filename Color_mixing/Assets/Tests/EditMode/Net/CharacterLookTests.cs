using NUnit.Framework;
using UnityEngine;

// Host mengacak rupa klien, client memakai angka yang sama: hasilnya harus identik.
public class CharacterLookTests
{
    private readonly System.Collections.Generic.List<Object> toDestroy = new System.Collections.Generic.List<Object>();

    [TearDown]
    public void TearDown()
    {
        GameNet.End();
        GameSession.CurrentLook = null;
        foreach (var o in toDestroy) if (o != null) Object.DestroyImmediate(o);
        toDestroy.Clear();
    }

    // Dibuat nonaktif, diisi, lalu diaktifkan supaya Awake memakai renderer yang sudah ada.
    private RandomCharacter NewCharacter()
    {
        var go = new GameObject("Char");
        toDestroy.Add(go);
        go.SetActive(false);
        var body = go.AddComponent<SpriteRenderer>();
        var hairGo = new GameObject("Hair");
        hairGo.transform.SetParent(go.transform, false);
        var hair = hairGo.AddComponent<SpriteRenderer>();

        var c = go.AddComponent<RandomCharacter>();
        c.bodyRenderer = body;
        c.hairRenderer = hair;
        c.hairStyles = new[]
        {
            new RandomCharacter.HairStyle { position = new Vector2(0.1f, 0.2f), scale = 1.5f },
            new RandomCharacter.HairStyle { position = new Vector2(-0.3f, 0.4f), scale = 0.8f, layer = RandomCharacter.HairLayer.Front },
            new RandomCharacter.HairStyle { position = Vector2.zero, scale = 1f, layer = RandomCharacter.HairLayer.Random },
        };
        c.colorVariants = new[] { new RandomCharacter.ColorVariant(), new RandomCharacter.ColorVariant() };
        go.SetActive(true);
        return c;
    }

    [Test]
    public void RollHair_StaysInsideTheOptions()
    {
        var c = NewCharacter();
        for (int i = 0; i < 50; i++)
        {
            var look = c.RollHair();
            Assert.That(look.style, Is.InRange(0, 2));
            Assert.That(look.variant, Is.InRange(0, 2));
        }
    }

    [Test]
    public void RollHair_WithoutStyles_ReturnsNull()
    {
        var c = NewCharacter();
        c.hairStyles = new RandomCharacter.HairStyle[0];
        Assert.IsNull(c.RollHair());
    }

    [Test]
    public void ApplyLook_GivesSameResultOnTwoDevices_AfterJson()
    {
        var host = NewCharacter();
        var client = NewCharacter();

        var rolled = host.RollHair();
        host.ApplyLook(rolled);
        client.ApplyLook(JsonUtility.FromJson<HairLook>(JsonUtility.ToJson(rolled)));   // lewat "jaringan"

        Assert.AreEqual(host.hairRenderer.color, client.hairRenderer.color);
        Assert.AreEqual(host.hairRenderer.sortingOrder, client.hairRenderer.sortingOrder);
        Assert.AreEqual(host.hairRenderer.transform.localPosition, client.hairRenderer.transform.localPosition);
        Assert.AreEqual(host.hairRenderer.transform.localScale, client.hairRenderer.transform.localScale);
    }

    [Test]
    public void ApplyLook_BadStyleIndex_IsIgnored()
    {
        var c = NewCharacter();
        var before = c.hairRenderer.transform.localScale;
        c.ApplyLook(new HairLook { style = 99 });
        c.ApplyLook(new HairLook { style = -1 });
        c.ApplyLook(null);
        Assert.AreEqual(before, c.hairRenderer.transform.localScale);   // tidak ada yang berubah
    }

    // ---------- Spawner memilih rupa ----------

    private CharacterSpawner NewSpawner(int prefabCount)
    {
        var go = new GameObject("Spawner");
        toDestroy.Add(go);
        go.SetActive(false);   // Start tidak perlu jalan
        var spawner = go.AddComponent<CharacterSpawner>();
        spawner.characterPrefabs = new GameObject[prefabCount];
        for (int i = 0; i < prefabCount; i++)
        {
            var p = new GameObject("Prefab" + i);
            toDestroy.Add(p);
            spawner.characterPrefabs[i] = p;
        }
        return spawner;
    }

    [Test]
    public void ChooseLook_Host_NeverRepeatsSamePrefab()
    {
        GameNet.BeginOffline();
        var spawner = NewSpawner(3);
        int previous = spawner.ChooseLook().prefabIndex;
        for (int i = 0; i < 30; i++)
        {
            int next = spawner.ChooseLook().prefabIndex;
            Assert.AreNotEqual(previous, next);
            previous = next;
        }
    }

    [Test]
    public void ChooseLook_Client_UsesTheHostChoice()
    {
        GameNet.Begin(new FakeNetwork().Nodes[1]);
        var spawner = NewSpawner(3);
        GameSession.CurrentLook = new ClientLook { valid = true, prefabIndex = 2 };

        Assert.AreEqual(2, spawner.ChooseLook().prefabIndex);
        Assert.AreEqual(2, spawner.ChooseLook().prefabIndex, "pilihan host dipakai lagi, tidak diacak");
    }

    [Test]
    public void ChooseLook_Client_IgnoresChoiceOutsideTheList()
    {
        GameNet.Begin(new FakeNetwork().Nodes[1]);
        var spawner = NewSpawner(2);
        GameSession.CurrentLook = new ClientLook { valid = true, prefabIndex = 7 };

        var look = spawner.ChooseLook();
        Assert.That(look.prefabIndex, Is.InRange(0, 1));
    }
}

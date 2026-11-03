using NUnit.Framework;
using UnityEngine;

namespace ColorMixing.Tests.Lobby
{
    public class PlayerSlotViewTests
    {
        private SlotHarness slot;

        [SetUp]
        public void SetUp()
        {
            slot = new SlotHarness();
            slot.Root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (slot != null) Object.DestroyImmediate(slot.Root);
        }

        private static LobbyPlayer Player(string name = Names.Ani, bool isHost = false, bool isLocal = false, bool isReady = false) =>
            new LobbyPlayer { name = name, isHost = isHost, isLocal = isLocal, isReady = isReady };

        // ---------- Slot kosong / terisi ----------

        [Test]
        public void Show_WithNull_ShowsEmptySlot()
        {
            slot.View.Show(null, viewerIsHost: true);

            Assert.That(slot.Silhouette.activeSelf, Is.True);
            Assert.That(slot.Character.activeSelf, Is.False);
            Assert.That(slot.ReadyIcon.activeSelf, Is.False);
            Assert.That(slot.KickObject.activeSelf, Is.False);
            Assert.That(slot.NameLabel.text, Is.EqualTo("[player]"));
        }

        [Test]
        public void Show_WithNull_UsesConfiguredEmptyLabel()
        {
            Reflect.SetField(slot.View, "emptyLabel", "[player 2]");

            slot.View.Show(null, viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("[player 2]"));
        }

        [Test]
        public void Show_WithPlayer_ShowsCharacterInsteadOfSilhouette()
        {
            slot.View.Show(Player(), viewerIsHost: false);

            Assert.That(slot.Character.activeSelf, Is.True);
            Assert.That(slot.Silhouette.activeSelf, Is.False);
        }

        [Test]
        public void Show_PlayerThenNull_ReturnsToEmptySlot()
        {
            slot.View.Show(Player(isReady: true), viewerIsHost: true);

            slot.View.Show(null, viewerIsHost: true);

            Assert.That(slot.Silhouette.activeSelf, Is.True);
            Assert.That(slot.Character.activeSelf, Is.False);
            Assert.That(slot.ReadyIcon.activeSelf, Is.False);
            Assert.That(slot.KickObject.activeSelf, Is.False);
            Assert.That(slot.NameLabel.text, Is.EqualTo("[player]"));
        }

        // ---------- Ikon ready ----------

        [TestCase(false, false, false, TestName = "Show_ReadyIcon_ClientNotReady_Hidden")]
        [TestCase(false, true, true, TestName = "Show_ReadyIcon_ClientReady_Visible")]
        [TestCase(true, false, false, TestName = "Show_ReadyIcon_HostNotReady_Hidden")]
        [TestCase(true, true, false, TestName = "Show_ReadyIcon_HostReady_StillHidden")]
        public void Show_ReadyIcon(bool isHost, bool isReady, bool expected)
        {
            slot.View.Show(Player(isHost: isHost, isReady: isReady), viewerIsHost: false);

            Assert.That(slot.ReadyIcon.activeSelf, Is.EqualTo(expected));
        }

        // ---------- Visibilitas tombol kick ----------

        [TestCase(true, false, true, TestName = "Show_Kick_HostViewingOtherPlayer_Visible")]
        [TestCase(true, true, false, TestName = "Show_Kick_HostViewingSelf_Hidden")]
        [TestCase(false, false, false, TestName = "Show_Kick_ClientViewingOtherPlayer_Hidden")]
        [TestCase(false, true, false, TestName = "Show_Kick_ClientViewingSelf_Hidden")]
        public void Show_KickVisibility(bool viewerIsHost, bool playerIsLocal, bool expected)
        {
            slot.View.Show(Player(isLocal: playerIsLocal), viewerIsHost);

            Assert.That(slot.KickObject.activeSelf, Is.EqualTo(expected));
        }

        // ---------- Label nama ----------

        [Test]
        public void Show_ClientLabel_IsPlainName()
        {
            slot.View.Show(Player("Ani"), viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("Ani"));
        }

        [Test]
        public void Show_HostLabel_HasHostSuffix()
        {
            slot.View.Show(Player("Ani", isHost: true), viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("Ani (host)"));
        }

        [Test]
        public void Show_NameOfExactly12Chars_IsNotTruncated()
        {
            slot.View.Show(Player("ABCDEFGHIJKL"), viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("ABCDEFGHIJKL"));
        }

        [Test]
        public void Show_NameOf13Chars_IsTruncatedWithEllipsis()
        {
            slot.View.Show(Player("ABCDEFGHIJKLM"), viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("ABCDEFGHIJKL…"));
        }

        [Test]
        public void Show_LongHostName_IsTruncatedBeforeHostSuffix()
        {
            slot.View.Show(Player("ABCDEFGHIJKLM", isHost: true), viewerIsHost: false);

            Assert.That(slot.NameLabel.text, Is.EqualTo("ABCDEFGHIJKL… (host)"));
        }

        // ---------- Klik kick ----------

        [Test]
        public void KickClick_RaisesEventWithCurrentPlayer()
        {
            var player = Player();
            LobbyPlayer kicked = null;
            slot.View.OnKickClicked += p => kicked = p;
            slot.View.Show(player, viewerIsHost: true);

            slot.KickButton.onClick.Invoke();

            Assert.That(kicked, Is.SameAs(player));
        }

        [Test]
        public void KickClick_AfterPlayerReplaced_RaisesEventWithNewPlayer()
        {
            var replacement = Player(Names.Budi);
            LobbyPlayer kicked = null;
            slot.View.OnKickClicked += p => kicked = p;
            slot.View.Show(Player(Names.Ani), viewerIsHost: true);
            slot.View.Show(replacement, viewerIsHost: true);

            slot.KickButton.onClick.Invoke();

            Assert.That(kicked, Is.SameAs(replacement));
        }

        [Test]
        public void KickClick_OnEmptySlot_DoesNotRaiseEvent()
        {
            int raised = 0;
            slot.View.OnKickClicked += _ => raised++;
            slot.View.Show(Player(), viewerIsHost: true);
            slot.View.Show(null, viewerIsHost: true);

            slot.KickButton.onClick.Invoke();

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void KickClick_WithNoSubscriber_DoesNotThrow()
        {
            slot.View.Show(Player(), viewerIsHost: true);

            Assert.DoesNotThrow(() => slot.KickButton.onClick.Invoke());
        }

        [Test]
        public void Awake_FindsKickButtonOnChildObject()
        {
            var nested = new SlotHarness(kickButtonOnChild: true);
            try
            {
                nested.Root.SetActive(true);
                var player = Player();
                LobbyPlayer kicked = null;
                nested.View.OnKickClicked += p => kicked = p;
                nested.View.Show(player, viewerIsHost: true);

                nested.KickButton.onClick.Invoke();

                Assert.That(kicked, Is.SameAs(player));
            }
            finally
            {
                Object.DestroyImmediate(nested.Root);
            }
        }

        // ---------- Referensi Inspector opsional ----------

        [Test]
        public void Show_WithoutInspectorReferences_DoesNotThrow()
        {
            var go = new GameObject("BareSlot");
            try
            {
                var bare = go.AddComponent<PlayerSlotView>();

                Assert.DoesNotThrow(() => bare.Show(Player(), viewerIsHost: true));
                Assert.DoesNotThrow(() => bare.Show(null, viewerIsHost: true));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

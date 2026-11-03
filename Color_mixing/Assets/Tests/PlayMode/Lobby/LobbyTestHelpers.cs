using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ColorMixing.Tests.Lobby
{
    // Kode lobby menyimpan banyak hal sebagai private (field Inspector, HandleMessage, dll).
    // Reflection dipakai supaya test tidak perlu mengubah kode game. Kalau ada yang di-rename,
    // test gagal dengan pesan yang menyebut nama yang hilang.
    internal static class Reflect
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, Flags);
            if (field == null) Assert.Fail($"Field '{fieldName}' tidak ada di {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        public static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, Flags);
            if (field == null) Assert.Fail($"Field '{fieldName}' tidak ada di {target.GetType().Name}.");
            return field.GetValue(target);
        }

        public static void Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, Flags);
            if (method == null) Assert.Fail($"Method '{methodName}' tidak ada di {target.GetType().Name}.");

            try
            {
                method.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                // Tampilkan exception asli, bukan pembungkus reflection.
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }
    }

    // Membungkus LobbyService baru untuk satu test, supaya state tidak bocor antar test.
    // Kalau dipasang sebagai Instance, Instance sebelumnya dikembalikan saat Dispose.
    internal sealed class ServiceHarness : IDisposable
    {
        private readonly GameObject go;
        private readonly LobbyService previousInstance;
        private readonly bool replacedInstance;

        public LobbyService Service { get; }

        public ServiceHarness(bool asInstance = false)
        {
            go = new GameObject("TestLobbyService");
            Service = go.AddComponent<LobbyService>();

            if (asInstance)
            {
                previousInstance = LobbyService.Instance;
                replacedInstance = true;
                SetInstance(Service);
            }
        }

        public void Dispose()
        {
            if (replacedInstance) SetInstance(previousInstance);
            Object.DestroyImmediate(go);
        }

        private static void SetInstance(LobbyService value)
        {
            var property = typeof(LobbyService).GetProperty(nameof(LobbyService.Instance));
            property.GetSetMethod(true).Invoke(null, new object[] { value });
        }

        public LobbyPlayer Local => Service.Players.Find(p => p.isLocal);
        public LobbyPlayer Player(string name) => Service.Players.Find(p => p.name == name);

        public void Receive(LobbyMessage message) => Reflect.Invoke(Service, "HandleMessage", message);
        public void ConnectionChanged(string playerName, string state) =>
            Reflect.Invoke(Service, "OnPlayerConnectionChanged", playerName, state);

        // Pesan dari client lain.
        public static LobbyMessage ClientMessage(string name, string type = "HELLO", bool ready = false) =>
            new LobbyMessage { type = type, name = name, isHost = false, ready = ready };

        // Pesan dari host.
        public static LobbyMessage HostMessage(string name = Names.Host, string type = "HELLO",
            string roomName = "", string slots = "", string target = "") =>
            new LobbyMessage { type = type, name = name, isHost = true, roomName = roomName, slots = slots, target = target };

        // Host dengan client yang sudah masuk.
        public void HostWithClients(params string[] clientNames)
        {
            Service.CreateRoom();
            foreach (var name in clientNames) Receive(ClientMessage(name));
        }

        // Client yang sudah mendengar host.
        public void ClientWithHost(string code = "12345")
        {
            Service.JoinRoom(code);
            Receive(HostMessage());
        }
    }

    internal static class Names
    {
        public const string Host = "HostAsli";
        public const string Ani = "Ani";
        public const string Budi = "Budi";
        public const string Citra = "Citra";
    }

    // PlayerSlotView lengkap dengan semua referensi Inspector-nya.
    // GameObject dibuat nonaktif: Awake baru jalan saat diaktifkan, yaitu setelah field terisi.
    internal sealed class SlotHarness
    {
        public PlayerSlotView View { get; }
        public TMP_Text NameLabel { get; }
        public GameObject ReadyIcon { get; }
        public GameObject Character { get; }
        public GameObject Silhouette { get; }
        public GameObject KickObject { get; }
        public Button KickButton { get; }
        public GameObject Root => View.gameObject;

        public SlotHarness(Transform parent = null, bool kickButtonOnChild = false)
        {
            var root = new GameObject("Slot");
            root.SetActive(false);
            if (parent != null) root.transform.SetParent(parent, false);

            View = root.AddComponent<PlayerSlotView>();
            NameLabel = UiFactory.Text("Name", root.transform);
            ReadyIcon = UiFactory.Child("ReadyIcon", root.transform);
            Character = UiFactory.Child("Character", root.transform);
            Silhouette = UiFactory.Child("Silhouette", root.transform);
            KickObject = UiFactory.Child("Kick", root.transform);

            var buttonHost = kickButtonOnChild ? UiFactory.Child("KickInner", KickObject.transform) : KickObject;
            KickButton = buttonHost.AddComponent<Button>();

            Reflect.SetField(View, "nameLabel", NameLabel);
            Reflect.SetField(View, "readyIcon", ReadyIcon);
            Reflect.SetField(View, "character", Character);
            Reflect.SetField(View, "silhouette", Silhouette);
            Reflect.SetField(View, "kickButton", KickObject);
        }
    }

    internal static class UiFactory
    {
        public static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static TMP_Text Text(string name, Transform parent) =>
            Child(name, parent).AddComponent<TextMeshProUGUI>();

        public static Button Button(string name, Transform parent)
        {
            var go = Child(name, parent);
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }
    }
}

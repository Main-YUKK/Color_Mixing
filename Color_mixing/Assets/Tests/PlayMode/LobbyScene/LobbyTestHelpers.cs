using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pembantu untuk test lobby (PlayMode).
// LobbyService menerima pesan lewat method private (HandleMessage, dst), jadi di sini dipanggil lewat reflection
// supaya test bisa meniru "pesan dari device lain" tanpa Game Center.
public static class LobbyTestHelpers
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    // Jumlah pemain agar game boleh mulai. Dibaca dari LobbyService supaya test tetap benar
    // kalau konstanta itu diubah ke 2 (tes dengan 2 device).
    public static int RequiredPlayers => ReadConst<int>(typeof(LobbyService), "RequiredPlayers");

    public static T ReadConst<T>(Type type, string name)
    {
        var field = type.GetField(name, Any);
        Assert.NotNull(field, $"Konstanta '{name}' tidak ditemukan di {type.Name}");
        return (T)field.GetRawConstantValue();
    }

    // LobbyService baru yang berdiri sendiri (bukan singleton LobbyService.Instance), supaya tiap test mulai bersih.
    public static LobbyService NewService(List<UnityEngine.Object> toDestroy)
    {
        var go = new GameObject("TestLobbyService");
        toDestroy.Add(go);
        return go.AddComponent<LobbyService>();
    }

    // ---------- Meniru pesan dan event dari device lain ----------

    public static void Receive(LobbyService service, string type, string name, bool isHost = false, bool ready = false,
        string roomName = "", string target = "", string slots = "")
    {
        var msg = new LobbyMessage
        {
            type = type, name = name, isHost = isHost, ready = ready,
            roomName = roomName, target = target, slots = slots
        };
        Call(service, "HandleMessage", msg);
    }

    public static void ConnectionChanged(LobbyService service, string playerName, string state)
    {
        Call(service, "OnPlayerConnectionChanged", playerName, state);
    }

    public static void DataReceived(LobbyService service, byte[] data, string fromName)
    {
        Call(service, "OnDataReceived", data, fromName);
    }

    // Client yang sedang join: host mengirim pesan terakhirnya = KICK untuk pemain lokal -> LobbyService.Fail().
    public static void KickLocal(LobbyService service)
    {
        Receive(service, "KICK", "HostX", isHost: true, target: LocalName(service));
    }

    private static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(m, $"Method '{method}' tidak ditemukan di {target.GetType().Name}");
        try { m.Invoke(target, args); }
        catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
    }

    // ---------- Daftar pemain ----------

    public static LobbyPlayer AddPlayer(LobbyService service, string name, bool isHost = false, bool ready = false)
    {
        var player = new LobbyPlayer { name = name, isHost = isHost, isReady = ready };
        service.Players.Add(player);
        return player;
    }

    public static string LocalName(LobbyService service)
    {
        var me = service.Players.Find(p => p.isLocal);
        Assert.NotNull(me, "tidak ada pemain lokal di lobby");
        return me.name;
    }

    // Jumlah subscriber sebuah event (field-like event: backing field-nya bernama sama). Untuk cek kebocoran subscribe.
    public static int Subscribers(object target, string eventName)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(field, $"Event '{eventName}' tidak ditemukan di {target.GetType().Name}");
        var handler = field.GetValue(target) as Delegate;
        return handler == null ? 0 : handler.GetInvocationList().Length;
    }
}

// Satu PlayerSlotView lengkap dengan objek-objek yang dipakainya (dipakai test PlayerSlotView dan LobbyScreen).
public class SlotRig
{
    public GameObject root;
    public PlayerSlotView view;
    public TextMeshPro label;
    public GameObject readyIcon;
    public GameObject character;
    public GameObject silhouette;
    public GameObject kick;
    public Button kickButton;

    // withKickButton=false: objek kick tanpa komponen Button (kasus salah setting di Inspector).
    public static SlotRig Build(string name, List<UnityEngine.Object> toDestroy, bool withKickButton = true)
    {
        var rig = new SlotRig();

        // Nonaktif dulu supaya Awake() jalan setelah semua field terisi.
        rig.root = Track(new GameObject(name), toDestroy);
        rig.root.SetActive(false);
        rig.view = rig.root.AddComponent<PlayerSlotView>();

        rig.label = Track(new GameObject(name + "Label"), toDestroy).AddComponent<TextMeshPro>();
        rig.readyIcon = Track(new GameObject(name + "Ready"), toDestroy);
        rig.character = Track(new GameObject(name + "Character"), toDestroy);
        rig.silhouette = Track(new GameObject(name + "Silhouette"), toDestroy);
        rig.kick = Track(new GameObject(name + "Kick"), toDestroy);
        if (withKickButton) rig.kickButton = rig.kick.AddComponent<Button>();

        PlayTestHelpers.SetField(rig.view, "nameLabel", rig.label);
        PlayTestHelpers.SetField(rig.view, "readyIcon", rig.readyIcon);
        PlayTestHelpers.SetField(rig.view, "character", rig.character);
        PlayTestHelpers.SetField(rig.view, "silhouette", rig.silhouette);
        PlayTestHelpers.SetField(rig.view, "kickButton", rig.kick);

        rig.root.SetActive(true);
        return rig;
    }

    private static GameObject Track(GameObject go, List<UnityEngine.Object> toDestroy)
    {
        toDestroy.Add(go);
        return go;
    }
}

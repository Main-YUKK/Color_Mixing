using System;
using UnityEngine;

public enum LeaveAlert
{
    HostLeft,                 // client: host keluar -> menu utama
    PlayerDisconnectedHost,   // host: ada client keluar -> lobby
    PlayerDisconnectedClient, // client: client lain keluar -> lobby
}

// Hal-hal yang dilakukan setelah ada pemain keluar. Dipisah dari aturannya supaya aturan bisa dites tanpa scene.
public interface ILeaveActions
{
    void Halt();                                                       // hentikan hari / timer
    void ShowAlert(LeaveAlert alert, int player, Action onClosed);     // peringatan singkat; onClosed dipanggil saat ditutup / habis waktu
    void GoMainMenu();
    void GoLobby();
}

// Aturan keluar / putus:
//   - host keluar            -> dua client: peringatan, lalu menu utama
//   - client keluar          -> client itu: langsung menu utama; host dan client lain: peringatan, lalu lobby
//   - putus tanpa pamit      -> sama dengan keluar (PeerLost dari transport)
// Hari yang belum selesai tidak tersimpan, jadi pemain yang tersisa memainkan hari itu lagi dari awal.
public class LeaveCoordinator : IDisposable
{
    [Serializable] private class PlayerData { public int index; }

    private readonly GameNet net;
    private readonly ILeaveActions actions;

    public LeaveCoordinator(GameNet net, ILeaveActions actions)
    {
        this.net = net ?? throw new ArgumentNullException(nameof(net));
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));

        net.Subscribe(NetMessageType.Leave, OnLeave);
        net.Subscribe(NetMessageType.PlayerLeft, OnPlayerLeft);
        net.PeerLost += OnPeerLost;
    }

    // true selama peristiwa keluar sedang diproses; peristiwa berikutnya diabaikan (LEAVE lalu putus = satu kejadian).
    public bool Ending { get; private set; }

    public void Dispose()
    {
        net.Unsubscribe(NetMessageType.Leave, OnLeave);
        net.Unsubscribe(NetMessageType.PlayerLeft, OnPlayerLeft);
        net.PeerLost -= OnPeerLost;
    }

    // Scene tujuan sudah termuat: kejadian keluar berikutnya boleh diproses lagi.
    public void ResetEnding() => Ending = false;

    // Pemain di device ini keluar dengan sengaja (menu pause: Exit).
    public void LeaveLocally()
    {
        if (Ending) return;
        Ending = true;

        if (!net.IsOffline)
        {
            var leave = NetMessage.Create(NetMessageType.Leave);
            if (net.IsHost) net.SendToAll(leave);
            else net.SendToHost(leave);
        }

        actions.Halt();
        actions.GoMainMenu();
    }

    // ---------- Kabar dari pemain lain ----------

    private void OnLeave(NetMessage message)
    {
        if (Ending) return;

        if (net.IsHost)
        {
            if (IsClientIndex(message.from)) ClientGone(message.from);
        }
        else if (message.from == 0)
        {
            HostGone();
        }
    }

    // Host memberi tahu client lain bahwa seorang client keluar.
    private void OnPlayerLeft(NetMessage message)
    {
        if (Ending || net.IsHost || message.from != 0) return;

        var data = message.Read<PlayerData>();
        if (data == null || !IsClientIndex(data.index) || data.index == net.LocalPlayerIndex) return;

        ClientGone(data.index);
    }

    private void OnPeerLost(int index)
    {
        if (Ending) return;

        if (net.IsHost)
        {
            if (IsClientIndex(index)) ClientGone(index);
        }
        else if (index == 0)
        {
            HostGone();
        }
    }

    // ---------- Akibat ----------

    private void HostGone()
    {
        Ending = true;
        actions.Halt();
        actions.ShowAlert(LeaveAlert.HostLeft, 0, actions.GoMainMenu);
    }

    private void ClientGone(int player)
    {
        Ending = true;

        if (net.IsHost)
        {
            // Client lain tidak tahu apa-apa kalau host tidak memberi tahu.
            for (int other = 1; other < PigmentAssignment.PlayerCount; other++)
                if (other != player)
                    net.SendTo(other, NetMessage.Create(NetMessageType.PlayerLeft, new PlayerData { index = player }));
        }

        actions.Halt();
        actions.ShowAlert(net.IsHost ? LeaveAlert.PlayerDisconnectedHost : LeaveAlert.PlayerDisconnectedClient, player, actions.GoLobby);
    }

    private static bool IsClientIndex(int index) => index >= 1 && index < PigmentAssignment.PlayerCount;
}

using System;

// Jaringan palsu di dalam memori untuk test: 3 pemain (0 = host, 1 dan 2 = client), pesan sampai langsung.
// Nanti dipakai juga untuk menguji pesan yang hilang / terlambat.
public class FakeNetwork
{
    public readonly FakeTransport[] Nodes;

    public FakeNetwork(int playerCount = PigmentAssignment.PlayerCount)
    {
        Nodes = new FakeTransport[playerCount];
        for (int i = 0; i < playerCount; i++) Nodes[i] = new FakeTransport(this, i);
    }

    // Satu GameNet per pemain, tanpa lewat GameNet.Instance.
    public GameNet[] CreateGameNets()
    {
        var nets = new GameNet[Nodes.Length];
        for (int i = 0; i < nets.Length; i++) nets[i] = new GameNet(Nodes[i]);
        return nets;
    }

    internal void Deliver(int from, int to, byte[] data) => Nodes[to].Raise(from, data);
}

public class FakeTransport : INetTransport
{
    private readonly FakeNetwork network;

    public FakeTransport(FakeNetwork network, int index)
    {
        this.network = network;
        LocalPlayerIndex = index;
    }

    public bool IsHost => LocalPlayerIndex == 0;
    public int LocalPlayerIndex { get; }
    public event Action<int, byte[]> DataReceived;

    public void SendToHost(byte[] data) => network.Deliver(LocalPlayerIndex, 0, data);

    public void SendToAll(byte[] data)
    {
        for (int i = 0; i < network.Nodes.Length; i++)
            if (i != LocalPlayerIndex) network.Deliver(LocalPlayerIndex, i, data);
    }

    public void SendTo(int playerIndex, byte[] data) => network.Deliver(LocalPlayerIndex, playerIndex, data);

    internal void Raise(int from, byte[] data) => DataReceived?.Invoke(from, data);

    // Untuk test: pura-pura ada data mentah masuk dari pemain lain.
    public void InjectRaw(int from, byte[] data) => Raise(from, data);
}

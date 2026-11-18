using UnityEngine;

// Penghubung DayCycleManager dengan jaringan. Taruh di objek yang sama dengan DayCycleManager.
// Host: kirim foto keadaan hari ke client setiap ada perubahan. Client: pasang foto dari host ke manager.
public class DayCycleNet : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;
    [Tooltip("Database yang sama dengan di ClientRequestController. Dipakai client untuk mencari request dari id kiriman host.")]
    [SerializeField] private ClientRequestDatabase database;

    private GameNet net;

    private void Reset() => cycle = GetComponent<DayCycleManager>();

    private void OnEnable()
    {
        if (cycle == null) cycle = GetComponent<DayCycleManager>();
        net = GameNet.Instance;
        if (net == null) return;   // tanpa sesi jaringan: tidak ada yang disinkronkan

        cycle.OnStatePublished += OnPublished;
        net.Subscribe(NetMessageType.DayState, OnDayState);
    }

    private void OnDisable()
    {
        if (net == null) return;
        cycle.OnStatePublished -= OnPublished;
        net.Unsubscribe(NetMessageType.DayState, OnDayState);
        net = null;
    }

    private void OnPublished(DaySnapshotKind kind)
    {
        if (!cycle.IsAuthority) return;
        net.SendToAll(NetMessage.Create(NetMessageType.DayState, cycle.TakeSnapshot(kind)));
    }

    private void OnDayState(NetMessage message)
    {
        if (message.from != 0 || cycle.IsAuthority) return;   // hanya host yang boleh mengatur hari

        var snapshot = message.Read<DaySnapshot>();
        if (snapshot == null) return;

        ClientRequest request = null;
        if (!string.IsNullOrEmpty(snapshot.requestId))
        {
            request = database != null ? database.GetById(snapshot.requestId) : null;
            if (request == null)
                Debug.LogError($"[DAY] Request '{snapshot.requestId}' dari host tidak ada di database (Database di DayCycleNet kosong / beda?)");
        }

        cycle.ApplyRemote(snapshot, request);
    }
}

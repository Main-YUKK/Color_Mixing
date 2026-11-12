using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Spawns one random character at a time. Each character prefab carries its own RandomCharacter
/// (so it picks its own random hairstyle/color when it appears).
/// The character walks in from the right to the spawn point, and walks back out to the right (the door) when replaced.
/// </summary>
public class CharacterSpawner : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    public Transform spawnPoint;                // where the client stands (uses this object's position if empty)
    public bool spawnOnStart = true;

    [Header("Walking")]
    [Tooltip("Off: characters just pop in and out like before.")]
    public bool walk = true;
    public Transform entryPoint;                // optional: where clients come from (default: Walk Distance to the right)
    public Transform exitPoint;                 // optional: where clients leave to (default: Walk Distance to the right, the door)
    public float walkDistance = 10f;
    public float walkSpeed = 3f;
    [Tooltip("How high the character hops while walking.")]
    public float bobHeight = 0.12f;
    [Tooltip("Hops per second.")]
    public float bobSpeed = 4f;

    [Header("Auto next customer (0 = only when SpawnNext() is called)")]
    public Vector2 stayTime = new Vector2(0f, 0f);

    /// <summary>Fired when the new client has reached the spot and can be judged (the timer starts here).</summary>
    public event Action OnClientArrived;

    GameObject current;
    public GameObject Current => current;
    /// <summary>True while a client is walking in or out.</summary>
    public bool IsBusy { get; private set; }
    int lastIndex = -1;
    float nextTime;
    float nativeSign = 1f;                      // scale.x sign of the prefab: it faces the way it walks in
    float entryDirX;

    Vector3 StandPos => spawnPoint != null ? spawnPoint.position : transform.position;
    Vector3 EntryPos => entryPoint != null ? entryPoint.position : StandPos + Vector3.right * walkDistance;
    Vector3 ExitPos => exitPoint != null ? exitPoint.position : StandPos + Vector3.right * walkDistance;

    void Start()
    {
        if (spawnOnStart) SpawnNext();
    }

    void Update()
    {
        if (!IsBusy && stayTime.y > 0f && Time.time >= nextTime) SpawnNext();
    }

    /// <summary>Current client walks away, then a new random one walks in (hook this to a button/event).</summary>
    [ContextMenu("Spawn Next")]
    public void SpawnNext() => SpawnNext(null);

    /// <summary>Rupa klien dipilih sekarang; gerakannya (klien lama pergi, klien baru masuk) menunggu selama hold() true.</summary>
    public void SpawnNext(Func<bool> hold)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;
        if (IsBusy) return;                     // ignore while someone is still walking

        // Dipilih SEKARANG (sebelum klien lama pergi), karena host mengirim pilihan ini ke client lewat GameSession.CurrentLook.
        var look = ChooseLook();
        GameSession.CurrentLook = look;
        IsBusy = true;
        StartCoroutine(Swap(look, hold));
    }

    /// <summary>Host / satu device: acak prefab + rambut. Client: pakai pilihan host (GameSession.CurrentLook).</summary>
    public ClientLook ChooseLook()
    {
        bool authority = GameNet.Instance == null || GameNet.Instance.IsHost;
        var shared = GameSession.CurrentLook;
        if (!authority && shared != null && shared.valid && shared.prefabIndex >= 0 && shared.prefabIndex < characterPrefabs.Length)
        {
            lastIndex = shared.prefabIndex;
            return shared;
        }

        // avoid the same character twice in a row
        int i = Random.Range(0, characterPrefabs.Length);
        if (characterPrefabs.Length > 1 && i == lastIndex) i = (i + 1 + Random.Range(0, characterPrefabs.Length - 1)) % characterPrefabs.Length;
        lastIndex = i;

        var look = new ClientLook { valid = true, prefabIndex = i };
        var prefab = characterPrefabs[i];
        if (prefab != null && prefab.TryGetComponent(out RandomCharacter hair))
        {
            look.hair = hair.RollHair();
            look.hasHair = look.hair != null;
        }
        return look;
    }

    IEnumerator Swap(ClientLook look, Func<bool> hold)
    {
        IsBusy = true;
        if (hold != null)
            while (hold()) yield return null;

        // old client leaves. Only start the nested coroutine when there is something to walk:
        // yielding one always costs a frame, and the new client must appear in the same frame when nobody walks.
        if (current != null)
        {
            if (walk) yield return SendOff();
            else { Destroy(current); current = null; }
        }

        // new client arrives
        Transform t = spawnPoint != null ? spawnPoint : transform;
        Vector3 startPos = walk ? EntryPos : t.position;
        current = Instantiate(characterPrefabs[look.prefabIndex], startPos, t.rotation);
        if (look.hasHair && current.TryGetComponent(out RandomCharacter hair)) hair.ApplyLook(look.hair);
        nativeSign = Mathf.Sign(current.transform.localScale.x);
        entryDirX = t.position.x - startPos.x;
        if (walk) yield return Walk(current, t.position);

        nextTime = Time.time + Random.Range(stayTime.x, stayTime.y);
        IsBusy = false;
        OnClientArrived?.Invoke();
    }

    /// <summary>Fired when the client has walked out and nobody replaced them (see Dismiss).</summary>
    public event Action OnClientLeft;

    /// <summary>Current client walks out the door and nobody comes in (end of day). Does nothing if nobody is standing or someone is walking.</summary>
    public void Dismiss()
    {
        if (IsBusy || current == null) return;
        StartCoroutine(Leave());
    }

    IEnumerator Leave()
    {
        IsBusy = true;
        yield return SendOff();
        IsBusy = false;
        OnClientLeft?.Invoke();
    }

    // The current client walks to the door (if walking is on) and is removed.
    IEnumerator SendOff()
    {
        if (current == null) yield break;

        if (walk)
        {
            Face(current, ExitPos.x - StandPos.x);      // face the door so they don't walk backward
            yield return Walk(current, ExitPos);
        }
        if (current != null) Destroy(current);
        current = null;
    }

    // Turns the character to face a horizontal direction (sprites face the way they walked in).
    void Face(GameObject go, float dirX)
    {
        if (Mathf.Approximately(dirX, 0f) || Mathf.Approximately(entryDirX, 0f)) return;
        bool sameAsEntry = Mathf.Sign(dirX) == Mathf.Sign(entryDirX);
        Vector3 s = go.transform.localScale;
        s.x = Mathf.Abs(s.x) * (sameAsEntry ? nativeSign : -nativeSign);
        go.transform.localScale = s;
    }

    // Moves the character in a straight line at walkSpeed, hopping up and down on the way.
    IEnumerator Walk(GameObject go, Vector3 target)
    {
        Vector3 basePos = go.transform.position;
        float bobTime = 0f;

        while (go != null && (basePos - target).sqrMagnitude > 0.0001f)
        {
            basePos = Vector3.MoveTowards(basePos, target, walkSpeed * Time.deltaTime);
            bobTime += Time.deltaTime;
            float hop = Mathf.Abs(Mathf.Sin(bobTime * bobSpeed * Mathf.PI)) * bobHeight;
            go.transform.position = basePos + Vector3.up * hop;
            yield return null;
        }

        if (go != null) go.transform.position = target;     // land exactly on the spot
    }
}

using System.Collections.Generic;
using UnityEngine;

// Jadwal level request per hari. 1 asset, diisi di Inspector (designer bisa ubah tanpa compile).
// Read-only saat runtime: jangan ubah list dari script, perubahan saat Play di Editor tersimpan ke asset.
[CreateAssetMenu(fileName = "DaySchedule", menuName = "Color Mixing/Day Schedule")]
public class DaySchedule : ScriptableObject
{
    [System.Serializable]
    public class Day
    {
        public List<RequestDifficulty> clients = new List<RequestDifficulty>();
    }

    // Hari yang ditulis eksplisit (index 0 = hari 1). Hari setelah list habis dihitung dari loop.
    [SerializeField] private List<Day> days = new List<Day>();

    // Mulai index ini, hari-hari di sisa list diputar terus (endless).
    [Tooltip("Index hari pertama yang di-loop (0-based). Sekarang 4 = hari 5.")]
    [SerializeField] private int loopStartIndex = 4;

    // GET level request untuk 1 klien.
    public RequestDifficulty GetLevel(int day, int clientIndex)
    {
        int dayIndex = day - 1;

        if (dayIndex >= days.Count)
            dayIndex = loopStartIndex + (dayIndex - loopStartIndex) % (days.Count - loopStartIndex);

        var clients = days[dayIndex].clients;
        return clients[clientIndex]; 
    }

    // DEBUG: klik kanan asset di Inspector -> "Log Hari 1-14".
#if UNITY_EDITOR
    [ContextMenu("Log Hari 1-14")]
    private void LogDays()
    {
        for (int day = 1; day <= 14; day++)
            Debug.Log($"[SCHEDULE] Hari {day}: {GetLevel(day, 0)} - {GetLevel(day, 1)} - {GetLevel(day, 2)}");
    }
#endif
}

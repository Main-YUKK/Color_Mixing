using UnityEngine;

// Taruh di scene gameplay. Memulai sesi baru dari Day 1.
// TODO: Continue / New Game dari lobby (pakai SaveService.HasSave()) belum ada.
public class DayCycleStarter : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;

    private void Start() => cycle.StartSession(false);
}

using UnityEngine;

// Taruh di scene gameplay. Save aktif (dipilih dari Saved Files) = lanjut dari hari tersimpan, kalau tidak mulai dari Day 1.
public class DayCycleStarter : MonoBehaviour
{
    [SerializeField] private DayCycleManager cycle;

    private void Start() => cycle.StartSession(SaveService.Active != null);
}

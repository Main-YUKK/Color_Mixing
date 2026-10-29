using UnityEngine;
using UnityEngine.SceneManagement;

// Objek ini hanya dipakai saat scene di-Play sendirian (kamera, EventSystem untuk tes).
// Saat scene dimuat additive di atas scene lain, objek ini dimatikan supaya tidak bentrok.
public class StandaloneSceneOnly : MonoBehaviour
{
    private void Awake()
    {
        if (SceneManager.sceneCount > 1) gameObject.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BootLog
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Log()
    {
        Debug.Log("[BOOT] scene aktif: " + SceneManager.GetActiveScene().name +
                  " | jumlah scene di build: " + SceneManager.sceneCountInBuildSettings);
    }
}
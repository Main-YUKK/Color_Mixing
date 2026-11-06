using UnityEngine;

public class MainMenuSession : MonoBehaviour
{
    private void Start()
    {
        MatchStarter.EndGame();
#if !UNITY_EDITOR
        GameNet.End();   // sesi game dari match sebelumnya (kalau ada) ikut ditutup
#endif
        SaveService.ClearActive();

        if (LobbyService.Instance != null)
        {
            LobbyService.Instance.LeaveRoom();
        }
    }
}
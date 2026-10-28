using UnityEngine;

public class MainMenuSession : MonoBehaviour
{
    private void Start()
    {
        SaveService.ClearActive();

        if (LobbyService.Instance != null)
        {
            LobbyService.Instance.LeaveRoom();
        }
    }
}
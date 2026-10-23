using UnityEngine;

public class MainMenuSession : MonoBehaviour
{
    private void Start()
    {
        if (LobbyService.Instance != null)
        {
            LobbyService.Instance.LeaveRoom();
        }
    }
}
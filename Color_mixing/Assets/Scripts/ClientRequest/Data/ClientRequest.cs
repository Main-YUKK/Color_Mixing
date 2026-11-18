using UnityEngine;

public enum RequestDifficulty
{
    LevelOne,
    LevelTwo,
    LevelThree,
}

[System.Serializable]
public class ClientRequest
{
    [Header("Request Details")]
    public string requestId;
    public string requestName;
    [TextArea(2, 5)] public string description;
    public Color targetColor = Color.white;
    public RequestDifficulty difficulty;

}

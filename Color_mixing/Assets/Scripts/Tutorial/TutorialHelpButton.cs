using UnityEngine;
using UnityEngine.UI;

// Tombol "?" (HelpButton): membuka scene tutorial dari scene mana pun, lalu tutorial kembali ke scene itu.
[RequireComponent(typeof(Button))]
public class TutorialHelpButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => TutorialEntry.Open());
    }
}

using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    public DialogueManager dialogueManager;

    [Tooltip("Matches Quest Manager → Required Talk Source Id when set; blank = any.")]
    public string completesTalkObjectiveAsSourceId;

    string[] dialogue = {
        "Hey Guardian, Im Chadani",
        "Our city is in danger.",
        "Different Creatures are attacking our city.",
        "Now KHYA is attacking near krishna mandir.",
        "Defeat him and save our Durbar Square."
    };

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            QuestManager.Resolve()?.NotifyTalkWithNpc(completesTalkObjectiveAsSourceId);
            dialogueManager.StartDialogue(dialogue);
        }
    }
}
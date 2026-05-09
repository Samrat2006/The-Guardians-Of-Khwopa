using UnityEngine;

public class ArissaProximityDialogue : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private Animator arissaAnimator;

    [Header("Dialogue Lines")]
    [TextArea(2, 6)]
    [SerializeField] private string[] dialogueLines =
    {
        "Hey Guardian, Im Chadani",
        "Our city is in danger.",
        "Different Creatures are attacking our city.",
        "Now KHYA is attacking near krishna mandir.",
        "Defeat him and save our Durbar Square."
    };

    [Header("Proximity Talking")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string talkingBoolParam = "IsTalking";

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Quest")]
    [Tooltip("Leave blank to complete Talk objective on first interaction.")]
    [SerializeField] private string completesTalkObjectiveAsSourceId;

    private bool _playerInRange;

    private void Awake()
    {
        // Dialogue freezes Time.timeScale=0; this keeps Arissa's Animator running during dialogue.
        if (arissaAnimator != null)
        {
            arissaAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            arissaAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            arissaAnimator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (!_playerInRange) return;
        if (dialogueManager == null) return;

        // Prevent re-trigger while a dialogue is already open.
        if (dialogueManager.dialoguePanel != null && dialogueManager.dialoguePanel.activeSelf)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            QuestManager.Resolve()?.NotifyTalkWithNpc(completesTalkObjectiveAsSourceId);
            dialogueManager.StartDialogue(dialogueLines);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInRange = true;
        SetTalking(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _playerInRange = false;
        SetTalking(false);
    }

    private void SetTalking(bool value)
    {
        if (arissaAnimator == null) return;
        if (string.IsNullOrWhiteSpace(talkingBoolParam)) return;
        arissaAnimator.SetBool(talkingBoolParam, value);
    }
}


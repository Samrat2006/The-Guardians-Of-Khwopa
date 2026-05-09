using System.Collections;
using UnityEngine;

/// <summary>
/// Chest interaction:
/// - When player enters trigger: show Yes/No prompt
/// - Yes: play open animation, give coins, show toast
/// - No or leaving range: hide prompt
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChestInteractable : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private string promptText = "Would you like to open the chest?";
    [SerializeField] private string rewardText = "You got 100x gold";

    [Header("Reward")]
    [SerializeField] private int goldReward = 100;

    [Header("Animation")]
    [Tooltip("Optional. If null, tries GetComponentInChildren<Animator>().")]
    [SerializeField] private Animator chestAnimator;
    [Tooltip("Trigger to play chest opening animation.")]
    [SerializeField] private string openTriggerName = "open";

    [Header("Visual swap (no Animator)")]
    [Tooltip("Optional: assign the closed chest mesh GameObject.")]
    [SerializeField] private GameObject closedVisual;
    [Tooltip("Optional: assign the opened chest mesh GameObject.")]
    [SerializeField] private GameObject openedVisual;

    [Header("Audio")]
    [Tooltip("Played when the player confirms opening the chest.")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] [Range(0f, 1f)] private float openSoundVolume = 1f;

    [Header("Behaviour")]
    [SerializeField] private bool hidePromptOnExit = true;
    [SerializeField] private float toastSeconds = 1.6f;
    [Tooltip("Optional: if set, uses this DialogueManager panel instead of the custom UI.")]
    [SerializeField] private DialogueManager dialogueManager;

    private bool opened;
    private bool playerInRange;
    private ChestPromptUI ui;
    private int openTriggerHash;

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;

        if (chestAnimator == null)
            chestAnimator = GetComponentInChildren<Animator>(true);

        // Auto-find visuals if the prefab has obvious names.
        if (closedVisual == null)
            closedVisual = FindChildByNameContains(transform, "close") ?? FindChildByNameContains(transform, "closed");
        if (openedVisual == null)
            openedVisual = FindChildByNameContains(transform, "open") ?? FindChildByNameContains(transform, "opened");

        if (closedVisual != null && openedVisual != null)
        {
            closedVisual.SetActive(true);
            openedVisual.SetActive(false);
        }

        openTriggerHash = Animator.StringToHash(openTriggerName);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (opened) return;
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        ShowPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;

        if (hidePromptOnExit)
            HidePrompt();
    }

    private void ShowPrompt()
    {
        if (opened) return;
        if (dialogueManager != null)
        {
            dialogueManager.ShowYesNoChoice(promptText, yes =>
            {
                if (!playerInRange && hidePromptOnExit) return;
                if (yes) OnYesOpen();
                else OnNoClose();
            });
            return;
        }

        ui = ui != null ? ui : ChestPromptUI.GetOrCreate();
        ui.Show(promptText, OnYesOpen, OnNoClose);
    }

    private void HidePrompt()
    {
        if (dialogueManager != null)
            dialogueManager.HideDialogue();
        if (ui != null)
            ui.Hide();
    }

    private void OnNoClose()
    {
        HidePrompt();
    }

    private void OnYesOpen()
    {
        if (opened) return;
        opened = true;

        if (dialogueManager != null)
        {
            dialogueManager.StartDialogue(new[] { rewardText });
        }
        else
        {
            // Close prompt UI (we'll show toast message in same panel briefly).
            ui = ui != null ? ui : ChestPromptUI.GetOrCreate();
            ui.SetToast(rewardText);
        }

        if (openSound != null)
            AudioSource.PlayClipAtPoint(openSound, transform.position, openSoundVolume);

        // Play animation if available.
        if (chestAnimator != null)
        {
            chestAnimator.ResetTrigger(openTriggerHash);
            chestAnimator.SetTrigger(openTriggerHash);
            chestAnimator.Update(0f);
        }
        else if (closedVisual != null && openedVisual != null)
        {
            // If you're using two prefabs (chest_close + chest_open), swap visuals.
            closedVisual.SetActive(false);
            openedVisual.SetActive(true);
        }
        else
        {
            // No animator and no visual swap configured.
        }

        // Award gold.
        if (CoinCollector.Instance != null)
            CoinCollector.Instance.AddCoin(goldReward);

        StartCoroutine(HideAfterToast());
    }

    private IEnumerator HideAfterToast()
    {
        float t = 0f;
        while (t < toastSeconds)
        {
            // If player walks away we still let the toast finish quickly, but don't re-open prompt.
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        HidePrompt();
    }

    private static GameObject FindChildByNameContains(Transform root, string token)
    {
        if (root == null || string.IsNullOrWhiteSpace(token)) return null;
        token = token.ToLowerInvariant();

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null) continue;
            if (t == root) continue;
            if (t.name != null && t.name.ToLowerInvariant().Contains(token))
                return t.gameObject;
        }

        return null;
    }
}


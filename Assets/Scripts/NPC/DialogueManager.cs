using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    [Header("Optional: NPC-style fields")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject continueHint;
    [Header("Defaults (used for string[] dialogues)")]
    [SerializeField] private bool showNpcFieldsForStringDialogue = true;
    [SerializeField] private string defaultSpeakerName;
    [SerializeField] private Sprite defaultPortrait;

    string[] lines;
    int index;
    private DialogueEntry[] entries;

    TypeWriterEffect typewriter;
    private bool choiceMode;
    private int choiceIndex;
    private System.Action<bool> onChoice; // true=yes, false=no
    private string choicePrompt;
    private int ignoreInputUntilFrame;

    public static bool IsBlockingGameplay { get; private set; }
    private static int s_blockCount;
    private static float s_prevTimeScale = 1f;
    private bool hasBlockToken;

    [Header("Highlight overlay (auto-created)")]
    [SerializeField] private bool dimBackgroundWhileOpen = true;
    [SerializeField] [Range(0f, 0.9f)] private float dimAlpha = 0.55f;
    private GameObject dimmer;
    private Image dimmerImage;

    void Start()
    {
        dialoguePanel.SetActive(false);
        typewriter = GetComponent<TypeWriterEffect>();
        EnsureDimmerExists();
        ApplyEntryVisuals(null);
        EnsureDialogueTextActive();
    }

    void Update()
    {
        if (!dialoguePanel.activeSelf) return;
        EnsureDialogueTextActive();
        // Prevent the same key-press that opened the dialogue from instantly selecting an option.
        if (Time.frameCount <= ignoreInputUntilFrame) return;

        if (choiceMode)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                choiceIndex = 0;
                RenderChoice();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                choiceIndex = 1;
                RenderChoice();
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                bool yes = choiceIndex == 0;
                System.Action<bool> cb = onChoice;
                ExitChoiceMode();
                cb?.Invoke(yes);
            }
            return;
        }

        // Normal dialogue: Enter goes to next line
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (typewriter != null && typewriter.IsTyping)
            {
                typewriter.CompleteInstantly();
                if (continueHint != null) continueHint.SetActive(true);
            }
            else
            {
                NextLine();
            }
        }
    }

    public void StartDialogue(string[] dialogueLines)
    {
        ExitChoiceMode();
        EnterBlockModeIfNeeded();
        ShowOnTopOfUI();
        dialoguePanel.SetActive(true);
        EnsureDialogueTextActive();
        ignoreInputUntilFrame = Time.frameCount + 1;

        lines = dialogueLines;
        entries = null;
        index = 0;

        if (lines == null || lines.Length == 0)
        {
            // Nothing to show; close gracefully.
            dialoguePanel.SetActive(false);
            ExitBlockModeIfNeeded();
            return;
        }

        ApplyEntryVisuals(null);
        ShowLine();
    }

    public void StartDialogue(DialogueEntry[] dialogueEntries)
    {
        ExitChoiceMode();
        EnterBlockModeIfNeeded();
        ShowOnTopOfUI();
        dialoguePanel.SetActive(true);
        EnsureDialogueTextActive();
        ignoreInputUntilFrame = Time.frameCount + 1;

        entries = dialogueEntries;
        lines = null;
        index = 0;

        if (entries == null || entries.Length == 0)
        {
            dialoguePanel.SetActive(false);
            ExitBlockModeIfNeeded();
            return;
        }

        ApplyEntryVisuals(entries[index]);
        ShowEntry();
    }

    public void ShowYesNoChoice(string prompt, System.Action<bool> onChosen)
    {
        ExitChoiceMode();
        EnterBlockModeIfNeeded();
        ShowOnTopOfUI();
        dialoguePanel.SetActive(true);
        EnsureDialogueTextActive();
        ignoreInputUntilFrame = Time.frameCount + 1;
        choiceMode = true;
        choicePrompt = prompt;
        onChoice = onChosen;
        choiceIndex = 0; // default YES
        ApplyEntryVisuals(null);
        RenderChoice();
    }

    public void HideDialogue()
    {
        ExitChoiceMode();
        dialoguePanel.SetActive(false);
        HideDimmer();
        ExitBlockModeIfNeeded();
        ignoreInputUntilFrame = 0;
        ApplyEntryVisuals(null);
    }

    private void EnsureDialogueTextActive()
    {
        if (dialoguePanel == null || !dialoguePanel.activeInHierarchy)
            return;

        if (dialogueText == null)
        {
            Debug.LogWarning($"{nameof(DialogueManager)}: dialogueText reference is not set.");
            return;
        }

        if (!dialogueText.gameObject.activeSelf)
            dialogueText.gameObject.SetActive(true);
        if (!dialogueText.enabled)
            dialogueText.enabled = true;

        // Optional fields: keep them enabled if their GameObjects are active.
        if (speakerNameText != null && speakerNameText.gameObject.activeSelf && !speakerNameText.enabled)
            speakerNameText.enabled = true;
        if (portraitImage != null && portraitImage.gameObject.activeSelf && !portraitImage.enabled)
            portraitImage.enabled = true;
    }

    public void NextLine()
    {
        if (choiceMode) return;
        if ((lines == null || lines.Length == 0) && (entries == null || entries.Length == 0))
        {
            // Dialogue panel was opened externally or lines were cleared.
            HideDialogue();
            return;
        }

        index++;

        if (entries != null && entries.Length > 0)
        {
            if (index < entries.Length)
            {
                ApplyEntryVisuals(entries[index]);
                ShowEntry();
            }
            else
            {
                dialoguePanel.SetActive(false);
                HideDimmer();
                ExitBlockModeIfNeeded();
                ApplyEntryVisuals(null);
            }
            return;
        }

        if (index < lines.Length)
        {
            ApplyEntryVisuals(null);
            ShowLine();
        }
        else
        {
            dialoguePanel.SetActive(false);
            HideDimmer();
            ExitBlockModeIfNeeded();
            ApplyEntryVisuals(null);
        }
    }

    void ShowLine()
    {
        if (choiceMode) return;
        if (lines == null || lines.Length == 0 || index < 0 || index >= lines.Length)
        {
            HideDialogue();
            return;
        }

        if (continueHint != null) continueHint.SetActive(false);

        string text = lines[index] ?? "";
        if (typewriter != null)
            typewriter.Begin(dialogueText, text, () => { if (continueHint != null) continueHint.SetActive(true); });
        else
        {
            dialogueText.text = text;
            if (continueHint != null) continueHint.SetActive(true);
        }
    }

    void ShowEntry()
    {
        if (choiceMode) return;
        if (entries == null || entries.Length == 0 || index < 0 || index >= entries.Length)
        {
            HideDialogue();
            return;
        }

        if (continueHint != null) continueHint.SetActive(false);

        string text = entries[index].text ?? "";
        if (typewriter != null)
            typewriter.Begin(dialogueText, text, () => { if (continueHint != null) continueHint.SetActive(true); });
        else
        {
            dialogueText.text = text;
            if (continueHint != null) continueHint.SetActive(true);
        }
    }

    private void RenderChoice()
    {
        // Highlight selected option using TMP rich text.
        string yes = choiceIndex == 0 ? "<color=#FFD34D><b>[ YES ]</b></color>" : "[ YES ]";
        string no = choiceIndex == 1 ? "<color=#FFD34D><b>[ NO ]</b></color>" : "[ NO ]";
        dialogueText.text = $"{choicePrompt}\n\n{yes}        {no}";
        if (continueHint != null) continueHint.SetActive(false);
    }

    private void ExitChoiceMode()
    {
        choiceMode = false;
        choiceIndex = 0;
        onChoice = null;
        choicePrompt = null;
    }

    private void ApplyEntryVisuals(DialogueEntry? entry)
    {
        if (speakerNameText != null)
        {
            string name = entry.HasValue ? entry.Value.speakerName : null;
            if (!entry.HasValue && showNpcFieldsForStringDialogue)
                name = defaultSpeakerName;
            bool hasName = !string.IsNullOrWhiteSpace(name);
            speakerNameText.gameObject.SetActive(hasName);
            if (hasName) speakerNameText.text = name;
        }

        if (portraitImage != null)
        {
            Sprite portrait = entry.HasValue ? entry.Value.portrait : null;
            if (!entry.HasValue && showNpcFieldsForStringDialogue)
                portrait = defaultPortrait;
            bool hasPortrait = portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            if (hasPortrait) portraitImage.sprite = portrait;
        }
    }

    /// <summary>Shared freeze stack (dialogue, quest journal, etc.). Same <see cref="IsBlockingGameplay"/> flag.</summary>
    public static void AddGlobalGameplayBlock()
    {
        s_blockCount++;
        if (s_blockCount == 1)
        {
            s_prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsBlockingGameplay = true;
        }
    }

    public static void RemoveGlobalGameplayBlock()
    {
        if (s_blockCount <= 0) return;
        s_blockCount--;
        if (s_blockCount == 0)
        {
            Time.timeScale = s_prevTimeScale;
            IsBlockingGameplay = false;
        }
    }

    private void EnterBlockModeIfNeeded()
    {
        if (hasBlockToken) return;
        hasBlockToken = true;
        AddGlobalGameplayBlock();
    }

    private void ExitBlockModeIfNeeded()
    {
        if (!hasBlockToken) return;
        hasBlockToken = false;
        RemoveGlobalGameplayBlock();
    }

    private void OnDisable()
    {
        // Safety: if this object gets disabled while dialogue is up, unfreeze the game.
        HideDimmer();
        ExitBlockModeIfNeeded();
    }

    private void OnDestroy()
    {
        HideDimmer();
        ExitBlockModeIfNeeded();
    }

    private void ShowOnTopOfUI()
    {
        EnsureDimmerExists();
        if (dimBackgroundWhileOpen)
            ShowDimmer();

        // Ensure we render on top of other UI in the same Canvas.
        if (dialoguePanel != null)
            dialoguePanel.transform.SetAsLastSibling();

        if (dimmer != null && dialoguePanel != null)
        {
            // Place dimmer just behind the dialogue panel.
            dimmer.transform.SetSiblingIndex(Mathf.Max(0, dialoguePanel.transform.GetSiblingIndex() - 1));
        }
    }

    private void EnsureDimmerExists()
    {
        if (!dimBackgroundWhileOpen) return;
        if (dialoguePanel == null) return;
        if (dimmer != null) return;

        Canvas canvas = dialoguePanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        dimmer = new GameObject("DialogueDimmer", typeof(RectTransform));
        dimmer.transform.SetParent(canvas.transform, false);
        RectTransform rt = dimmer.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        dimmerImage = dimmer.AddComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, dimAlpha);
        dimmerImage.raycastTarget = true; // blocks clicks on pause menu behind

        dimmer.SetActive(false);
    }

    private void ShowDimmer()
    {
        if (dimmer == null) return;
        if (dimmerImage != null)
            dimmerImage.color = new Color(0f, 0f, 0f, dimAlpha);
        dimmer.SetActive(true);
    }

    private void HideDimmer()
    {
        if (dimmer != null)
            dimmer.SetActive(false);
    }
}
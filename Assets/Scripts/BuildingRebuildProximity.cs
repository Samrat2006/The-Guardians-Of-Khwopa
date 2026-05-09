using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Proximity rebuild: ruined art stays visible until the player pays coins and waits for a short "work" timer,
/// then the restored group is revealed and the destroyed group is hidden.
/// <para><b>Hierarchy:</b> use one empty GameObject + trigger + this script; add two child roots (defaults: names <c>Destroyed</c> / <c>Restored</c>)
/// parenting all ruin vs rebuilt meshes/fabs.</para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BuildingRebuildProximity : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Parent of all ruined meshes (preferred). Drag it or leave blank and match Child Name below.")]
    [SerializeField] private GameObject destroyedPhaseRoot;

    [Tooltip("Parent of all rebuilt meshes. Usually inactive until restoration finishes (script hides it until then).")]
    [SerializeField] private GameObject restoredPhaseRoot;

    [Tooltip("If Destroyed Phase Root is empty: first direct child with this GameObject name under this rebuild object.")]
    [SerializeField] private string destroyedPhaseChildName = "Destroyed";

    [Tooltip("If Restored Phase Root is empty: first direct child with this name.")]
    [SerializeField] private string restoredPhaseChildName = "Restored";

    [Header("Cost")]
    [SerializeField] private int coinsToRebuild = 50;

    [Header("Confirmation (optional)")]
    [Tooltip("If on, player must confirm YES before coins are spent and rebuilding starts.")]
    [SerializeField] private bool requireYesNoConfirmation = true;
    [TextArea(2, 4)]
    [SerializeField] private string confirmationPrompt = "Rebuild this building now?";

    [Header("Timing")]
    [Tooltip("Single E press spends coins immediately, then rebuilding finishes after this delay.")]
    [SerializeField] private float restorationWorkflowSeconds = 2.5f;
    [Tooltip("Recommended on: survives pause / dialogue freezing Time.timeScale during optional hint lines.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Rebuild VFX (optional)")]
    [Tooltip("Spawned while rebuilding (between paying and the final swap).")]
    [SerializeField] private GameObject rebuildingVfxPrefab;
    [Tooltip("If set, prefab spawns at this transform. If empty, uses this GameObject's transform.")]
    [SerializeField] private Transform rebuildingVfxSpawnPoint;
    [Tooltip("If true, VFX is destroyed when rebuild completes (recommended).")]
    [SerializeField] private bool destroyRebuildingVfxOnComplete = true;
    [Tooltip("If > 0, force-destroys VFX after this many seconds (safety). Uses real time.")]
    [SerializeField] private float rebuildingVfxMaxLifetimeSeconds = 6f;

    [Header("Input")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Interact zone")]
    [Tooltip("Shrink trigger after rebuild.")]
    [SerializeField] private bool disableColliderAfterRebuild = true;
    [Tooltip("Optional TMP near the ruin; cleared when rebuilt.")]
    [SerializeField] private TextMeshProUGUI worldOrScreenPromptHint;

    [Header("Feedback")]
    [SerializeField] private AudioClip insufficientFundsSound;
    [SerializeField] private AudioClip restorationStartSound;
    [SerializeField] private AudioClip restorationCompleteSound;
    [SerializeField] private float sfxVolume = 1f;

    [Header("Messages (optional)")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private string insufficientFundsDialogue =
        "You need more blessings (coins) before you can dedicate this shrine.";
    [SerializeField] private string rebuildingDialogueHint;

    [Header("Quest")]
    [SerializeField] private bool notifyQuestTempleWhenComplete = true;

    [Header("Save (optional)")]
    [Tooltip("Leave empty for no persistence. Recommended: TempleRebuild_MyScene_MyObject")]
    [SerializeField] private string playerPrefsRebuildKey;

    private Collider _zone;
    private bool _playerInRange;
    private bool _hasRebuilt;
    private Coroutine _routine;
    private bool _rebuildRoutineRunning;
    private CoinCollector _coins;
    private GameObject _rebuildingVfxInstance;
    private bool _confirmationShowing;

    public bool HasRebuilt => _hasRebuilt;

    private GameObject ResolvedDestroyedGo { get; set; }
    private GameObject ResolvedRestoredGo { get; set; }

    private void Awake()
    {
        ResolveVisualRoots();

        _zone = GetComponent<Collider>();
        if (_zone != null)
            _zone.isTrigger = true;
        _coins = CoinCollector.Instance != null ? CoinCollector.Instance : FindObjectOfType<CoinCollector>();

        TryRestoreFromPersistence();

        // Before first rebuild: ruined visible, restored hidden (skip if prefs already restored).
        if (!_hasRebuilt)
            ApplyRuinedStartingVisualState();
    }

    private void ResolveVisualRoots()
    {
        ResolvedDestroyedGo = destroyedPhaseRoot != null ? destroyedPhaseRoot : FindDirectChildByName(transform, destroyedPhaseChildName)?.gameObject;
        ResolvedRestoredGo = restoredPhaseRoot != null ? restoredPhaseRoot : FindDirectChildByName(transform, restoredPhaseChildName)?.gameObject;

        if (ResolvedDestroyedGo == null && ResolvedRestoredGo == null)
            Debug.LogWarning($"{nameof(BuildingRebuildProximity)} on '{name}': no destroyed/restored roots. Assign refs or children named '{destroyedPhaseChildName}' / '{restoredPhaseChildName}'.", this);
    }

    private static Transform FindDirectChildByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;
        string want = childName.Trim();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c != null && c.name == want)
                return c;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        StopRebuildVfx();
    }

    private void ApplyRuinedStartingVisualState()
    {
        if (ResolvedDestroyedGo != null)
            ResolvedDestroyedGo.SetActive(true);
        if (ResolvedRestoredGo != null)
            ResolvedRestoredGo.SetActive(false);
    }

    private void TryRestoreFromPersistence()
    {
        if (string.IsNullOrWhiteSpace(playerPrefsRebuildKey))
            return;
        if (PlayerPrefs.GetInt(playerPrefsRebuildKey, 0) != 1)
            return;
        ApplyRebuiltVisualImmediate();
        _hasRebuilt = true;
        if (_zone != null && disableColliderAfterRebuild)
            _zone.enabled = false;
    }

    private void Update()
    {
        if (!_playerInRange || _hasRebuilt)
            return;
        if (DialogueManager.IsBlockingGameplay)
            return;

        if (Input.GetKeyDown(interactKey))
            AttemptStartRebuild();

        RefreshPromptUi();
    }

    private void RefreshPromptUi()
    {
        if (worldOrScreenPromptHint == null)
            return;
        worldOrScreenPromptHint.gameObject.SetActive(_playerInRange && !_hasRebuilt);
        if (_playerInRange && !_hasRebuilt)
            worldOrScreenPromptHint.text =
                $"{interactKey} — Rebuild ({coinsToRebuild} coins)";
    }

    private void AttemptStartRebuild()
    {
        if (_hasRebuilt || _rebuildRoutineRunning)
            return;

        if (requireYesNoConfirmation)
        {
            ShowConfirmationPrompt();
            return;
        }

        BeginRebuildAfterConfirmation();
    }

    private void ShowConfirmationPrompt()
    {
        if (_confirmationShowing)
            return;

        _confirmationShowing = true;

        // If no dialogue manager assigned, we can't show a choice. Fall back to starting immediately.
        if (dialogueManager == null)
        {
            _confirmationShowing = false;
            BeginRebuildAfterConfirmation();
            return;
        }

        string prompt = !string.IsNullOrWhiteSpace(confirmationPrompt)
            ? confirmationPrompt
            : $"Rebuild this building for {coinsToRebuild} coins?";

        dialogueManager.ShowYesNoChoice(prompt, yes =>
        {
            _confirmationShowing = false;
            if (!yes) return;
            if (_hasRebuilt || _rebuildRoutineRunning) return;
            if (!_playerInRange) return;
            BeginRebuildAfterConfirmation();
        });
    }

    private void BeginRebuildAfterConfirmation()
    {
        var cc = _coins != null ? _coins : (CoinCollector.Instance != null ? CoinCollector.Instance : FindObjectOfType<CoinCollector>());
        _coins = cc;
        if (cc == null)
            return;

        if (!cc.TrySpendCoins(coinsToRebuild))
        {
            Sound(insufficientFundsSound);
            if (dialogueManager != null && !string.IsNullOrWhiteSpace(insufficientFundsDialogue))
                dialogueManager.StartDialogue(new[] { insufficientFundsDialogue });
            return;
        }

        Sound(restorationStartSound);
        if (dialogueManager != null && !string.IsNullOrWhiteSpace(rebuildingDialogueHint))
            dialogueManager.StartDialogue(new[] { rebuildingDialogueHint });

        StartRebuildVfx();

        _rebuildRoutineRunning = true;
        _routine = StartCoroutine(RestoreRoutine());
    }

    private IEnumerator RestoreRoutine()
    {
        float wait = Mathf.Max(0f, restorationWorkflowSeconds);
        float end = (useUnscaledTime ? Time.unscaledTime : Time.time) + wait;
        while (true)
        {
            float now = useUnscaledTime ? Time.unscaledTime : Time.time;
            if (now >= end)
                break;
            yield return null;
        }

        FinishRebuild();
    }

    private void FinishRebuild()
    {
        _hasRebuilt = true;
        StopRebuildVfx();
        ApplyRebuiltVisualImmediate();

        Sound(restorationCompleteSound);

        if (notifyQuestTempleWhenComplete)
            QuestManager.Resolve()?.NotifyTempleRebuilt();

        if (_zone != null && disableColliderAfterRebuild)
            _zone.enabled = false;

        if (worldOrScreenPromptHint != null)
            worldOrScreenPromptHint.gameObject.SetActive(false);

        SavePersistence();

        Destroy(this);
    }


    private void ApplyRebuiltVisualImmediate()
    {
        if (ResolvedDestroyedGo != null)
            ResolvedDestroyedGo.SetActive(false);
        if (ResolvedRestoredGo != null)
            ResolvedRestoredGo.SetActive(true);
    }

    private void SavePersistence()
    {
        if (string.IsNullOrWhiteSpace(playerPrefsRebuildKey))
            return;
        PlayerPrefs.SetInt(playerPrefsRebuildKey, 1);
        PlayerPrefs.Save();
    }

    private void Sound(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
    }

    private void StartRebuildVfx()
    {
        if (rebuildingVfxPrefab == null)
            return;

        if (_rebuildingVfxInstance != null)
            return;

        Transform at = rebuildingVfxSpawnPoint != null ? rebuildingVfxSpawnPoint : transform;
        _rebuildingVfxInstance = Instantiate(rebuildingVfxPrefab, at.position, at.rotation);
        if (at != null)
            _rebuildingVfxInstance.transform.SetParent(at, worldPositionStays: true);

        // Safety cleanup in case something interrupts the flow.
        if (rebuildingVfxMaxLifetimeSeconds > 0.01f)
            Destroy(_rebuildingVfxInstance, rebuildingVfxMaxLifetimeSeconds);
    }

    private void StopRebuildVfx()
    {
        if (_rebuildingVfxInstance == null)
            return;

        if (destroyRebuildingVfxOnComplete)
        {
            Destroy(_rebuildingVfxInstance);
        }
        else
        {
            // If you prefer the VFX to fade out naturally, stop particle systems.
            foreach (ParticleSystem ps in _rebuildingVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
            }
        }

        _rebuildingVfxInstance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.CompareTag(playerTag))
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag(playerTag))
            _playerInRange = false;
    }

    /// <summary>Forced completion (cutscenes / debug) without coins.</summary>
    public void CheatCompleteRebuild()
    {
        if (_hasRebuilt)
            return;
        _hasRebuilt = true;
        ApplyRebuiltVisualImmediate();
        if (notifyQuestTempleWhenComplete)
            QuestManager.Resolve()?.NotifyTempleRebuilt();
        if (_zone != null && disableColliderAfterRebuild)
            _zone.enabled = false;
        SavePersistence();
        Destroy(this);
    }
}

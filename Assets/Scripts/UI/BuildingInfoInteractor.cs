using UnityEngine;

/// <summary>
/// Put this on the Player. When inside a BuildingInfoSource trigger, press E to open/close the shared panel.
/// </summary>
[DisallowMultipleComponent]
public class BuildingInfoInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BuildingInfoPanel panel;

    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Rules")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("If on, opening info will freeze gameplay using DialogueManager's global block.")]
    [SerializeField] private bool blockGameplayWhileOpen = true;

    private BuildingInfoSource _current;
    private int _ignoreInputUntilFrame;
    private bool _hasBlockToken;

    private void Awake()
    {
        if (panel == null)
            panel = FindFirstObjectByType<BuildingInfoPanel>();
    }

    private void Update()
    {
        if (Time.frameCount <= _ignoreInputUntilFrame)
            return;

        bool open = panel != null && panel.IsOpen;
        if (open)
        {
            if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(interactKey))
            {
                Close();
            }
            return;
        }

        if (_current == null) return;

        if (Input.GetKeyDown(interactKey))
        {
            Open(_current);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!other.CompareTag(playerTag) && !CompareTag(playerTag))
        {
            // If this is on the player, other should be the building trigger (not player).
            // So we don't filter by other tag here.
        }

        BuildingInfoSource src = other.GetComponentInParent<BuildingInfoSource>();
        if (src == null)
            src = other.GetComponent<BuildingInfoSource>();
        if (src == null)
            return;

        _current = src;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        BuildingInfoSource src = other.GetComponentInParent<BuildingInfoSource>();
        if (src == null)
            src = other.GetComponent<BuildingInfoSource>();
        if (src == null)
            return;

        if (_current == src)
            _current = null;
    }

    private void Open(BuildingInfoSource src)
    {
        if (panel == null || src == null) return;

        if (blockGameplayWhileOpen)
        {
            _hasBlockToken = true;
            DialogueManager.AddGlobalGameplayBlock();
        }

        panel.Show(src);
        _ignoreInputUntilFrame = Time.frameCount + 1;
    }

    private void Close()
    {
        if (panel != null)
            panel.Hide();

        if (_hasBlockToken)
        {
            _hasBlockToken = false;
            DialogueManager.RemoveGlobalGameplayBlock();
        }

        _ignoreInputUntilFrame = Time.frameCount + 1;
    }
}


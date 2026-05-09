using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keyboard navigation for Main Menu without requiring mouse.
/// - Down/S: next option
/// - Up/W: previous option
/// - Enter: execute
///
/// Automatically finds menu options by TMP text content ("Start", "Quit") and highlights the selected one.
/// Put this on the MainmenuManager GameObject in Mainmenu scene.
/// </summary>
public class MainMenuKeyboardController : MonoBehaviour
{
    [SerializeField] private MainMenuManager menu;
    [Tooltip("Optional; if empty, will auto-find TMP labels with text 'Start' and 'Quit'.")]
    [SerializeField] private TextMeshProUGUI[] optionLabels;

    private readonly List<MenuOption> options = new();
    private int selectedIndex;

    private struct MenuOption
    {
        public TextMeshProUGUI label;
        public Image background;
        public System.Action action;
    }

    private void Awake()
    {
        if (menu == null)
            menu = FindFirstObjectByType<MainMenuManager>();

        if (optionLabels == null || optionLabels.Length == 0)
            optionLabels = FindObjectsOfType<TextMeshProUGUI>(true);

        BuildOptions(optionLabels);
        UpdateSelection();
    }

    private void Update()
    {
        if (DialogueManager.IsBlockingGameplay) return;
        if (options.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            selectedIndex = (selectedIndex + 1) % options.Count;
            UpdateSelection();
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
            UpdateSelection();
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            options[selectedIndex].action?.Invoke();
        }
    }

    private void BuildOptions(TextMeshProUGUI[] labels)
    {
        options.Clear();

        foreach (var label in labels)
        {
            if (label == null) continue;

            string t = (label.text ?? string.Empty).Trim();
            if (!t.Equals("Start", System.StringComparison.OrdinalIgnoreCase) &&
                !t.Equals("Quit", System.StringComparison.OrdinalIgnoreCase))
                continue;

            Image bg = label.GetComponentInParent<Image>();
            System.Action action = null;
            if (menu != null)
            {
                if (t.Equals("Start", System.StringComparison.OrdinalIgnoreCase)) action = menu.StartGame;
                if (t.Equals("Quit", System.StringComparison.OrdinalIgnoreCase)) action = menu.QuitGame;
            }

            options.Add(new MenuOption { label = label, background = bg, action = action });
        }

        // Prefer Start first if present
        options.Sort((a, b) =>
        {
            string at = a.label != null ? a.label.text : "";
            string bt = b.label != null ? b.label.text : "";
            if (at == bt) return 0;
            if (at.Equals("Start", System.StringComparison.OrdinalIgnoreCase)) return -1;
            if (bt.Equals("Start", System.StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        });
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < options.Count; i++)
        {
            bool selected = i == selectedIndex;
            if (options[i].label != null)
            {
                options[i].label.color = selected ? new Color(1f, 0.92f, 0.3f, 1f) : Color.white;
                options[i].label.transform.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
            }

            if (options[i].background != null)
            {
                options[i].background.color = selected
                    ? new Color(0.15f, 0.15f, 0.18f, 0.9f)
                    : new Color(0.05f, 0.05f, 0.06f, 0.75f);
            }
        }
    }
}


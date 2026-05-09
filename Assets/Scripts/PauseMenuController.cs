using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenu;

    public TextMeshProUGUI[] options; // Resume, Restart, Main Menu

    [Header("Debug")]
    [SerializeField] private bool logToggle = false;

    int selectedIndex = 0;
    bool isPaused = false;

    void Start()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        UpdateSelection();
    }

    void Update()
    {
        // Sync with real state (fixes cases where PauseManager opens the menu).
        if (pauseMenu != null)
            isPaused = pauseMenu.activeSelf;

        // Toggle pause with ESC
        bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (escPressed)
        {
            if (logToggle) Debug.Log("PauseMenuController: ESC pressed");
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        if (!isPaused) return;

        // Move DOWN
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedIndex++;
            if (selectedIndex >= options.Length)
                selectedIndex = 0;

            UpdateSelection();
        }

        // Move UP
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = options.Length - 1;

            UpdateSelection();
        }

        // Press ENTER to select
        if (Input.GetKeyDown(KeyCode.Return))
        {
            ExecuteOption();
        }
    }

    void PauseGame()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(true);
        if (logToggle) Debug.Log($"PauseMenuController: pauseMenu active={pauseMenu != null && pauseMenu.activeSelf}");
        Time.timeScale = 0f;
        isPaused = true;
    }

    void ResumeGame()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    void ExecuteOption()
    {
        if (selectedIndex == 0)
        {
            ResumeGame();
        }
        else if (selectedIndex == 1)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else if (selectedIndex == 2)
        {
            // Confirm before leaving the game.
            DialogueManager dm = FindFirstObjectByType<DialogueManager>();
            if (dm != null)
            {
                dm.ShowYesNoChoice("Would you really like to quit to Main Menu?", yes =>
                {
                    if (yes)
                    {
                        Time.timeScale = 1f;
                        SceneManager.LoadScene("MainMenu");
                    }
                    // No: dialogue closes and we stay paused (pause menu stays open).
                });
            }
            else
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene("MainMenu");
            }
        }
    }

    void UpdateSelection()
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (i == selectedIndex)
            {
                options[i].color = Color.yellow;
                options[i].transform.localScale = Vector3.one * 1.2f; // selected bigger
            }
            else
            {
                options[i].color = Color.white;
                options[i].transform.localScale = Vector3.one; // normal size
            }
        }
    }
}
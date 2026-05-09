using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenu;

    bool isPaused = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f; // FREEZE GAME
        isPaused = true;
    }

    public void ResumeGame()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1f; // RESUME GAME
        isPaused = false;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
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
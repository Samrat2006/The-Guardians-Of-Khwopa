using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists across scenes (<see cref="DontDestroyOnLoad"/>): plays <see cref="menuMusic"/> in the main menu
/// and <see cref="gameMusic"/> in all other scenes. Place one instance in <b>MainMenu</b> (and optionally in the game scene
/// if you often hit Play starting from the game scene only).
/// </summary>
public class GameMusicManager : MonoBehaviour
{
    public static GameMusicManager Instance { get; private set; }

    [Header("Tracks")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;

    [Header("Rules")]
    [Tooltip("Scene name that uses menu music (must match File → Build Settings name, e.g. MainMenu).")]
    [SerializeField] private string menuSceneName = "MainMenu";

    [SerializeField] [Range(0f, 1f)] private float volume = 0.4f;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
        audioSource.ignoreListenerPause = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ApplyMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMusicForScene(scene.name);
    }

    private void ApplyMusicForScene(string sceneName)
    {
        bool isMenu = string.Equals(sceneName, menuSceneName, System.StringComparison.OrdinalIgnoreCase);
        AudioClip want = isMenu ? menuMusic : gameMusic;
        if (want == null)
            return;

        if (audioSource.clip == want && audioSource.isPlaying)
        {
            audioSource.volume = volume;
            return;
        }

        audioSource.clip = want;
        audioSource.volume = volume;
        audioSource.Play();
    }

    /// <summary>Optional: call from settings UI.</summary>
    public void SetMusicVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (audioSource != null)
            audioSource.volume = volume;
    }
}

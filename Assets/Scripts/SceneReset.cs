using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneReset : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(ResetScene());
    }

    System.Collections.IEnumerator ResetScene()
    {
        yield return null;

        Camera.main.ResetAspect();
        DynamicGI.UpdateEnvironment();
    }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// Destroys the GameObject after a delay using <b>real</b> time (ignores <see cref="Time.timeScale"/>).
/// Stops and clears child <see cref="ParticleSystem"/>s first so bursts do not linger in the world.
/// </summary>
public class DestroyAfterRealtime : MonoBehaviour
{
    [SerializeField] private float durationRealtime = 2f;

    public void SetDuration(float seconds) => durationRealtime = Mathf.Max(0.05f, seconds);

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(durationRealtime);
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(gameObject);
    }
}

using UnityEngine;

/// <summary>Shared particle burst spawn + auto destroy (realtime) for bow, Warrox super, etc.</summary>
public static class VfxLifetimeUtility
{
    public static void PlayParticleSystems(GameObject root)
    {
        if (root == null) return;
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Clear(true);
            ps.Simulate(0f, true, true);
            ps.Play(true);
        }
    }

    public static float EstimateParticleBurstLifetime(ParticleSystem[] systems)
    {
        if (systems == null || systems.Length == 0)
            return 2f;

        float best = 0.2f;
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            if (main.loop)
            {
                best = Mathf.Max(best, 1.75f);
                continue;
            }

            float d = main.duration;
            float lt = main.startLifetime.constantMax;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                lt = main.startLifetime.constant;

            best = Mathf.Max(best, d + lt + 0.15f);
        }

        return Mathf.Clamp(best, 0.25f, 8f);
    }

    public static void SpawnBurstAndDestroy(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float lifetimeSeconds,
        float maxLifetimeClamp)
    {
        if (prefab == null) return;

        GameObject vfx = Object.Instantiate(prefab, position, rotation);
        vfx.SetActive(true);
        PlayParticleSystems(vfx);

        ParticleSystem[] systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        float life = lifetimeSeconds;
        if (life <= 0f)
            life = EstimateParticleBurstLifetime(systems);

        float maxL = maxLifetimeClamp > 0f ? maxLifetimeClamp : 8f;
        life = Mathf.Clamp(life, 0.08f, maxL);

        if (!vfx.TryGetComponent<DestroyAfterRealtime>(out DestroyAfterRealtime cleaner))
            cleaner = vfx.AddComponent<DestroyAfterRealtime>();
        cleaner.SetDuration(life);
    }
}

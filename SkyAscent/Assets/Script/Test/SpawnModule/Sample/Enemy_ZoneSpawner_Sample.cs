using UnityEngine;

public class Enemy_ZoneSpawner_Sample : SpawnerBase
{
    [Header("Spawn Data")]
    [SerializeField] private SpawnKey EnemyKey;
    [SerializeField] private int count = 5;

    [Header("Volume")]
    [SerializeField] private Collider volume;

    [Header("Determinism")]
    [SerializeField] private uint seed = 1234;

    private void Reset()
    {
        volume = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        Spawn();
    }

    protected override SpawnRequest BuildRequest()
    {
        if (volume == null) volume = GetComponent<Collider>();
        ISpawnAlgorithm algo = new ColliderVolumeAlgorithm(volume, maxTryPerPoint: 24);

        return new SpawnRequest(
            key: EnemyKey,
            count: count,
            parent: transform,
            algorithm: algo,
            seed: seed
        );
    }
}

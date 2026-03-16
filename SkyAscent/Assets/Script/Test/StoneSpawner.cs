using UnityEngine;

public class StoneSpawner : SpawnerBase
{
    [Header("Spawn Data")]
    [SerializeField] private SpawnKey Key;
    [SerializeField] private int count = 20;

    [Header("Volume")]
    [SerializeField] private Collider volume;

    [Header("Spacing")]
    [SerializeField] private float minDistance = 0.8f; // khoảng cách tối thiểu giữa các point (để tránh spawn chồng)
    [SerializeField] private int candidatesPerPoint = 24; // số lượng point random thử cho mỗi point cần spawn (tăng sẽ có vị trí tốt hơn nhưng chậm hơn)

    [Header("Determinism")]
    [SerializeField] private uint seed = 0;

    private ColliderVolumeAlgorithm _algo;

    private void Reset()
    {
        volume = GetComponent<Collider>();
    }

    private void Start()
    {
         Spawn();
    }

    protected override SpawnRequest BuildRequest()
    {
        if (volume == null) volume = GetComponent<Collider>();

        uint s = seed;
        if (s == 0)
            s = SeedResolver.Resolve(0, SeedMode.ContextStable, Key.Hash, gameObject.GetInstanceID());

        // cache algo + reset placed
        _algo ??= new ColliderVolumeAlgorithm(
            collider: volume,
            maxTryPerPoint: 24,
            candidatesPerPoint: candidatesPerPoint,
            minDistance: minDistance,
            maxCount: 512
        );
        _algo.ResetPlaced();

        return new SpawnRequest(Key, count, transform, _algo, s);
    }
}

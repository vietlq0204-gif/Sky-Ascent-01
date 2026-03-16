using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Request spawn dạng batch.
/// </summary>
public readonly struct SpawnRequest
{
    public readonly SpawnKey key;
    public readonly int count;
    public readonly Transform parent;
    public readonly ISpawnAlgorithm algorithm;
    public readonly uint seed;

    public SpawnRequest(SpawnKey key, int count, Transform parent, ISpawnAlgorithm algorithm, uint seed = 0)
    {
        this.key = key;
        this.count = count;
        this.parent = parent;
        this.algorithm = algorithm;
        this.seed = seed;
    }
}

/// <summary>
/// Handle giữ các instance đã spawn để despawn hàng loạt.
/// </summary>
public sealed class SpawnHandle : IDisposable
{
    private readonly SpawnManager _manager;
    private readonly List<GameObject> _instances;

    public IReadOnlyList<GameObject> Instances => _instances;

    internal SpawnHandle(SpawnManager manager, List<GameObject> instances)
    {
        _manager = manager;
        _instances = instances;
    }

    /// <summary>
    /// Despawn toàn bộ instance của handle.
    /// </summary>
    public void DespawnAll()
    {
        if (_manager == null) return;
        for (int i = 0; i < _instances.Count; i++)
        {
            _manager.Despawn(_instances[i]);
        }
        _instances.Clear();
    }

    public void Dispose()
    {
        DespawnAll();
    }
}
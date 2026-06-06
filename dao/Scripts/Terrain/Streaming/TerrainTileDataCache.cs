using System.Collections.Generic;
using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

internal sealed class TerrainTileDataCache
{
    private readonly Dictionary<TerrainTileCacheKey, TerrainTileData> _tileCache = new();
    private readonly Dictionary<TerrainTileCacheKey, LinkedListNode<TerrainTileCacheKey>> _tileCacheNodes = new();
    private readonly LinkedList<TerrainTileCacheKey> _tileCacheLru = new();

    internal int Count => _tileCache.Count;

    internal TerrainTileData? TryGet(TerrainTileCacheKey key)
    {
        if (!_tileCache.TryGetValue(key, out TerrainTileData? tileData))
        {
            return null;
        }

        Touch(key);
        return tileData;
    }

    internal bool Store(TerrainTileData data, TerrainTileCacheKey key, int limit)
    {
        int safeLimit = Mathf.Max(0, limit);
        if (safeLimit == 0)
        {
            return false;
        }

        if (_tileCache.ContainsKey(key))
        {
            _tileCache[key] = data;
            Touch(key);
            return true;
        }

        _tileCache[key] = data;
        _tileCacheNodes[key] = _tileCacheLru.AddLast(key);
        bool changed = true;

        while (_tileCache.Count > safeLimit && _tileCacheLru.First is not null)
        {
            TerrainTileCacheKey oldest = _tileCacheLru.First.Value;
            _tileCacheLru.RemoveFirst();
            _tileCacheNodes.Remove(oldest);
            _tileCache.Remove(oldest);
            changed = true;
        }

        return changed;
    }

    internal bool Clear()
    {
        if (_tileCache.Count == 0 && _tileCacheNodes.Count == 0 && _tileCacheLru.Count == 0)
        {
            return false;
        }

        _tileCache.Clear();
        _tileCacheNodes.Clear();
        _tileCacheLru.Clear();
        return true;
    }

    private void Touch(TerrainTileCacheKey key)
    {
        if (!_tileCacheNodes.TryGetValue(key, out LinkedListNode<TerrainTileCacheKey>? node))
        {
            return;
        }

        _tileCacheLru.Remove(node);
        _tileCacheLru.AddLast(node);
    }
}

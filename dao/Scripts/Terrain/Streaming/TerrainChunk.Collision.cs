using Dao.Terrain.Generation;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainChunk
{
    private void RebuildCollision(TerrainTileData data)
    {
        if (_staticBody is not null)
        {
            _staticBody.QueueFree();
            _staticBody = null;
        }

        if (data.CollisionFaces.Length == 0)
        {
            return;
        }

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(data.CollisionFaces);
        shape.BackfaceCollision = false;

        var collisionShape = new CollisionShape3D
        {
            Name = "CollisionShape",
            Shape = shape
        };

        _staticBody = new StaticBody3D { Name = "Collision" };
        _staticBody.AddChild(collisionShape);
        AddChild(_staticBody);
    }
}

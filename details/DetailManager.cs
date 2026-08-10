[GlobalClass]
public partial class DetailManager : Node
{
    #nullable disable
    public static World World;
    #nullable enable
    public static CircleCollider[] CircleColliders = [];

    public const int DETAILED_CHUNKS_AROUND_PLAYER = 2;

    public override void _PhysicsProcess(double delta)
    {
        CreateCircleColliders();
    }

    public static void StartingArea()
    {
        ProceduralGenerator.Mutex.Lock();
        for (int x = -DETAILED_CHUNKS_AROUND_PLAYER; x <= DETAILED_CHUNKS_AROUND_PLAYER; x++)
		for (int y = -DETAILED_CHUNKS_AROUND_PLAYER; y <= DETAILED_CHUNKS_AROUND_PLAYER; y++)
			PlaceDetails(new(x,y));
        ProceduralGenerator.Mutex.Unlock();
    }

    public static void PlayerCrossedChunkBoundary(Vector2I to, Vector2I from)
	{
        Vector2I direction = to - from;
        static Vector2I RotateCCW(Vector2I v) => new(-v.Y, v.X);

        ProceduralGenerator.Mutex.Lock();
		for (int h = -DETAILED_CHUNKS_AROUND_PLAYER; h <= DETAILED_CHUNKS_AROUND_PLAYER; h++) {
            PlaceDetails(to + direction * DETAILED_CHUNKS_AROUND_PLAYER + RotateCCW(direction) * h);
        }
        ProceduralGenerator.Mutex.Unlock();
	}

    static T PlaceDetail<T>(Vector2 position) where T : Node2D, IDetail<T>
    {
        T detail = T.Scene.Instantiate<T>();
        T.Instances[T.IdIterator] = detail;
        detail.Id = T.IdIterator++;
        detail.Position = position;
        World.AddChild(detail);
        return detail;
    }

    static void PlaceDetails(Vector2I chunk)
    {
        if (!ProceduralGenerator.ChunkStates.TryGetValue(chunk, out ProceduralGenerator.ChunkState value) || value == ProceduralGenerator.ChunkState.Detailed) return;
        for (int i = 0; i < 20; i++) {
            Vector2I tile = chunk * ProceduralGenerator.CONVERTED_CHUNK_SIZE + new Vector2I(Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE), Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE));
            if (!World.InteriorTile(tile)) continue;
            if (!World.SolidTile(tile + new Vector2I(0,-1))) continue;
            Vector2 position = (tile + new Vector2(0.5f,0.5f)) * World.CONVERTED_TILE_SIZE;
            foreach (VineGroup checkGroup in VineGroup.Instances.Values) if (position.DistanceSquaredTo(checkGroup.Position) < 80000) goto cont;
            PlaceDetail<VineGroup>(position);
            cont:;
        }
        for (int i = 0; i < 5; i++)
        {
            Vector2I tile = chunk * ProceduralGenerator.CONVERTED_CHUNK_SIZE + new Vector2I(Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE), Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE));
            if (!World.SolidTile(tile)) continue;
            float rotation;
            if (World.InteriorTile(tile + new Vector2I(0,-1))) rotation = 0;
            else if (World.InteriorTile(tile + new Vector2I(1,0))) rotation = PI/2;
            else if (World.InteriorTile(tile + new Vector2I(0,1))) rotation = PI;
            else if (World.InteriorTile(tile + new Vector2I(-1,0))) rotation = -PI/2;
            else continue;
            Vector2 position = (tile + new Vector2(0.5f,0.5f)) * World.CONVERTED_TILE_SIZE;
            PlaceDetail<Shelter>(position).Rotation = rotation;
        }
        ProceduralGenerator.ChunkStates[chunk] = ProceduralGenerator.ChunkState.Detailed;
    }

    static void CreateCircleColliders()
    {
        static IEnumerable<CircleCollider> GetCreatureColliders<T>() where T : Node2D, ICreature<T> {
            return T.Creatures.Values.Where(c=>c.CollisionRadius != 0).Select(c=>new CircleCollider(c.Position, c.CollisionRadius));
        }
        if (!World.Player.Visible) CircleColliders = [..GetCreatureColliders<Aawaga>(), ..GetCreatureColliders<Spider>()];
        else CircleColliders = [..GetCreatureColliders<Aawaga>(), ..GetCreatureColliders<Spider>(), new CircleCollider(World.Player.Position, 15)];
    }
}

public interface IDetail<T> where T:IDetail<T>
{
    public static abstract PackedScene Scene {get; set;}
    public static abstract Dictionary<int, T> Instances {get; set;}
    public static abstract int IdIterator {get; set;}
    public int Id {get; set;}
}

public struct CircleCollider(Vector2 Position, float Radius)
{
    public Vector2 Position = Position;
    public float Radius = Radius;
}

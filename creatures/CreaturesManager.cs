using System.Text.RegularExpressions;

[GlobalClass]
public partial class CreaturesManager : Node
{
    #nullable disable
    public static World World;
    #nullable enable

    public const int CREATURED_CHUNKS_AROUND_PLAYER = 4;

    public static void StartingArea()
    {
        for (int x = -CREATURED_CHUNKS_AROUND_PLAYER; x <= CREATURED_CHUNKS_AROUND_PLAYER; x++)
		for (int y = -CREATURED_CHUNKS_AROUND_PLAYER; y <= CREATURED_CHUNKS_AROUND_PLAYER; y++)
			SpawnCreatures(new(x,y));
    }

    public static void PlayerCrossedChunkBoundary(Vector2I to, Vector2I from)
	{
        Vector2I direction = to - from;
        static Vector2I RotateCCW(Vector2I v) => new(-v.Y, v.X);

		for (int h = -CREATURED_CHUNKS_AROUND_PLAYER; h <= CREATURED_CHUNKS_AROUND_PLAYER; h++) {
            DespawnCreatures(to + direction * -CREATURED_CHUNKS_AROUND_PLAYER + RotateCCW(direction) * h);
            SpawnCreatures(to + direction * CREATURED_CHUNKS_AROUND_PLAYER + RotateCCW(direction) * h);
        }
	}

    public static void CreatureMoved<T>(T creature) where T : Node2D, ICreature<T>
    {
        Vector2I toPlayerChunk = World.PositionToChunk(creature.Position) - World.Player.CurrentChunk;
        if (Math.Abs(toPlayerChunk.X) > CREATURED_CHUNKS_AROUND_PLAYER || Math.Abs(toPlayerChunk.Y) > CREATURED_CHUNKS_AROUND_PLAYER)
        {
            RemoveCreature(creature);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        FloodFillAawagas();
    }

    public static void SpawnCreature<T>(Vector2 position) where T : Node2D, ICreature<T>
	{
		T creature = T.Scene.Instantiate<T>();
        T.Creatures[T.IdIterator] = creature;
        creature.Id = T.IdIterator++;
        creature.Position = position;
		World.AddChild(creature);
	}

	public static void RemoveCreature<T>(T creature) where T : Node2D, ICreature<T>
	{
		creature.QueueFree();
        T.Creatures.Remove(creature.Id);
	}

    static void FloodFillAawagas()
	{
		foreach (Aawaga creature in Aawaga.Creatures.Values) {
            creature.FloodFilled = false;
            creature.ConnectedToSurface = false;
        }
        
        static bool TouchingSurface(Aawaga creature) {
            foreach (Node2D body in creature.GetCollidingBodies())
                if (body is TileMapLayer) return true;
            return false;
        }

        static void FloodFill(Aawaga creature)
        {
            if(creature.FloodFilled) return;
            creature.FloodFilled = true;
            creature.ConnectedToSurface = true;
            foreach (Node2D body in creature.GetCollidingBodies())
                if (body is Aawaga next) FloodFill(next);
        }
        
        foreach (Aawaga creature in Aawaga.Creatures.Values)
            if (!creature.FloodFilled && TouchingSurface(creature)) FloodFill(creature);
	}

    static void SpawnCreatures(Vector2I chunk)
    {
        for (int i = 0; i < 2; i++)
        {
            Vector2I tile = chunk * ProceduralGenerator.CONVERTED_CHUNK_SIZE + new Vector2I(Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE), Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE));
            if (!World.InteriorTile(tile)) continue;
            Vector2 position = tile * World.CONVERTED_TILE_SIZE + Vector2.One * World.CONVERTED_TILE_SIZE * 0.5f;
            switch (Game.RNG.Range(0,3))  {
                case < 2: SpawnCreature<Aawaga>(position); break;
                default: SpawnCreature<Spider>(position); break;
            };
        }
    }

    static void DespawnCreatures(Vector2I chunk)
    {
        void DespawnType<T>() where T : Node2D, ICreature<T> {
            foreach (T creature in T.Creatures.Values)
                if (World.PositionToChunk(creature.Position) == chunk) RemoveCreature(creature);
        }
        DespawnType<Aawaga>();
        DespawnType<Spider>();
    }
}

public interface ICreature<T> where T:ICreature<T>
{
    public static abstract PackedScene Scene {get; set;}
    public static abstract Dictionary<int, T> Creatures {get; set;}
    public static abstract int IdIterator {get; set;}
    public int Id {get; set;}

    public abstract float CollisionRadius {get; set;}
}

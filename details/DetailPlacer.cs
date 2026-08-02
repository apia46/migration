[GlobalClass]
public partial class DetailPlacer : Node
{
    #nullable disable
    public static World World;
    #nullable enable

    public const int DETAILED_CHUNKS_AROUND_PLAYER = 2;

    public static void StartingArea()
    {
        for (int x = -DETAILED_CHUNKS_AROUND_PLAYER; x <= DETAILED_CHUNKS_AROUND_PLAYER; x++)
		for (int y = -DETAILED_CHUNKS_AROUND_PLAYER; y <= DETAILED_CHUNKS_AROUND_PLAYER; y++)
			PlaceDetails(new(x,y));
    }

    static void PlaceDetail<T>(Vector2 position) where T : Node2D, IDetail<T>
    {
        T detail = T.Scene.Instantiate<T>();
        T.Instances[T.IdIterator] = detail;
        detail.Id = T.IdIterator++;
        detail.Position = position;
        World.AddChild(detail);
    }

    static void PlaceDetails(Vector2I chunk)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2I tile = chunk * ProceduralGenerator.CONVERTED_CHUNK_SIZE + new Vector2I(Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE), Game.RNG.Range(0, ProceduralGenerator.CONVERTED_CHUNK_SIZE));
            if (!World.InteriorTile(tile)) continue;
            if (!World.SolidTile(tile + new Vector2I(0,-1))) continue;
            Vector2 position = tile * World.CONVERTED_TILE_SIZE + Vector2.One * World.CONVERTED_TILE_SIZE * 0.5f;
            PlaceDetail<Vine>(position);
        }
    }
}

public interface IDetail<T> where T:IDetail<T>
{
    public static abstract PackedScene Scene {get; set;}
    public static abstract Dictionary<int, T> Instances {get; set;}
    public static abstract int IdIterator {get; set;}
    public int Id {get; set;}
}

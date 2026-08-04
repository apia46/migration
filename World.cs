[GlobalClass]
public partial class World : Node2D
{
	#nullable disable
	public static TileMapLayer ConvertedTileMapLayer;
	public static ProceduralGenerator ProceduralGenerator;
	public static Camera2D Camera;
	public static Player Player;
	public static CreaturesManager CreaturesManager;
	public static DetailManager DetailManager;
	#nullable enable
	static double time = 0;

	public override void _Ready()
	{
		ProceduralGenerator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		Camera = GetNode<Camera2D>("%Camera");
		Player = GetNode<Player>("%Player");
		Player.World = this;
		CreaturesManager = GetNode<CreaturesManager>("%CreaturesManager");
		CreaturesManager.World = this;
		DetailManager = GetNode<DetailManager>("%DetailManager");
		DetailManager.World = this;
		ConvertedTileMapLayer = GetNode<TileMapLayer>("%ConvertedTileMapLayer");
		ProceduralGenerator.SetContext(GetNode<TileMapLayer>("%PatternTileMapLayer"), ConvertedTileMapLayer, GD.Load<ModelResource>("res://procedural_generation/model.tres").ToModel());
		ProceduralGenerator.StartingArea();
	}

    public override void _Process(double delta)
    {
        time += delta;
    }

	public static Vector2I PositionToChunk(Vector2 position) => (Vector2I)(position / PATTERN_TILE_SIZE / ProceduralGenerator.PATTERN_CHUNK_SIZE).Floor();
	public static Vector2I PositionToConvertedTile(Vector2 position) => (Vector2I)(position / CONVERTED_TILE_SIZE).Floor();

	public static void InitialProcGenFinished()
	{
		GD.Print(time);
		DetailManager.StartingArea();
		CreaturesManager.StartingArea();
	}

	 public static void PlayerCrossedChunkBoundary(Vector2I to, Vector2I from)
	{
		DetailManager.PlayerCrossedChunkBoundary(to,from);
		CreaturesManager.PlayerCrossedChunkBoundary(to,from);
	}

	public const int PATTERN_TILE_SIZE = 64;
	public const int CONVERTED_TILE_SIZE = 32;

	public static bool InteriorTile(Vector2I tile)
	{
		Vector2I tileAtlas = ConvertedTileMapLayer.GetCellAtlasCoords(tile);
        return ProceduralGenerator.Model.ConvertedTiles.Convert(tileAtlas) switch {
            3 or 4 or 7 or 8 => false,
            _ => true,
        };
    }
	public static bool SolidTile(Vector2I tile)
	{
		Vector2I tileAtlas = ConvertedTileMapLayer.GetCellAtlasCoords(tile);
        return ProceduralGenerator.Model.ConvertedTiles.Convert(tileAtlas) switch {
            3  => true,
            _ => false,
        };
    }

	public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle")) {	
			GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled = !GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled;
			GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled = !GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled;
		} else if (@event.IsActionPressed("spawn")) {	
			CreaturesManager.SpawnCreature<Aawaga>(Player.Position + new Vector2(30,-30));
		}
	}
}

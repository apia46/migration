[GlobalClass]
public partial class World : Node2D
{
	#nullable disable
	public TileMapLayer ConvertedTileMapLayer;
	public ProceduralGenerator ProceduralGenerator;
	public Camera2D Camera;
	public Player Player;
	public CreaturesManager CreaturesManager;
	#nullable enable

	public override void _Ready()
	{
		ProceduralGenerator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		ProceduralGenerator.World = this;
		Camera = GetNode<Camera2D>("%Camera");
		Player = GetNode<Player>("%Player");
		Player.World = this;
		CreaturesManager = GetNode<CreaturesManager>("%CreaturesManager");
		CreaturesManager.World = this;
		ConvertedTileMapLayer = GetNode<TileMapLayer>("%ConvertedTileMapLayer");
		ProceduralGenerator.SetContext(GetNode<TileMapLayer>("%PatternTileMapLayer"), ConvertedTileMapLayer, GD.Load<ModelResource>("res://procedural_generation/model.tres").ToModel());
		ProceduralGenerator.StartingArea();
	}

	public static Vector2I PositionToChunk(Vector2 position) => (Vector2I)(position / PATTERN_TILE_SIZE / ProceduralGenerator.PATTERN_CHUNK_SIZE).Floor();

	public void InitialProcGenFinished()
	{
		CreaturesManager.StartingArea();
	}

	public const int PATTERN_TILE_SIZE = 64;
	public const int CONVERTED_TILE_SIZE = 32;

	public bool InteriorTile(Vector2I tile)
	{
		Vector2I tileAtlas = ConvertedTileMapLayer.GetCellAtlasCoords(tile);
        return ProceduralGenerator.Model.ConvertedTiles.Convert(tileAtlas) switch {
            3 or 4 or 7 or 8 => false,
            _ => true,
        };
    }

	public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle")) {	
			GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled = !GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled;
			GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled = !GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled;
		} else if (@event.IsActionPressed("spawn")) {	
			CreaturesManager.SpawnCreature<Spider>(Player.Position + new Vector2(30,-30));
		}
	}
}

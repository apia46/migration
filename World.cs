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
		ProceduralGenerator.world = this;
		Camera = GetNode<Camera2D>("%Camera");
		Player = GetNode<Player>("%Player");
		Player.World = this;
		CreaturesManager = GetNode<CreaturesManager>("%CreaturesManager");
		CreaturesManager.World = this;
		ConvertedTileMapLayer = GetNode<TileMapLayer>("%ConvertedTileMapLayer");
		ProceduralGenerator.SetContext(GetNode<TileMapLayer>("%PatternTileMapLayer"), ConvertedTileMapLayer, GD.Load<ModelResource>("res://procedural_generation/model.tres").ToModel());
		ProceduralGenerator.AddToQueue(Vector2I.Zero, false);
		for (int i = 0; i < 4; i++) NextChunks(3);
		for (int i = 0; i < 4; i++) NextChunks(5);
		ProceduralGenerator.QueueEmpty += NextChunks;
	}

	const int GENERATE_CHUNKS_AROUND_PLAYER = 8;
	const int UNSTABLE_CHUNKS_THRESHOLD = 9;
	public const int TILE_SIZE = 64;
	
	void NextChunks() => NextChunks(GENERATE_CHUNKS_AROUND_PLAYER);
	void NextChunks(int chunks)
	{
		const int CHUNK_SIZE = ProceduralGenerator.CHUNK_SIZE;
		Vector2I position = (Vector2I)(Player.Position / CHUNK_SIZE / TILE_SIZE).Round();
		for (int layer = chunks; layer > 0; layer--) {
			bool unstable = layer >= UNSTABLE_CHUNKS_THRESHOLD;
			for (int x = 0; x < layer*2; x++) {
				ProceduralGenerator.AddToQueue(position + new Vector2I(layer,layer-x), unstable && Game.RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(layer-x,-layer), unstable && Game.RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(-layer,x-layer), unstable && Game.RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(x-layer,layer), unstable && Game.RNG.NextDouble()*4 < Player.Stillness);
			}
		}
		ProceduralGenerator.AddToQueue(position, false);
	}

	public bool InteriorTile(Vector2I tile)
	{
        return ProceduralGenerator.Model.ConvertedTiles.Convert(tile) switch {
            3 | 4 | 7 | 8 => false,
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

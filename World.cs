[GlobalClass]
public partial class World : Node2D
{
	#nullable disable
	public ProceduralGenerator ProceduralGenerator;
	public Player Player;
	public CreaturesManager CreaturesManager;
	#nullable enable
	readonly GameRandom RNG = new();

	public override void _Ready()
	{
		ProceduralGenerator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		ProceduralGenerator.world = this;
		Player = GetNode<Player>("%Player");
		CreaturesManager = GetNode<CreaturesManager>("%CreaturesManager");
		CreaturesManager.World = this;
		ProceduralGenerator.SetContext(GetNode<TileMapLayer>("%PatternTileMapLayer"), GetNode<TileMapLayer>("%ConvertedTileMapLayer"), GD.Load<ModelResource>("res://procedural_generation/model.tres").ToModel());
		ProceduralGenerator.AddToQueue(Vector2I.Zero, false);
		for (int i = 0; i < 4; i++) NextChunks(3);
		for (int i = 0; i < 4; i++) NextChunks(5);
		ProceduralGenerator.QueueEmpty += NextChunks;
	}

	const int GENERATE_CHUNKS_AROUND_PLAYER = 8;
	const int UNSTABLE_CHUNKS_THRESHOLD = 9;
	const int TILE_SIZE = 64;
	
	void NextChunks() => NextChunks(GENERATE_CHUNKS_AROUND_PLAYER);
	void NextChunks(int chunks)
	{
		const int CHUNK_SIZE = ProceduralGenerator.CHUNK_SIZE;
		Vector2I position = (Vector2I)(Player.Position / CHUNK_SIZE / TILE_SIZE).Round();
		for (int layer = chunks; layer > 0; layer--) {
			bool unstable = layer >= UNSTABLE_CHUNKS_THRESHOLD;
			for (int x = 0; x < layer*2; x++) {
				ProceduralGenerator.AddToQueue(position + new Vector2I(layer,layer-x), unstable && RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(layer-x,-layer), unstable && RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(-layer,x-layer), unstable && RNG.NextDouble()*4 < Player.Stillness);
				ProceduralGenerator.AddToQueue(position + new Vector2I(x-layer,layer), unstable && RNG.NextDouble()*4 < Player.Stillness);
			}
		}
		ProceduralGenerator.AddToQueue(position, false);
	}

	public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle")) {	
			GetNode<TileMapLayer>("%TileMapLayer").Enabled = !GetNode<TileMapLayer>("%TileMapLayer").Enabled;
			GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled = !GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled;
		} else if (@event.IsActionPressed("spawn")) {	
			CreaturesManager.SpawnCreature(Player.Position + new Vector2(30,-30));
		}
	}
}

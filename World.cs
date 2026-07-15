using Godot;
using System;
using System.Linq;

[GlobalClass]
public partial class World : Node2D
{
	ProceduralGenerator? generator;
	Line2D? debugDraw;
	public Player? player;
	readonly Random RNG = new();

	PackedScene AawagaScene = GD.Load<PackedScene>("aawaga.tscn");

	public override void _Ready()
	{
		generator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		debugDraw = GetNode<Line2D>("%Line2D");
		player = GetNode<Player>("%player");
		// generator.world = this;
		generator.SetContext(GetNode<TileMapLayer>("%TileMapLayer"), GetNode<TileMapLayer>("%ConvertedTileMapLayer"), GD.Load<ModelResource>("res://procedural_generation/model.tres").ToModel());
		generator.world = this;
		generator.AddToQueue(Vector2I.Zero, false);
		generator.QueueEmpty += NextChunks;
	}

	const int GENERATE_CHUNKS_AROUND_PLAYER = 8;
	const int UNSTABLE_CHUNKS_THRESHOLD = 7;
	const int TILE_SIZE = 64;

	void NextChunks()
	{
		const int CHUNK_SIZE = ProceduralGenerator.CHUNK_SIZE;
		Vector2I position = (Vector2I)(player!.Position / CHUNK_SIZE / TILE_SIZE).Round();
		for (int layer = GENERATE_CHUNKS_AROUND_PLAYER; layer > 0; layer--) {
			bool unstable = layer >= UNSTABLE_CHUNKS_THRESHOLD;
			for (int x = 0; x < layer*2; x++) {
				generator!.AddToQueue(position + new Vector2I(layer,layer-x), unstable && RNG.NextDouble() < player.Stillness);
				generator.AddToQueue(position + new Vector2I(layer-x,-layer), unstable && RNG.NextDouble() < player.Stillness);
				generator.AddToQueue(position + new Vector2I(-layer,x-layer), unstable && RNG.NextDouble() < player.Stillness);
				generator.AddToQueue(position + new Vector2I(x-layer,layer), unstable && RNG.NextDouble() < player.Stillness);
			}
		}
		generator!.AddToQueue(position, false);
	}

	public void DrawDebug(Rect2I rect)
	{
		debugDraw!.SetPointPosition(0, rect.Position * TILE_SIZE);
		debugDraw.SetPointPosition(1, new Vector2(rect.End.X, rect.Position.Y) * TILE_SIZE);
		debugDraw.SetPointPosition(2, rect.End * TILE_SIZE);
		debugDraw.SetPointPosition(3, new Vector2(rect.Position.X, rect.End.Y) * TILE_SIZE);
	}

	public void SpawnCreature(Vector2 position)
	{
		Aawaga aawaga = AawagaScene.Instantiate<Aawaga>();
		aawaga.Player = player;
		AddChild(aawaga);
		aawaga.Position = position * TILE_SIZE;
	}

	public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle")) {	
			GetNode<TileMapLayer>("%TileMapLayer").Enabled = !GetNode<TileMapLayer>("%TileMapLayer").Enabled;
			GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled = !GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled;
		}
	}
}

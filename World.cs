using Godot;
using System;
using System.Linq;

[GlobalClass]
public partial class World : Node2D
{
	ProceduralGenerator generator;
	Line2D debugDraw;
	public CharacterBody2D player;

	public override void _Ready()
	{
		generator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		debugDraw = GetNode<Line2D>("%Line2D");
		player = GetNode<CharacterBody2D>("%player");
		generator.world = this;
		generator.SetContext(GetNode<TileMapLayer>("%TileMapLayer"), GD.Load<Model>("res://procedural_generation/model.tres"));
		generator.AddToQueue(ChunkAt(Vector2I.Zero));
		generator.QueueEmpty += NextChunks;
	}

	const int CHUNKS_AROUND_PLAYER = 4;
	const int TILE_SIZE = 64;
	const int CHUNK_SIZE = 8;

	void NextChunks()
	{
		Vector2I position = (Vector2I)(player.Position / CHUNK_SIZE / TILE_SIZE);
		for (int layer = CHUNKS_AROUND_PLAYER; layer > 0; layer--) {
			for (int x = 0; x < layer*2; x++) {
				generator.AddToQueue(ChunkAt(position + new Vector2I(layer,layer-x)));
				generator.AddToQueue(ChunkAt(position + new Vector2I(layer-x,-layer)));
				generator.AddToQueue(ChunkAt(position + new Vector2I(-layer,x-layer)));
				generator.AddToQueue(ChunkAt(position + new Vector2I(x-layer,layer)));
			}
		}
		generator.AddToQueue(ChunkAt(position));
	}

	Rect2I ChunkAt(Vector2I position)
	{
		return new Rect2I(position * CHUNK_SIZE, Vector2I.One * CHUNK_SIZE);
	}

	public void DrawDebug(Rect2I rect)
	{
		debugDraw.SetPointPosition(0, rect.Position * TILE_SIZE);
		debugDraw.SetPointPosition(1, new Vector2(rect.End.X, rect.Position.Y) * TILE_SIZE);
		debugDraw.SetPointPosition(2, rect.End * TILE_SIZE);
		debugDraw.SetPointPosition(3, new Vector2(rect.Position.X, rect.End.Y) * TILE_SIZE);
	}
}

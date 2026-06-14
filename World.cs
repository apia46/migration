using Godot;
using System;
using System.Linq;

[GlobalClass]
public partial class World : Node2D
{
	ProceduralGenerator generator;
	int layer = 0;

	public override void _Ready()
	{
		generator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		generator.SetContext(GetNode<TileMapLayer>("%TileMapLayer"), GD.Load<Model>("res://procedural_generation/model.tres"));
		generator.Queue.Push(ChunkAt(Vector2I.Zero));
		generator.QueueEmpty += NextChunks;
	}

	void NextChunks()
	{
		//if (layer == 6) return;
		layer++;
		for (int x = 0; x < layer*2; x++) {
			generator.Queue.Push(ChunkAt(new Vector2I(layer,layer-x)));
			generator.Queue.Push(ChunkAt(new Vector2I(layer-x,-layer)));
			generator.Queue.Push(ChunkAt(new Vector2I(-layer,x-layer)));
			generator.Queue.Push(ChunkAt(new Vector2I(x-layer,layer)));
		}
	}

	Rect2I ChunkAt(Vector2I position)
	{
		return new Rect2I(position * 8, Vector2I.One * 8);
	}
}

using Godot;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class Generator : Node
{
	[Signal]
	public delegate void QueueEmptyEventHandler();

	TileMapLayer tileMapLayer;
	Model model;

	Stack<Rect2I> queue = [];
	
	Rect2I initialGenRect;
	Rect2I genRect;
	int[,,] tilePossibilities; // position, overlap, tile
	int[] entropies; // position
	bool failed = false;
	int fails = 0;
	bool[] tilesChanged;
	int tilesCompleted;

	int lowestIndex;
	Vector2I lowestPosition;

	enum Result { Next, Retry, Advance }
	Result nextTick = Result.Next;

	public void SetContext(TileMapLayer tileMapLayer, Model model)
	{
		this.tileMapLayer = tileMapLayer;
		this.model = model;
		model.ImportProperties();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	void Tick()
	{
		switch (nextTick) {
			case Result.Next:
				Rect2I? next = queue.Pop();
				if (next is Rect2I nextRect) {
					fails = 0;
					initialGenRect = nextRect;
					nextTick = Setup(nextRect);
				} else EmitSignalQueueEmpty();
			break;
			case Result.Retry:

			break;
			case Result.Advance:

			break;
		}
	}

	Result Setup(Rect2I genRect)
	{
		this.genRect = genRect;
		tilePossibilities = new int[
			genRect.Area,
			model.BasePatternSize.X*model.BasePatternSize.Y,
			model.TileAtlasCoords.Count
		];
		entropies = new int[genRect.Area];
		tilesChanged = new bool[genRect.Area];
		tilesCompleted = 0;

		for (int y = 0; y < genRect.Size.Y; y++) {
			for (int x = 0; x < genRect.Size.X; x++) {
				int index = y*genRect.Size.X + x;
				Vector2I position = new(x, y);
				if (tileMapLayer.GetCellAtlasCoords(position + genRect.Position) != Vector2I.One * -1) {
					// tile already filled
					entropies[index] = -1;
					tilesCompleted++;
					continue;
				}
				
				for (int py = 0; py < model.BasePatternSize.Y; py++) {
					for (int px = 0; px < model.BasePatternSize.X; px++) {
						Vector2I patternOffset = new(px, py);
						for (int p = 0; p < model.BasePatterns.Count(); p++) {
							int[] pattern = model.BasePatterns[p];
							if (Matches(pattern, position-patternOffset+genRect.Position)) {
								int tile = pattern[py*model.BasePatternSize.X+px];
								
							}
						}
					}
				}
			}
		}

		return Result.Advance;
	}

	bool Matches(int[] pattern, Vector2I position)
	{
		return true;
	}
}

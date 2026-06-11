using Godot;
using System;
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
	
	Rect2I genRect;
	TileCache tileCache;
	int[,,,,] tilePossibilities; // position, overlap, tile
	int[,] entropies; // position
	bool failed = false;
	int fails = 0;
	bool[,] tilesChanged;
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
		for (int i = 0; i < 10; i++) Tick();
	}

	void Tick()
	{
		switch (nextTick) {
			case Result.Next:
				Rect2I? next = queue.Pop();
				if (next is Rect2I nextRect) {
					fails = 0;
					tileCache = new(
						nextRect, model.BasePatternSize-Vector2I.One,
						tileMapLayer, model.TileAtlasCoordsMap);
					genRect = nextRect;
					nextTick = Setup();
				} else EmitSignalQueueEmpty();
			break;
			case Result.Retry:
				fails++;
				if (fails % 6 == 1) {
					genRect.Position -= Vector2I.One;
					genRect.Size += Vector2I.One * 2;
					tileCache = new(tileCache, Vector2I.One, tileMapLayer, model.TileAtlasCoordsMap);
				}
				nextTick = Setup();
			break;
			case Result.Advance:
				nextTick = Advance();
			break;
		}
	}

	Result Setup()
	{
		tilePossibilities = new int[
			genRect.Size.X,
			genRect.Size.Y,
			model.BasePatternSize.X,
			model.BasePatternSize.Y,
			model.TileAtlasCoords.Count
		];
		entropies = new int[genRect.Size.X, genRect.Size.Y];
		tilesChanged = new bool[genRect.Size.X, genRect.Size.Y];
		tilesCompleted = 0;

		for (int y = 0; y < genRect.Size.Y; y++) {
			for (int x = 0; x < genRect.Size.X; x++) {
				Vector2I position = new(x, y);
				if (tileCache.GetTile(position + genRect.Position) != -1) {
					// tile already filled
					entropies[x, y] = -1;
					tilesCompleted++;
					continue;
				}
				
				for (int py = 0; py < model.BasePatternSize.Y; py++) {
					for (int px = 0; px < model.BasePatternSize.X; px++) {
						Vector2I patternOffset = new(px, py);
						for (int p = 0; p < model.BasePatterns.Count; p++) {
							int[,] pattern = model.BasePatterns[p];
							if (Matches(pattern, position-patternOffset+genRect.Position)) {
								int tile = pattern[px, py];
								tilePossibilities[
									x, y, px, py, tile
								] += model.BasePatternFrequencies[p];
							}
						}
					}
				}
				entropies[x, y] = GetEntropy(position);
			}
		}

		if (tilesCompleted == genRect.Area) return Result.Next;

		return Result.Advance;
	}

	Result Advance()
	{
		int lowestIndex = entropies.Cast<int>().Min(Comparer<int>.Create((a,b) => {
			if (a == b) return 0;
			if (a == -1) return 1;
			if (b == -1) return -1;
			return a-b;
		}));
		Vector2I lowestPosition = new(lowestIndex/genRect.Size.Y, lowestIndex%genRect.Size.Y);
		
		SelectPossibility(lowestPosition);

		entropies[lowestPosition.X, lowestPosition.Y] = -1;
		tilesCompleted++;

		if (tilesCompleted == genRect.Area) {
			tileCache.WriteCache(tileMapLayer, model.TileAtlasCoords);
			return Result.Next;
		}

		// for each pattern
		for (int p = 0; p < model.BasePatterns.Count; p++) {
			int[,] pattern = model.BasePatterns[p];
			// at each overlap with the new cell
			for (int y = 0; y < model.BasePatternSize.Y; y++) {
				for (int x = 0; x < model.BasePatternSize.X; x++) {
					Vector2I checkPatternPosition = lowestPosition + new Vector2I(x,y);
					// if it has just unmatched as a result of that cell being added
					if (NewlyUnmatches(pattern, genRect.Position + checkPatternPosition, genRect.Position + lowestPosition)) {
						// update each affected cell
						for (int cy = 0; cy < model.BasePatternSize.Y; cy++) {
							for (int cx = 0; cx < model.BasePatternSize.X; cx++) {
								Vector2I cellPosition = checkPatternPosition + new Vector2I(cx, cy);
								// skip if cell outside genRect
								if (!genRect.HasPoint(genRect.Position + cellPosition)) continue;
								// skip if cell is already added
								if (entropies[cellPosition.X, cellPosition.Y] == -1) continue;
								int tile = pattern[cx, cy];
								tilePossibilities[
									cellPosition.X, cellPosition.Y,
									model.BasePatternSize.X-1-x, model.BasePatternSize.Y-1-y, tile
								] -= model.BasePatternFrequencies[p];
								System.Diagnostics.Debug.Assert(tilePossibilities[
									cellPosition.X, cellPosition.Y,
									model.BasePatternSize.X-1-x, model.BasePatternSize.Y-1-y, tile
								] >= 0);
							}
						}
					} else if (failed) {
						failed = false;
						for (int cy = 0; cy < genRect.Size.Y; cy++) {
							for (int cx = 0; cx < genRect.Size.X; cx++) {
								tileCache.SetTile(new Vector2I(cx, cy), -1);
							}
						}
						return Result.Retry;
					}
				}
			}
		}

		return Result.Advance;
	}

	double[] CollectPossibilities(Vector2I position)
	{
		double[] frequencies = new double[model.TilesCount];
		for (int tile = 0; tile < model.TilesCount; tile++) {
			frequencies[tile] = 1.0;
			for (int py = 0; py < model.BasePatternSize.Y; py++) {
				for (int px = 0; px < model.BasePatternSize.X; px++) {
					frequencies[tile] *= tilePossibilities[position.X, position.Y, px, py, tile];
				}
			}
			frequencies[tile] = Math.Pow(frequencies[tile], 0.25);
		}
		return frequencies;
	}

	void SelectPossibility(Vector2I position)
	{
		
	}

	bool Matches(int[,] pattern, Vector2I position)
	{
		for (int y = 0; y < model.BasePatternSize.Y; y++) {
			for (int x = 0; x < model.BasePatternSize.X; x++)
			{
				int checkTile = tileCache.GetTile(position+genRect.Position + new Vector2I(x,y));
				if (checkTile != -1 && checkTile != pattern[x,y]) return false;
			}
		}
		return true;
	}
	
	bool NewlyUnmatches(int[,] pattern, Vector2I patternPosition, Vector2I newCell)
	{
		return true;
	}

	int GetEntropy(Vector2I position)
	{
		return 0;
	}
}

class TileCache
{
	readonly Rect2I Bounds;
    readonly int[,] Tiles;

	public TileCache(Rect2I rect, Vector2I expand, TileMapLayer tileMapLayer, Dictionary<Vector2I, int> tileAtlasCoordsMap)
	{
		Bounds = new Rect2I(rect.Position - expand, rect.Size + 2*expand);
		for (int y = 0; y < Bounds.Size.Y; y++) {
			for (int x = 0; x < Bounds.Size.X; x++) {
				Vector2I relativePosition = new(x, y);
				Tiles[x, y] = tileAtlasCoordsMap[tileMapLayer.GetCellAtlasCoords(relativePosition + Bounds.Position)];
			}
		}
	}

	public TileCache(TileCache current, Vector2I expand, TileMapLayer tileMapLayer, Dictionary<Vector2I, int> tileAtlasCoordsMap)
	{
		Bounds = new Rect2I(current.Bounds.Position - expand, current.Bounds.Size + 2*expand);
		for (int y = 0; y < Bounds.Size.Y; y++) {
			for (int x = 0; x < Bounds.Size.X; x++) {
				Vector2I relativePosition = new(x, y);
				if (current.Bounds.HasPoint(relativePosition + Bounds.Position))
					Tiles[x, y] = current.Tiles[x-expand.X, y-expand.Y];
				else Tiles[x, y] = tileAtlasCoordsMap[tileMapLayer.GetCellAtlasCoords(relativePosition + Bounds.Position)];
			}
		}
	}

	public int GetTile(Vector2I position)
	{
		return Tiles[position.X-Bounds.Position.X, position.Y-Bounds.Position.Y];
	}

	public void SetTile(Vector2I position, int tile)
	{
		Tiles[position.X-Bounds.Position.X, position.Y-Bounds.Position.Y] = tile;
	}

	public void WriteCache(TileMapLayer tileMapLayer, List<Vector2I> tileAtlasCoords)
	{
		for (int y = 0; y < Bounds.Size.Y; y++) {
			for (int x = 0; x < Bounds.Size.X; x++) {
				Vector2I relativePosition = new(x, y);
				tileMapLayer.SetCell(Bounds.Position+relativePosition, 0, tileAtlasCoords[Tiles[x, y]]);
			}
		}
	}
}

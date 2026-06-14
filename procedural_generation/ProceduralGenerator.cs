using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class ProceduralGenerator : Node
{
	[Signal]
	public delegate void QueueEmptyEventHandler();

	readonly Random RNG = new();

	TileMapLayer tileMapLayer;
	Model model;

	public Stack<Rect2I> Queue = [];
	
	Rect2I genRect;
	TileCache tileCache;
	int[,,,,] tilePossibilities; // position, overlap, tile
	int[,] entropies; // position
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
		for (int i = 0; i < 30; i++) Tick();
	}

	void Tick()
	{
		switch (nextTick) {
			case Result.Next:
				if (Queue.Count > 0) {
					Rect2I nextRect = Queue.Pop();
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
				if (fails % 6 == 3) {
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
						Vector2I checkPatternPosition = position - new Vector2I(px, py);
						for (int p = 0; p < model.BasePatterns.Count; p++) {
							int[,] pattern = model.BasePatterns[p];
							if (Matches(pattern, checkPatternPosition+genRect.Position)) {
								int tile = pattern[px, py];
								Vector2I offset = model.BasePatternSize - Vector2I.One - new Vector2I(px, py);
								tilePossibilities[
									x, y, offset.X, offset.Y, tile
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
		Vector2I lowestPosition = GetLowestEntropy();
		
		if (SelectPossibility(lowestPosition)) {
			tileCache.WriteCache(tileMapLayer, model.BasePatternSize-Vector2I.One, model.TileAtlasCoords);
			for (int cy = 0; cy < genRect.Size.Y; cy++) {
				for (int cx = 0; cx < genRect.Size.X; cx++) {
					tileCache.SetTile(new Vector2I(cx, cy) + genRect.Position, -1);
				}
			}
			GD.Print("FAILED");
			return Result.Retry;
		}

		entropies[lowestPosition.X, lowestPosition.Y] = -1;
		tilesCompleted++;

		if (tilesCompleted == genRect.Area) {
			tileCache.WriteCache(tileMapLayer, model.BasePatternSize-Vector2I.One, model.TileAtlasCoords);
			return Result.Next;
		}

		// for each pattern
		for (int p = 0; p < model.BasePatterns.Count; p++) {
			int[,] pattern = model.BasePatterns[p];
			// at each overlap with the new cell
			for (int py = 0; py < model.BasePatternSize.Y; py++) {
				for (int px = 0; px < model.BasePatternSize.X; px++) {
					Vector2I checkPatternPosition = lowestPosition - new Vector2I(px, py);
					// if it has just unmatched as a result of that cell being added
					if (NewlyUnmatches(pattern, checkPatternPosition, lowestPosition)) {
						// update each affected cell
						for (int cy = 0; cy < model.BasePatternSize.Y; cy++) {
							for (int cx = 0; cx < model.BasePatternSize.X; cx++) {
								Vector2I cellPosition = checkPatternPosition + new Vector2I(cx, cy);
								// skip if cell outside genRect
								if (!genRect.HasPoint(genRect.Position + cellPosition)) continue;
								// skip if cell is already added
								if (entropies[cellPosition.X, cellPosition.Y] == -1) continue;
								int tile = pattern[cx, cy];
								Vector2I offset = model.BasePatternSize - Vector2I.One - new Vector2I(cx, cy);
								tilePossibilities[
									cellPosition.X, cellPosition.Y,
									offset.X, offset.Y, tile
								] -= model.BasePatternFrequencies[p];
								int result = tilePossibilities[
									cellPosition.X, cellPosition.Y,
									offset.X, offset.Y, tile
								];
								tilesChanged[cellPosition.X, cellPosition.Y] = true;
								System.Diagnostics.Debug.Assert(result >= 0);
							}
						}
					}
				}
			}
		}

		for (int y = 0; y < genRect.Size.Y; y++) {
			for (int x = 0; x < genRect.Size.X; x++){	
				if (tilesChanged[x,y]) {
					tilesChanged[x,y] = false;
					entropies[x,y] = GetEntropy(new(x,y));
				}
			}
		}

		return Result.Advance;
	}

	Vector2I GetLowestEntropy()
	{
		int lowest = -1;
		Vector2I lowestPosition = Vector2I.One * -1;
		for (int y = 0; y < genRect.Size.Y; y++) {
			for (int x = 0; x < genRect.Size.X; x++){
				int entropy = entropies[x,y];
				if (entropy != -1 && (lowest == -1 || entropy < lowest)) {
					lowest = entropy;
					lowestPosition = new(x,y);
				}
			}
		}
		System.Diagnostics.Debug.Assert(
			lowestPosition != Vector2I.One * -1
		);
		return lowestPosition;
	}

	double[] CollectPossibilities(Vector2I relativePosition)
	{
		double[] frequencies = new double[model.TilesCount];
		for (int tile = 0; tile < model.TilesCount; tile++) {
			frequencies[tile] = 1.0;
			for (int py = 0; py < model.BasePatternSize.Y; py++) {
				for (int px = 0; px < model.BasePatternSize.X; px++) {
					frequencies[tile] *= tilePossibilities[relativePosition.X, relativePosition.Y, px, py, tile];
				}
			}
			frequencies[tile] = Math.Pow(frequencies[tile], 0.25);
			
		}
		return frequencies;
	}

	bool SelectPossibility(Vector2I relativePosition)
	{
		double[] possibilities = CollectPossibilities(relativePosition);
		double totalFrequency = possibilities.Sum();
		if (totalFrequency == 0) return true;
		double randomValue = RNG.NextDouble() * totalFrequency;
		double slidingWindow = 0;
		for (int tile = 0; tile < model.TilesCount; tile++) {
			double slidingWindowNext = slidingWindow + possibilities[tile];
			if (slidingWindow <= randomValue && randomValue < slidingWindowNext) {
				tileCache.SetTile(relativePosition + genRect.Position, tile);
				return false;
			}
			slidingWindow = slidingWindowNext;
		}
		GD.Print($"this shouldnt happen! [{string.Join(", ", possibilities)}], {randomValue}");
		return true;
	}

	bool Matches(int[,] pattern, Vector2I absolutePosition)
	{
		for (int y = 0; y < model.BasePatternSize.Y; y++) {
			for (int x = 0; x < model.BasePatternSize.X; x++)
			{
				int checkTile = tileCache.GetTile(absolutePosition + new Vector2I(x,y));
				if (checkTile != -1 && checkTile != pattern[x,y]) return false;
			}
		}
		return true;
	}
	
	bool NewlyUnmatches(int[,] pattern, Vector2I relativePatternPosition, Vector2I relativeNewCell)
	{
		for (int y = 0; y < model.BasePatternSize.Y; y++) {
			for (int x = 0; x < model.BasePatternSize.X; x++)
			{ 
				Vector2I cellPosition = relativePatternPosition + new Vector2I(x,y);
				int checkTile = tileCache.GetTile(cellPosition+genRect.Position);
				if (checkTile == -1) continue;
				if (checkTile == pattern[x,y] == (cellPosition.X == relativeNewCell.X && cellPosition.Y == relativeNewCell.Y)) {
					return false;
				}
			}
		}
		return true;
	}

	int GetEntropy(Vector2I relativePosition)
	{
		double[] possibilities = CollectPossibilities(relativePosition);
		double scaling = 1/possibilities.Sum();
		double totalEntropy = 0;
		for (int tile = 0; tile < model.TilesCount; tile++) {
			double chance = possibilities[tile] * scaling;
			if (chance < 0.01) continue;
			totalEntropy -= chance * Math.Log(chance);
		}
		int entropy = (int)(totalEntropy * 1000 + RNG.NextDouble() * 8);
		return entropy;
	}
}

class TileCache
{
	readonly Rect2I Bounds;
    int[,] Tiles;

	public TileCache(Rect2I rect, Vector2I expand, TileMapLayer tileMapLayer, Dictionary<Vector2I, int> tileAtlasCoordsMap)
	{
		Bounds = new Rect2I(rect.Position - expand, rect.Size + 2*expand);
		Tiles = new int[Bounds.Size.X, Bounds.Size.Y];
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
		Tiles = new int[Bounds.Size.X, Bounds.Size.Y];
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

	public void WriteCache(TileMapLayer tileMapLayer, Vector2I expand, List<Vector2I> tileAtlasCoords)
	{
		for (int y = expand.Y; y < Bounds.Size.Y - expand.Y; y++) {
			for (int x = expand.X; x < Bounds.Size.X - expand.X; x++) {
				Vector2I relativePosition = new(x, y);
				tileMapLayer.SetCell(Bounds.Position+relativePosition, 0, Tiles[x, y] == -1 ? Vector2I.One * -1 : tileAtlasCoords[Tiles[x, y]]);
			}
		}
	}
}

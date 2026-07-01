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
	TileMapLayer? ConvertedTileMapLayer;
	Model model;

	public World world;

	Stack<Task> Queue = [];
	
	Task task;
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

	const double INVERSE_TEMPERATURE = 0.25;

	public void AddToQueue(Rect2I rect) {
		Queue.Push(new Task(rect, true));
	}

	public void SetContext(TileMapLayer tileMapLayer, TileMapLayer convertedTileMapLayer, Model model)
	{
		this.tileMapLayer = tileMapLayer;
		ConvertedTileMapLayer = convertedTileMapLayer;
		this.model = model;
		model.ImportProperties();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		for (int i = 0; i < 60; i++) Tick();
		world.DrawDebug(task.rect);
	}

	void Tick()
	{
		switch (nextTick) {
			case Result.Next:
				if (Queue.Count > 0) {
					Task nextTask = Queue.Pop();
					fails = 0;
					tileCache = new(
						nextTask.rect, model.BasePatternSize-Vector2I.One,
						tileMapLayer, model.TileAtlasCoordsMap);
					task = nextTask;
					nextTick = Setup();
				} else EmitSignalQueueEmpty();
			break;
			case Result.Retry:
				fails++;
				if (!task.increaseSize) {
					nextTick = Result.Next;
					return;
				}
				if (fails % 12 == 3) {
					// const int SPLIT_SIZE = 6;
					// const int SPLIT_CHUNKS = 2;
					// const int SPLIT_RETRIES = 5;
					// if (task.rect.Size.X >= SPLIT_SIZE * SPLIT_CHUNKS && task.rect.Size.Y >= SPLIT_SIZE * SPLIT_CHUNKS) {
					// 	for (int i = 0; i < SPLIT_RETRIES; i++) {
					// 		for (int sy = 0; sy < SPLIT_CHUNKS; sy++) {
					// 			for (int sx = 0; sx < SPLIT_CHUNKS; sx++) {
					// 				Rect2I nextRect = new(task.rect.Position+new Vector2I(sx, sy)*SPLIT_SIZE, Vector2I.One * SPLIT_SIZE);
					// 				Queue.Push(new Task(nextRect, false));
					// 			}
					// 		}
					// 	}
					// 	nextTick = Result.Next;
					// 	return;
					// }
					task.rect.Position -= Vector2I.One;
					task.rect.Size += Vector2I.One * 2;
					tileCache = new(tileCache, Vector2I.One, tileMapLayer, model.TileAtlasCoordsMap);
					if (task.rect.Size.X > 14) task.increaseSize = false;
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
			task.rect.Size.X,
			task.rect.Size.Y,
			model.BasePatternSize.X,
			model.BasePatternSize.Y,
			model.TileAtlasCoords.Count
		];
		entropies = new int[task.rect.Size.X, task.rect.Size.Y];
		tilesChanged = new bool[task.rect.Size.X, task.rect.Size.Y];
		tilesCompleted = 0;

		for (int y = 0; y < task.rect.Size.Y; y++) {
			for (int x = 0; x < task.rect.Size.X; x++) {
				Vector2I position = new(x, y);
				if (tileCache.GetTile(position + task.rect.Position) != -1) {
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
							if (Matches(pattern, checkPatternPosition+task.rect.Position)) {
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

		if (tilesCompleted == task.rect.Area) return Result.Next;

		return Result.Advance;
	}

	Result Advance()
	{
		Vector2I lowestPosition = GetLowestEntropy();
		
		if (SelectPossibility(lowestPosition)) {
			tileCache.WriteCache(tileMapLayer, model.BasePatternSize-Vector2I.One, model.TileAtlasCoords);
			for (int cy = 0; cy < task.rect.Size.Y; cy++) {
				for (int cx = 0; cx < task.rect.Size.X; cx++) {
					tileCache.SetTile(new Vector2I(cx, cy) + task.rect.Position, -1);
				}
			}
			return Result.Retry;
		}

		entropies[lowestPosition.X, lowestPosition.Y] = -1;
		tilesCompleted++;

		if (tilesCompleted == task.rect.Area) {
			tileCache.WriteCache(tileMapLayer, model.BasePatternSize-Vector2I.One, model.TileAtlasCoords);
			WriteConverted();
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
								// skip if cell outside task.rect
								if (!task.rect.HasPoint(task.rect.Position + cellPosition)) continue;
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

		for (int y = 0; y < task.rect.Size.Y; y++) {
			for (int x = 0; x < task.rect.Size.X; x++){	
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
		for (int y = 0; y < task.rect.Size.Y; y++) {
			for (int x = 0; x < task.rect.Size.X; x++){
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
			frequencies[tile] = Math.Pow(frequencies[tile], INVERSE_TEMPERATURE);
			
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
				tileCache.SetTile(relativePosition + task.rect.Position, tile);
				if (tile == 1 && RNG.NextSingle() < 0.02) world.SpawnCreature(relativePosition + task.rect.Position);
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
				int checkTile = tileCache.GetTile(cellPosition+task.rect.Position);
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

	void WriteConverted()
	{
		for (int y = -1; y < task.rect.Size.Y; y++) {
			for (int x = -1; x < task.rect.Size.X; x++) {
				Vector2I tilePosition = task.rect.Position + new Vector2I(x,y);
				int[,] pattern = new int[2,2];
				for (int cy = 0; cy < 2; cy++) {
					for (int cx = 0; cx < 2; cx++) {
						int tile = tileCache.GetTile(tilePosition + new Vector2I(cx,cy));
						if (tile == -1) goto next;
						pattern[cx,cy] = tile;
					}
				}
				try {
					int[,] result = model.ConversionMap[pattern];
					for (int cy = 0; cy < 2; cy++) {
						for (int cx = 0; cx < 2; cx++) {
							ConvertedTileMapLayer!.SetCell(tilePosition * 2 + Vector2I.One + new Vector2I(cx,cy), 0, model.ConvertedTileAtlasCoords[result[cx,cy]]);
						}
					}
				} catch {}
				next: continue;
			}
		}
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

class Task {
	public Rect2I rect;
	public bool increaseSize;

	public Task(Rect2I rect, bool increaseSize)
	{
		this.rect = rect;
		this.increaseSize = increaseSize;
	}
}

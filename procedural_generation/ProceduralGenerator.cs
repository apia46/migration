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

	#nullable disable
	TileMapLayer TileMapLayer;
	TileMapLayer ConvertedTileMapLayer;
	Model model;

	public World world;

	Stack<Task> Queue = [];
	
	Task task;
	TileCache tileCache;
	TileCache convertedTileCache;
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

	public void SetContext(TileMapLayer TileMapLayer, TileMapLayer convertedTileMapLayer, Model model)
	{
		this.TileMapLayer = TileMapLayer;
		ConvertedTileMapLayer = convertedTileMapLayer;
		this.model = model;
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
						nextTask.rect, model.PatternSize-Vector2I.One,
						TileMapLayer, model.PatternTiles);
					convertedTileCache = new(
						new Rect2I(nextTask.rect.Position*2, nextTask.rect.Size*2), Vector2I.Zero,
						ConvertedTileMapLayer, model.ConvertedTiles);
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
					task.rect.Position -= Vector2I.One;
					task.rect.Size += Vector2I.One * 2;
					tileCache = new(tileCache, Vector2I.One, TileMapLayer, model.PatternTiles);
					convertedTileCache = new(convertedTileCache, Vector2I.One*2, ConvertedTileMapLayer, model.ConvertedTiles);
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
			model.PatternSize.X,
			model.PatternSize.Y,
			model.PatternTiles.Count
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
				
				for (int py = 0; py < model.PatternSize.Y; py++) {
					for (int px = 0; px < model.PatternSize.X; px++) {
						Vector2I checkPatternPosition = position - new Vector2I(px, py);
						foreach (Pattern pattern in model.MatchPatterns(GetTiles(checkPatternPosition+task.rect.Position, model.PatternSize))) {
							int tile = pattern.Tiles[Fold(px, py, model.PatternSize)];
							Vector2I offset = model.PatternSize - Vector2I.One - new Vector2I(px, py);
							tilePossibilities[
								x, y, offset.X, offset.Y, tile
							] += pattern.Frequency;
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
			tileCache.WriteCache(TileMapLayer, model.PatternSize-Vector2I.One, model.PatternTiles);
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
			tileCache.WriteCache(TileMapLayer, model.PatternSize-Vector2I.One, model.PatternTiles);
			return Result.Next;
		}

		// for each pattern
		foreach (Pattern pattern in model.Patterns) {
			// at each overlap with the new cell
			for (int py = 0; py < model.PatternSize.Y; py++) {
				for (int px = 0; px < model.PatternSize.X; px++) {
					Vector2I checkPatternPosition = lowestPosition - new Vector2I(px, py);
					// if it has just unmatched as a result of that cell being added
					if (NewlyUnmatches(pattern, checkPatternPosition, lowestPosition)) {
						// update each affected cell
						for (int cy = 0; cy < model.PatternSize.Y; cy++) {
							for (int cx = 0; cx < model.PatternSize.X; cx++) {
								Vector2I cellPosition = checkPatternPosition + new Vector2I(cx, cy);
								// skip if cell outside task.rect
								if (!task.rect.HasPoint(task.rect.Position + cellPosition)) continue;
								// skip if cell is already added
								if (entropies[cellPosition.X, cellPosition.Y] == -1) continue;
								int tile = pattern.Tiles[Fold(cx, cy,model.PatternSize)];
								Vector2I offset = model.PatternSize - Vector2I.One - new Vector2I(cx, cy);
								tilePossibilities[
									cellPosition.X, cellPosition.Y,
									offset.X, offset.Y, tile
								] -= pattern.Frequency;
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
		double[] frequencies = new double[model.PatternTiles.Count];
		for (int tile = 0; tile < model.PatternTiles.Count; tile++) {
			frequencies[tile] = 1.0;
			for (int py = 0; py < model.PatternSize.Y; py++) {
				for (int px = 0; px < model.PatternSize.X; px++) {
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
		for (int tile = 0; tile < model.PatternTiles.Count; tile++) {
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

	int[] GetTiles(Vector2I absolutePosition, Vector2I size)
	{
		int[] tiles = new int[size.X*size.Y];
		for (int y = 0; y < size.Y; y++)
			for (int x = 0; x < size.X; x++)
				tiles[Fold(x,y,size)] = tileCache.GetTile(absolutePosition+new Vector2I(x,y));
		return tiles;
	}
	
	bool NewlyUnmatches(Pattern pattern, Vector2I relativePatternPosition, Vector2I relativeNewCell)
	{
		for (int y = 0; y < model.PatternSize.Y; y++) {
			for (int x = 0; x < model.PatternSize.X; x++)
			{ 
				Vector2I cellPosition = relativePatternPosition + new Vector2I(x,y);
				int checkTile = tileCache.GetTile(cellPosition+task.rect.Position);
				if (checkTile == -1) continue;
				if (checkTile == pattern.Tiles[Fold(x,y,model.PatternSize)] == (cellPosition.X == relativeNewCell.X && cellPosition.Y == relativeNewCell.Y)) {
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
		for (int tile = 0; tile < model.PatternTiles.Count; tile++) {
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
	public Rect2I Bounds;
    int[] Tiles;

	public TileCache(Rect2I rect, Vector2I expand, TileMapLayer TileMapLayer, EnumeratedTileSet tileSet)
	{
		Bounds = new Rect2I(rect.Position - expand, rect.Size + 2*expand);
		Tiles = new int[Bounds.Size.X*Bounds.Size.Y];
		for (int y = 0; y < Bounds.Size.Y; y++) {
			for (int x = 0; x < Bounds.Size.X; x++) {
				Vector2I relativePosition = new(x, y);
				Tiles[x+y*Bounds.Size.X] = tileSet.Convert(TileMapLayer.GetCellAtlasCoords(relativePosition + Bounds.Position));
			}
		}
	}

	public TileCache(TileCache current, Vector2I expand, TileMapLayer TileMapLayer, EnumeratedTileSet tileSet)
	{
		Bounds = new Rect2I(current.Bounds.Position - expand, current.Bounds.Size + 2*expand);
		Tiles = new int[Bounds.Size.X*Bounds.Size.Y];
		for (int y = 0; y < Bounds.Size.Y; y++) {
			for (int x = 0; x < Bounds.Size.X; x++) {
				Vector2I relativePosition = new(x, y);
				if (current.Bounds.HasPoint(relativePosition + Bounds.Position))
					Tiles[Fold(x,y,Bounds.Size)] = current.Tiles[Fold(x-expand.X,y-expand.Y,current.Bounds.Size)];
				else Tiles[Fold(x,y,Bounds.Size)] = tileSet.Convert(TileMapLayer.GetCellAtlasCoords(relativePosition + Bounds.Position));
			}
		}
	}

	public int GetTile(Vector2I position)
	{
		return Tiles[Fold(position-Bounds.Position, Bounds.Size)];
	}

	public void SetTile(Vector2I position, int tile)
	{
		Tiles[Fold(position-Bounds.Position, Bounds.Size)] = tile;
	}

	public void WriteCache(TileMapLayer TileMapLayer, Vector2I expand, EnumeratedTileSet tileSet)
	{
		for (int y = expand.Y; y < Bounds.Size.Y - expand.Y; y++) {
			for (int x = expand.X; x < Bounds.Size.X - expand.X; x++) {
				Vector2I relativePosition = new(x, y);
				TileMapLayer.SetCell(Bounds.Position+relativePosition, 0, Tiles[Fold(x, y, Bounds.Size)] == -1 ? Vector2I.One * -1 : tileSet.Convert(Tiles[Fold(x, y, Bounds.Size)]));
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

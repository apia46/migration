[GlobalClass]
public partial class ProceduralGenerator : Node
{
	readonly Rect2I STARTING_AREA = new(new(-7, -4), new(8, 4));
	public const int CHUNK_SIZE = 8;
	const double INVERSE_TEMPERATURE = 0.25;
	const int EXPAND_RADIUS = 1;
	public const int SIZE_THRESHOLD = 12;

	[Signal]
	public delegate void QueueEmptyEventHandler();

	readonly GameRandom RNG = new();
	readonly GodotThread Thread = new();
	readonly Mutex Mutex = new();

	#nullable disable
	public World world;

	TileMapLayer PatternLayer;
	TileMapLayer ConvertedLayer;
	
	// MUTEXED
	Model Model;
	// MUTEXED
	TileCache PatternTiles;
	// MUTEXED
	TileCache ConvertedTiles;
	#nullable enable

	// MUTEXED
	readonly Stack<Task> Queue = [];

	public void SetContext(TileMapLayer patternLayer, TileMapLayer convertedLayer, Model model)
	{
		PatternLayer = patternLayer;
		ConvertedLayer = convertedLayer;
		Model = model;
	}

	public void AddToQueue(Vector2I position, bool clearBefore)
	{
		Mutex.Lock();
		Queue.Push(new(position, clearBefore, true));
		Mutex.Unlock();
	}

    public override void _Process(double delta)
	{
		int runs = 0;
		Mutex.Lock();
		while (runs++ < 30 && !Thread.IsAlive()) {
			if (Queue.Count == 0) EmitSignalQueueEmpty();
			else {
				if (Thread.IsStarted()) {
					if ((bool)Thread.WaitToFinish()) {
						ConvertedTiles.WriteTileMap();
						PatternTiles.WriteTileMap();
					}
				}
				Task task = Queue.Peek();
				if (task.IsNew() && task.ClearBefore)
					for (int x = task.Rect.Position.X; x < task.Rect.End.X; x++)
						for (int y = task.Rect.Position.Y; y < task.Rect.End.Y; y++)
							if (!STARTING_AREA.HasPoint(new(x,y))) PatternLayer.SetCell(new Vector2I(x,y));
				Rect2I rect = task.Next();
				// world.DrawDebug(rect);
				if (task.IsEmpty()) Queue.Pop();
				Rect2I convertedRect = new((rect.Position-Vector2I.One)*Model.ConversionScale, (rect.Size+Vector2I.One*2)*Model.ConversionScale);
				PatternTiles = new(rect, Model.PatternSize-Vector2I.One, Model.PatternTiles, PatternLayer, 0);
				if (!PatternTiles.AnyEmpty()) continue;
				ConvertedTiles = new(convertedRect, Vector2I.Zero, Model.ConvertedTiles, ConvertedLayer, 0);
				Thread.Start(Callable.From(()=>Generate(rect, task.CanRetry)));
			}
		}
		Mutex.Unlock();
    }

	// returns if successful
	bool Generate(Rect2I rect, bool canRetry)
	{
		Mutex.Lock();
		int tries = 0;
		while (TryGenerate(rect)) {
			tries++;
			for (int x = rect.Position.X; x < rect.End.X; x++)
				for (int y = rect.Position.Y; y < rect.End.Y; y++)
					if (!STARTING_AREA.HasPoint(new(x,y))) PatternTiles.SetTile(new Vector2I(x,y), -1);
			if (tries > 3) {
				if (canRetry) {
					Task retry = new(new Rect2I(rect.Position - Vector2I.One*EXPAND_RADIUS, rect.Size + Vector2I.One*2*EXPAND_RADIUS), true);
					Queue.Push(retry);
				}
				Mutex.Unlock();
				return false;
			}
		}
		Mutex.Unlock();
		return true;
	}

	// returns true if failed
	bool TryGenerate(Rect2I rect)
	{
		Vector2I patternsMargin = Model.PatternSize - Vector2I.One;
		Vector2I patternsRectSize = rect.Size + patternsMargin;
		// setup
		List<Pattern>[] patterns = new List<Pattern>[patternsRectSize.X*patternsRectSize.Y];
		double[] entropies = new double[rect.Size.X*rect.Size.Y];
		int tilesCompleted = 0;

		for (int x = 0; x < patternsRectSize.X; x++)
			for (int y = 0; y < patternsRectSize.Y; y++)
				patterns[Fold(x,y,patternsRectSize)] = Model.MatchPatterns(GetTiles(rect.Position + new Vector2I(x,y) - patternsMargin, Model.PatternSize));
		for (int x = 0; x < rect.Size.X; x++)
			for (int y = 0; y < rect.Size.Y; y++) {
				Vector2I position = new(x,y);
				if (PatternTiles.GetTile(position+rect.Position) != -1) {
					entropies[Fold(x,y,rect.Size)] = -1;
					tilesCompleted++;
				}
				else entropies[Fold(x,y,rect.Size)] = GetEntropy(GetNearbyPatterns(position, patterns, patternsRectSize));
			}
		// loop
		while (tilesCompleted < rect.Area) {
			Vector2I collapsePosition = GetLowestEntropy(rect, entropies);
			if (SelectPossibility(GetNearbyPatterns(collapsePosition, patterns, patternsRectSize)) is int tile) {
				tilesCompleted++;
				PatternTiles.SetTile(collapsePosition + rect.Position, tile);
				entropies[Fold(collapsePosition,rect.Size)] = -1;
				for (int px = 0; px < Model.PatternSize.X; px++)
					for (int py = 0; py < Model.PatternSize.Y; py++) {
						Vector2I updatePatternPosition = collapsePosition + new Vector2I(px,py);
						patterns[Fold(updatePatternPosition,patternsRectSize)] = Model.MatchPatterns(
							GetTiles(updatePatternPosition-patternsMargin+rect.Position,Model.PatternSize),
							patterns[Fold(updatePatternPosition,patternsRectSize)]
						);
					}
				for (int px = 1-Model.PatternSize.X; px < Model.PatternSize.X; px++)
					for (int py = 1-Model.PatternSize.Y; py < Model.PatternSize.Y; py++) {
						Vector2I updateEntropyPosition = collapsePosition + new Vector2I(px,py);
						if (!rect.HasPoint(rect.Position + updateEntropyPosition)) continue;
						if (entropies[Fold(updateEntropyPosition,rect.Size)] == -1) continue;
						entropies[Fold(updateEntropyPosition,rect.Size)] = GetEntropy(GetNearbyPatterns(updateEntropyPosition, patterns, patternsRectSize));
					}
			} else return true;
		}
		for (int x = -1; x < rect.Size.X+1; x++)
			for (int y = -1; y < rect.Size.Y+1; y++) {
				Vector2I position = new(x,y);
				List<Pattern> convertPatterns = patterns[Fold(position+(Model.PatternSize-Vector2I.One)/2,patternsRectSize)];
				Pattern chosenPattern = convertPatterns[(int)(RNG.NextDouble() * convertPatterns.Count)];
				for (int cx = 0; cx < Model.ConversionScale.X; cx++)
					for (int cy = 0; cy < Model.ConversionScale.Y; cy++)
						ConvertedTiles.SetTile((rect.Position+position)*Model.ConversionScale + new Vector2I(cx,cy), chosenPattern.Conversion[Fold(cx,cy,Model.ConversionScale)]);
			}
		return false;
	}

	List<Pattern>[] GetNearbyPatterns(Vector2I relativePosition, List<Pattern>[] patterns, Vector2I patternsRectSize)
	{
		List<Pattern>[] result = new List<Pattern>[Model.PatternSize.X*Model.PatternSize.Y];
		for (int x = 0; x < Model.PatternSize.X; x++)
			for (int y = 0; y < Model.PatternSize.Y; y++) {
				Vector2I position = relativePosition + new Vector2I(x,y);
				result[Fold(x,y,Model.PatternSize)] = patterns[Fold(position,patternsRectSize)];
			}
		return result;
	}

	Vector2I GetLowestEntropy(Rect2I rect, double[] entropies)
	{
		double lowest = -1;
		Vector2I lowestPosition = Vector2I.One * -1;
		for (int x = 0; x < rect.Size.X; x++)
			for (int y = 0; y < rect.Size.Y; y++) {
				double entropy = entropies[Fold(x,y,rect.Size)];
				if (lowest == -1 || (entropy < lowest && entropy != -1)) {
					lowest = entropy;
					lowestPosition = new Vector2I(x,y);
				}
			}
		System.Diagnostics.Debug.Assert(lowestPosition != Vector2I.One * -1);
		return lowestPosition;
	}

	int[] CountTiles(List<Pattern> patterns, Vector2I at)
	{
		int[] counts = new int[Model.PatternTiles.Count];
		foreach (Pattern pattern in patterns) counts[pattern.Tiles[Fold(at,Model.PatternSize)]]++;
		return counts;
	}
	
	double[] CollectPossibilities(List<Pattern>[] patterns)
	{
		double[] possibilities = new double[Model.PatternTiles.Count];
		for (int i = 0; i < Model.PatternTiles.Count; i++) possibilities[i] = 1.0;
		for (int x = 0; x < Model.PatternSize.X; x++)
			for (int y = 0; y < Model.PatternSize.Y; y++) {
				int[] tiles = CountTiles(patterns[Fold(x,y,Model.PatternSize)], Model.PatternSize - new Vector2I(x,y) - Vector2I.One);
				for (int i = 0; i < Model.PatternTiles.Count; i++)
					possibilities[i] *= tiles[i];
			}
		for (int i = 0; i < Model.PatternTiles.Count; i++) possibilities[i] = Math.Pow(possibilities[i], INVERSE_TEMPERATURE);
		return possibilities;
	}

	int? SelectPossibility(List<Pattern>[] patterns)
	{
		double[] possibilities = CollectPossibilities(patterns);
		double totalFrequency = possibilities.Sum();
		if (totalFrequency == 0) return null;
		double randomValue = RNG.NextDouble() * totalFrequency;
		double slidingWindow = 0;
		for (int tile = 0; tile < Model.PatternTiles.Count; tile++) {
			double slidingWindowNext = slidingWindow + possibilities[tile];
			if (slidingWindow <= randomValue && randomValue < slidingWindowNext) return tile;
			slidingWindow = slidingWindowNext;
		}
		GD.Print($"this shouldnt happen! [{string.Join(", ", possibilities)}], {randomValue}");
		return null;
	}

	double GetEntropy(List<Pattern>[] patterns)
	{
		double entropy = 0;
		double[] possibilities = CollectPossibilities(patterns);
		double scale = 1/possibilities.Sum();
		foreach (double possibility in possibilities)
		{
			double chance = possibility*scale;
			if (chance < 0.01) continue;
			entropy -= chance * Math.Log(chance);
		}
		return entropy * 1000 + RNG.NextDouble() * 8;
	}

	int[] GetTiles(Vector2I absolutePosition, Vector2I size)
	{
		int[] tiles = new int[size.X*size.Y];
		for (int y = 0; y < size.Y; y++)
			for (int x = 0; x < size.X; x++)
				tiles[Fold(x,y,size)] = PatternTiles.GetTile(absolutePosition+new Vector2I(x,y));
		return tiles;
	}
}

class Task
{
	public bool ClearBefore;
	public bool CanRetry;
	public Rect2I Rect;
	readonly List<Rect2I> Subtasks;
	int pointer = 0;

	const int CHUNK_SIZE = ProceduralGenerator.CHUNK_SIZE;

	public Task(Vector2I position, bool clearBefore, bool canRetry)
	{
		ClearBefore = clearBefore;
		CanRetry = canRetry;
		Rect = new (position*CHUNK_SIZE, Vector2I.One * CHUNK_SIZE);
		Subtasks = [Rect];
	}

	// public Task (Rect2I rect, bool clearBefore, bool canRetry)
	// {
	// 	ClearBefore = clearBefore;
	// 	CanRetry = canRetry;
	// 	Rect = rect;
	// 	Subtasks = [Rect];
	// }

	public Task (Rect2I rect, bool clearBefore)
	{
		ClearBefore = clearBefore;
		Rect = rect;
		Subtasks = [];
		CanRetry = rect.Size.X <= ProceduralGenerator.SIZE_THRESHOLD;
		Subtasks.Add(Rect);
	}

	public Rect2I Next() { return Subtasks[pointer++]; }
	public bool IsEmpty() { return pointer == Subtasks.Count; }
	public bool IsNew() { return pointer == 0; }
}

class TileCache
{
	readonly public Rect2I Rect;
	readonly public Vector2I Margin;
	readonly public EnumeratedTileSet TileSet;
	readonly public TileMapLayer TileMap;
	readonly Vector2I TotalSize;
	readonly int SourceId;
	
	readonly int[] Tiles;

	public TileCache(Rect2I rect, Vector2I margin, EnumeratedTileSet tileSet, TileMapLayer tileMap, int sourceId)
	{
		Rect = rect;
		Margin = margin;
		TileSet = tileSet;
		TileMap = tileMap;
		SourceId = sourceId;
		TotalSize = new(Rect.Size.X+2*Margin.X, Rect.Size.Y+2*Margin.Y);
		Tiles = new int[TotalSize.X*TotalSize.Y];
		for (int x = 0; x < TotalSize.X; x++)
			for (int y = 0; y < TotalSize.Y; y++)
				Tiles[Fold(x,y,TotalSize)] = TileSet.Convert(TileMap.GetCellAtlasCoords(rect.Position - Margin + new Vector2I(x,y)));
	}

	public bool AnyEmpty()
	{
		foreach (int tile in Tiles) if (tile == -1) return true;
		return false;
	}

	public int GetTile(Vector2I absolutePosition) { return Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)]; }
	public void SetTile(Vector2I absolutePosition, int to) { Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)] = to; }

	public void WriteTileMap()
	{
		for (int x = 0; x < Rect.Size.X; x++)
			for (int y = 0; y < Rect.Size.Y; y++)
				TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId, TileSet.Convert(Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)]));
	}
}

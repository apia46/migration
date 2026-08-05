[GlobalClass]
public partial class ProceduralGenerator : Node
{
	readonly Rect2I STARTING_AREA = new(new(-7, -7), new(15, 8));
	public const int PATTERN_CHUNK_SIZE = 8;
	public const int CONVERTED_CHUNK_SIZE = 16;
	const double INVERSE_TEMPERATURE = 0.25;
	const int EXPAND_RADIUS = 1;
	public const int SIZE_THRESHOLD = 12;


	readonly GameRandom RNG = new();
	readonly GodotThread Thread = new();
	public static readonly Mutex Mutex = new();

	#nullable disable

	TileMapLayer PatternLayer;
	TileMapLayer ConvertedLayer;
	
	public Model Model;
	// MUTEXED
	TileCache PatternTiles;
	// MUTEXED
	RotateableTileCache ConvertedTiles;
	#nullable enable

	// MUTEXED
	readonly Stack<Task> Queue = [];
	// MUTEXED
	public enum ChunkState {Generated, Detailed};
	public static readonly Dictionary<Vector2I, ChunkState> ChunkStates = []; // in chunks

	bool CleanPass = false;
	bool InitialGenFinished = false;

	const int GENERATE_CHUNKS_AROUND_PLAYER = 4;
	const int UNSTABLE_CHUNKS_THRESHOLD = 9;

	public void StartingArea()
	{
		Mutex.Lock();
		Queue.Push(new(Vector2I.Zero, false, true));
		for (int i = 0; i < 4; i++) NextChunks(3);
		// for (int i = 0; i < 4; i++) NextChunks(5);
		Mutex.Unlock();
	}
	
	void NextChunks(int chunks)
	{
		if ((!InitialGenFinished) && CleanPass) {
			InitialGenFinished = true;
			World.InitialProcGenFinished();
		}
		void AddToQueue(Vector2I position, bool clearBefore) {
			if (ChunkStates.ContainsKey(position)) return;
			Queue.Push(new(position, clearBefore, true));
		}

		Vector2I position = (Vector2I)(World.Player.Position / PATTERN_CHUNK_SIZE / World.PATTERN_TILE_SIZE).Round();
		for (int layer = chunks; layer > 0; layer--) {
			bool unstable = layer >= UNSTABLE_CHUNKS_THRESHOLD;
			for (int x = 0; x < layer*2; x++) {
				AddToQueue(position + new Vector2I(layer,layer-x), unstable && Game.RNG.NextDouble()*4 < World.Player.Stillness);
				AddToQueue(position + new Vector2I(layer-x,-layer), unstable && Game.RNG.NextDouble()*4 < World.Player.Stillness);
				AddToQueue(position + new Vector2I(-layer,x-layer), unstable && Game.RNG.NextDouble()*4 < World.Player.Stillness);
				AddToQueue(position + new Vector2I(x-layer,layer), unstable && Game.RNG.NextDouble()*4 < World.Player.Stillness);
			}
		}
		AddToQueue(position, false);
	}

	public void SetContext(TileMapLayer patternLayer, TileMapLayer convertedLayer, Model model)
	{
		PatternLayer = patternLayer;
		ConvertedLayer = convertedLayer;
		Model = model;
	}

    public override void _Process(double delta)
	{
		int runs = 0;
		while (runs++ < 30 && !Thread.IsAlive()) {
			Mutex.Lock();
			if (Queue.Count == 0) {
				NextChunks(GENERATE_CHUNKS_AROUND_PLAYER);
				CleanPass = true;
			}
			if (Thread.IsStarted()) {
				if ((bool)Thread.WaitToFinish()) {
					ConvertedTiles.WriteTileMap();
					PatternTiles.WriteTileMap();
				} else CleanPass = false;
			}
			Task task = Queue.Peek();
			if (task.IsNew() && task.ClearBefore) {
				RectangleReverted(task.Rect);
				for (int x = task.Rect.Position.X; x < task.Rect.End.X; x++)
					for (int y = task.Rect.Position.Y; y < task.Rect.End.Y; y++)
						if (!STARTING_AREA.HasPoint(new(x,y))) PatternLayer.SetCell(new Vector2I(x,y));
			}
			Rect2I rect = task.Next();
			if (task.IsEmpty()) Queue.Pop();
			Rect2I convertedRect = new((rect.Position-Vector2I.One)*Model.ConversionScale, (rect.Size+Vector2I.One*2)*Model.ConversionScale);
			PatternTiles = new(rect, Model.PatternSize-Vector2I.One, Model.PatternTiles, PatternLayer, 0);
			if (!PatternTiles.AnyEmpty()) {Mutex.Unlock(); continue;}
			ConvertedTiles = new(convertedRect, Vector2I.Zero, Model.ConvertedTiles, ConvertedLayer, 0);
			Thread.Start(Callable.From(()=>Generate(rect, task.CanRetry)));
			Mutex.Unlock();
		}
    }

	void RectangleReverted(Rect2I rect)
	{
		Vector2I start = (Vector2I)(((Vector2)rect.Position)/8).Floor();
		Vector2I end = (Vector2I)(((Vector2)rect.End)/8).Ceil();
		for (int x = start.X; x < end.X; x++)
		for (int y = start.Y; y < end.Y; y++)
			ChunkStates.Remove(new(x,y));
	}

	// returns true if successful
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
		ChunkStates[(Vector2I)(((Vector2)rect.Position)/8).Ceil()] = ChunkState.Generated;
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
			for (int cy = 0; cy < Model.ConversionScale.Y; cy++) {
				ConvertedTiles.SetTile((rect.Position+position)*Model.ConversionScale + new Vector2I(cx,cy), chosenPattern.Conversion[Fold(cx,cy,Model.ConversionScale)]);
				ConvertedTiles.SetTileRotation((rect.Position+position)*Model.ConversionScale + new Vector2I(cx,cy), chosenPattern.ConversionRotation[Fold(cx,cy,Model.ConversionScale)]);
			}
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

	const int PATTERN_CHUNK_SIZE = ProceduralGenerator.PATTERN_CHUNK_SIZE;

	public Task(Vector2I position, bool clearBefore, bool canRetry)
	{
		ClearBefore = clearBefore;
		CanRetry = canRetry;
		Rect = new (position*PATTERN_CHUNK_SIZE, Vector2I.One * PATTERN_CHUNK_SIZE);
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

	public Rect2I Next() => Subtasks[pointer++];
	public bool IsEmpty() => pointer == Subtasks.Count;
	public bool IsNew() => pointer == 0;
}

class TileCache
{
	readonly public Rect2I Rect;
	readonly public Vector2I Margin;
	readonly public EnumeratedTileSet TileSet;
	readonly public TileMapLayer TileMap;
	readonly protected Vector2I TotalSize;
	readonly protected int SourceId;
	
	readonly protected int[] Tiles;

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

	public int GetTile(Vector2I absolutePosition) => Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)];
	public void SetTile(Vector2I absolutePosition, int to) => Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)] = to;

	public virtual void WriteTileMap()
	{
		for (int x = 0; x < Rect.Size.X; x++)
		for (int y = 0; y < Rect.Size.Y; y++)
			TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId, TileSet.Convert(Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)]));
	}
}

class RotateableTileCache : TileCache
{
	readonly int[] TileRotations;
    public RotateableTileCache(Rect2I rect, Vector2I margin, EnumeratedTileSet tileSet, TileMapLayer tileMap, int sourceId)
		: base(rect, margin, tileSet, tileMap, sourceId)
	{
		TileRotations = new int[TotalSize.X*TotalSize.Y];
		for (int x = 0; x < TotalSize.X; x++)
		for (int y = 0; y < TotalSize.Y; y++)
			TileRotations[Fold(x,y,TotalSize)] = TileSet.Convert(TileMap.GetCellAtlasCoords(rect.Position - Margin + new Vector2I(x,y)));
	}

	public int GetTileRotation(Vector2I absolutePosition) => TileRotations[Fold(absolutePosition-Rect.Position+Margin,TotalSize)];
	public void SetTileRotation(Vector2I absolutePosition, int to) => TileRotations[Fold(absolutePosition-Rect.Position+Margin,TotalSize)] = to;

	public override void WriteTileMap()
	{
		for (int x = 0; x < Rect.Size.X; x++)
		for (int y = 0; y < Rect.Size.Y; y++)
			TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId, TileSet.Convert(Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)]), TileRotations[Fold(x+Margin.X,y+Margin.Y,TotalSize)]);
	}
}

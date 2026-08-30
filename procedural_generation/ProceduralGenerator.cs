[GlobalClass]
public partial class ProceduralGenerator : Node
{
	public static readonly Rect2I STARTING_AREA = new(new(-7, -7), new(15, 8));
	public const int PATTERN_CHUNK_SIZE = 8;
	public const int CONVERTED_CHUNK_SIZE = 16;
	const double INVERSE_TEMPERATURE = 0.25;
	public const int EXPAND_LIMIT = 4;

	// in chunks
	public const int START_POOLS_TRANSITION = -3;

	readonly GameRandom RNG = new();
	readonly GodotThread Thread = new();
	public static readonly Mutex Mutex = new();

	#nullable disable

	[Export] public TileMapLayer PatternLayer;
	[Export] public TileMapLayer ConvertedLayer;
	[Export] public TileMapLayer WaterLayer;
	
	public enum Areas {Start, Pools, Top, Restart, End, TransStartPools, TransStartEnd};
	static readonly Model[] Models = [
		GD.Load<ModelResource>("res://procedural_generation/models/start.tres").ToModel(),
		GD.Load<ModelResource>("res://procedural_generation/models/pools.tres").ToModel(),
	];

	// MUTEXED
	TileSetCache PatternTiles;
	// MUTEXED
	RotateableTileSetCache ConvertedTiles;
	// MUTEXED
	TileExistsCache WaterTiles;
	#nullable enable

	// MUTEXED
	readonly Stack<Task> Queue = [];
	// MUTEXED
	public enum ChunkState {Generated, Detailed};
	public static readonly Dictionary<Vector2I, ChunkState> ChunkStates = []; // in chunks
	// should be mutexed but doesnt matter
	public static int GenCount = 0;

	bool CleanPass = false;
	bool InitialGenFinished = false;

	public const int GENERATE_CHUNKS_AROUND_PLAYER = 5;
	const int DEGENERATE_CHUNKS_AROUND_PLAYER = 12;

	public void StartingArea()
	{
		#pragma warning disable CS0162
		if (Game.DEBUG_NO_PROCGEN) return;
		Mutex.Lock();
		Queue.Push(new(Areas.Start, Vector2I.Zero, 0, false));
		for (int i = 0; i < 4; i++) NextChunks(3);
		for (int i = 0; i < 4; i++) NextChunks(5);
		Mutex.Unlock();
		#pragma warning restore CS0162
	}

	public void PlayerCrossedChunkBoundary(Vector2I to, Vector2I direction)
	{
        static Vector2I RotateCCW(Vector2I v) => new(-v.Y, v.X);

        Mutex.Lock();
		for (int h = -DEGENERATE_CHUNKS_AROUND_PLAYER; h <= DEGENERATE_CHUNKS_AROUND_PLAYER; h++) {
            Degenerate(to + direction * -DEGENERATE_CHUNKS_AROUND_PLAYER + RotateCCW(direction) * h);
        }
        Mutex.Unlock();
	}

	Areas GetAreaFromChunk(Vector2I chunk)
	{
		return chunk switch {
			{Y: < START_POOLS_TRANSITION} => Areas.Pools,
			{Y: START_POOLS_TRANSITION} => Areas.TransStartPools,
			_ => Areas.Start
		};
	}
	
	void NextChunks(int chunks)
	{
		if ((!InitialGenFinished) && CleanPass) {
			InitialGenFinished = true;
			World.InitialProcGenFinished();
		}
		void AddToQueue(Vector2I position, bool clearBefore) {
			if (ChunkStates.ContainsKey(position)) return;
			Queue.Push(new(GetAreaFromChunk(position), position, 0, clearBefore));
		}

		Vector2I position = (Vector2I)(World.Player.Position / PATTERN_CHUNK_SIZE / World.PATTERN_TILE_SIZE).Round();
		for (int layer = chunks; layer > 0; layer--) {
			for (int x = 0; x < layer*2; x++) {
				AddToQueue(position + new Vector2I(layer,layer-x), false);
				AddToQueue(position + new Vector2I(layer-x,-layer), false);
				AddToQueue(position + new Vector2I(-layer,x-layer), false);
				AddToQueue(position + new Vector2I(x-layer,layer), false);
			}
		}
		AddToQueue(position, false);
	}

    public override void _Process(double delta)
	{
		int runs = 0;
		while (runs++ < 30 && !Thread.IsAlive()) {
			Mutex.Lock();
			if (Queue.Count == 0) {
				NextChunks(Game.DEBUG_NO_PROCGEN ? 1 : GENERATE_CHUNKS_AROUND_PLAYER);
				CleanPass = true;
				if (Queue.Count == 0) {
					Mutex.Unlock();
					return;
				}
			}
			if (Thread.IsStarted()) {
				if ((bool)Thread.WaitToFinish()) {
					ConvertedTiles.WriteTileMap();
					PatternTiles.WriteTileMap();
					WaterTiles.WriteTileMap();
				} else CleanPass = false;
			}
			Task task = Queue.Pop();
			Rect2I rect = task.GetRect();
			World.DrawDebug(rect);
			if (task.ClearBefore) {
				TaskReverted(task);
				for (int x = rect.Position.X; x < rect.End.X; x++)
				for (int y = rect.Position.Y; y < rect.End.Y; y++)
					if (!STARTING_AREA.HasPoint(new(x,y))) PatternLayer.SetCell(new Vector2I(x,y));
			}
			if (task.Area > Areas.End) GenerateTransition(task);
			else {
				Model model = Models[(int)task.Area];
				Rect2I convertedRect = new((rect.Position-Vector2I.One)*Model.ConversionScale, (rect.Size+Vector2I.One*2)*Model.ConversionScale);
				PatternTiles = new(rect, Model.PatternSize-Vector2I.One, model.PatternTiles, PatternLayer, (int)task.Area);
				if (!PatternTiles.AnyEmpty()) {
					if (!ChunkStates.ContainsKey(task.Chunk)) GenCount++;
					ChunkStates[task.Chunk] = ChunkState.Generated;
					Mutex.Unlock();
					continue;
				}
				ConvertedTiles = new(convertedRect, Vector2I.Zero, model.ConvertedTiles, ConvertedLayer, (int)task.Area);
				WaterTiles = new(ExpandRect(rect, 1), Vector2I.Zero, WaterLayer, 0);
				Thread.Start(Callable.From(()=>Generate(task)));
			}
			Mutex.Unlock();
		}
    }

	void Degenerate(Vector2I position)
	{
		if (!ChunkStates.ContainsKey(position)) return;
		Rect2I rect = new(position * PATTERN_CHUNK_SIZE, Vector2I.One * PATTERN_CHUNK_SIZE);
		ChunkReverted(position);
		for (int x = rect.Position.X; x < rect.End.X; x++)
		for (int y = rect.Position.Y; y < rect.End.Y; y++) {
			Vector2I patternTile = new(x,y);
			PatternLayer.SetCell(patternTile);
			for (int cx = 0; cx < Model.ConversionScale.X; cx++)
			for (int cy = 0; cy < Model.ConversionScale.Y; cy++)
				ConvertedLayer.SetCell(patternTile*Model.ConversionScale + new Vector2I(cx,cy));
		}

	}

	void TaskReverted(Task task)
	{
		if (task.Expand > 0) {
			for (int x = task.Chunk.X-1; x <= task.Chunk.X+1; x++)
			for (int y = task.Chunk.Y-1; y <= task.Chunk.Y+1; y++)
				ChunkReverted(new(x,y));
		} else ChunkReverted(task.Chunk);
	}

	void ChunkReverted(Vector2I chunk)
	{
		if (ChunkStates.Remove(chunk)) GenCount--;
	}

	void GenerateTransition(Task task)
	{
		
	}

	// returns true if successful
	bool Generate(Task task)
	{
		Rect2I rect = task.GetRect();
		Mutex.Lock();
		int tries = 0;
		while (TryGenerate(task)) {
			tries++;
			for (int x = rect.Position.X; x < rect.End.X; x++)
			for (int y = rect.Position.Y; y < rect.End.Y; y++)
				if (!STARTING_AREA.HasPoint(new(x,y))) PatternTiles.SetTile(new Vector2I(x,y), -1);
			
			if (tries > 3) {
				if (task.CanRetry()) {
					Queue.Push(task.ExpandOnce());
				}
				Mutex.Unlock();
				return false;
			}
		}
		if (!ChunkStates.ContainsKey(task.Chunk)) GenCount++;
		ChunkStates[task.Chunk] = ChunkState.Generated;
		Mutex.Unlock();
		return true;
	}

	// returns true if failed
	bool TryGenerate(Task task)
	{
		Rect2I rect = task.GetRect();
		Model model = Models[(int)task.Area];
		Vector2I patternsMargin = Model.PatternSize - Vector2I.One;
		Vector2I patternsRectSize = rect.Size + patternsMargin;
		// setup
		List<Pattern>[] patterns = new List<Pattern>[patternsRectSize.X*patternsRectSize.Y];
		double[] entropies = new double[rect.Size.X*rect.Size.Y];
		int tilesCompleted = 0;

		for (int x = 0; x < patternsRectSize.X; x++)
		for (int y = 0; y < patternsRectSize.Y; y++)
			patterns[Fold(x,y,patternsRectSize)] = model.MatchPatterns(GetTiles(rect.Position + new Vector2I(x,y) - patternsMargin, Model.PatternSize));
		for (int x = 0; x < rect.Size.X; x++)
		for (int y = 0; y < rect.Size.Y; y++) {
			Vector2I position = new(x,y);
			if (PatternTiles.GetTile(position+rect.Position) != -1) {
				entropies[Fold(x,y,rect.Size)] = -1;
				tilesCompleted++;
			}
			else entropies[Fold(x,y,rect.Size)] = GetEntropy(model, GetNearbyPatterns(position, patterns, patternsRectSize));
		}
		// loop
		while (tilesCompleted < rect.Area) {
			Vector2I collapsePosition = GetLowestEntropy(rect, entropies);
			if (SelectPossibility(model, GetNearbyPatterns(collapsePosition, patterns, patternsRectSize)) is int tile) {
				tilesCompleted++;
				PatternTiles.SetTile(collapsePosition + rect.Position, tile);
				entropies[Fold(collapsePosition,rect.Size)] = -1;
				for (int px = 0; px < Model.PatternSize.X; px++)
				for (int py = 0; py < Model.PatternSize.Y; py++) {
					Vector2I updatePatternPosition = collapsePosition + new Vector2I(px,py);
					patterns[Fold(updatePatternPosition,patternsRectSize)] = model.MatchPatterns(
						GetTiles(updatePatternPosition-patternsMargin+rect.Position,Model.PatternSize),
						patterns[Fold(updatePatternPosition,patternsRectSize)]
					);
				}
				for (int px = 1-Model.PatternSize.X; px < Model.PatternSize.X; px++)
				for (int py = 1-Model.PatternSize.Y; py < Model.PatternSize.Y; py++) {
					Vector2I updateEntropyPosition = collapsePosition + new Vector2I(px,py);
					if (!rect.HasPoint(rect.Position + updateEntropyPosition)) continue;
					if (entropies[Fold(updateEntropyPosition,rect.Size)] == -1) continue;
					entropies[Fold(updateEntropyPosition,rect.Size)] = GetEntropy(model, GetNearbyPatterns(updateEntropyPosition, patterns, patternsRectSize));
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
			WaterTiles.SetTile(rect.Position+position, chosenPattern.Water);
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

	int[] CountTiles(Model model, List<Pattern> patterns, Vector2I at)
	{
		int[] counts = new int[model.PatternTiles.Count];
		foreach (Pattern pattern in patterns) counts[pattern.Tiles[Fold(at,Model.PatternSize)]]++;
		return counts;
	}
	
	double[] CollectPossibilities(Model model, List<Pattern>[] patterns)
	{
		double[] possibilities = new double[model.PatternTiles.Count];
		for (int i = 0; i < model.PatternTiles.Count; i++) possibilities[i] = 1.0;
		for (int x = 0; x < Model.PatternSize.X; x++)
		for (int y = 0; y < Model.PatternSize.Y; y++) {
			int[] tiles = CountTiles(model, patterns[Fold(x,y,Model.PatternSize)], Model.PatternSize - new Vector2I(x,y) - Vector2I.One);
			for (int i = 0; i < model.PatternTiles.Count; i++)
				possibilities[i] *= tiles[i];
		}
		for (int i = 0; i < model.PatternTiles.Count; i++) possibilities[i] = Math.Pow(possibilities[i], INVERSE_TEMPERATURE);
		return possibilities;
	}

	int? SelectPossibility(Model model, List<Pattern>[] patterns)
	{
		double[] possibilities = CollectPossibilities(model, patterns);
		double totalFrequency = possibilities.Sum();
		if (totalFrequency == 0) return null;
		double randomValue = RNG.NextDouble() * totalFrequency;
		double slidingWindow = 0;
		for (int tile = 0; tile < model.PatternTiles.Count; tile++) {
			double slidingWindowNext = slidingWindow + possibilities[tile];
			if (slidingWindow <= randomValue && randomValue < slidingWindowNext) return tile;
			slidingWindow = slidingWindowNext;
		}
		GD.Print($"this shouldnt happen! [{string.Join(", ", possibilities)}], {randomValue}");
		return null;
	}

	double GetEntropy(Model model, List<Pattern>[] patterns)
	{
		double entropy = 0;
		double[] possibilities = CollectPossibilities(model, patterns);
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

// class Task
// {
// 	public bool ClearBefore;
// 	public bool CanRetry;
// 	public Rect2I Rect;
// 	readonly List<Subtask> Subtasks;
// 	public ProceduralGenerator.Areas Area;
// 	int pointer = 0;

// 	const int PATTERN_CHUNK_SIZE = ProceduralGenerator.PATTERN_CHUNK_SIZE;

// 	public Task(ProceduralGenerator.Areas area, Vector2I chunk, bool clearBefore, bool canRetry)
// 	{
// 		Area = area;
// 		ClearBefore = clearBefore;
// 		CanRetry = canRetry;
// 		Subtasks = [new(chunk, area)];
// 	}

// 	// public Task (Rect2I rect, bool clearBefore, bool canRetry)
// 	// {
// 	// 	ClearBefore = clearBefore;
// 	// 	CanRetry = canRetry;
// 	// 	Rect = rect;
// 	// 	Subtasks = [Rect];
// 	// }

// 	public Task (ProceduralGenerator.Areas area, Rect2I rect, bool clearBefore)
// 	{
// 		Area = area;
// 		ClearBefore = clearBefore;
// 		Rect = rect;
// 		Subtasks = [];
// 		CanRetry = rect.Size.X <= ProceduralGenerator.SIZE_THRESHOLD;
// 		Subtasks.Add(Rect);
// 	}

// 	public Subtask Next() => Subtasks[pointer++];
// 	public bool IsEmpty() => pointer == Subtasks.Count;
// 	public bool IsNew() => pointer == 0;
// }

class Task(ProceduralGenerator.Areas area, Vector2I chunk, int expand, bool clearBefore)
{
	public Vector2I Chunk = chunk;
	public int Expand = expand;
	public ProceduralGenerator.Areas Area = area;
	public bool ClearBefore = clearBefore;

	public Rect2I GetRect() => ExpandRect(new(
		Chunk * ProceduralGenerator.PATTERN_CHUNK_SIZE,
		Vector2I.One * ProceduralGenerator.PATTERN_CHUNK_SIZE
	), Expand);
	
	public bool CanRetry() => Expand < ProceduralGenerator.EXPAND_LIMIT;

	public Task ExpandOnce() {
		Expand++;
		ClearBefore = true;
		return this;
	}
}

abstract class TileCache<T>
{
	readonly public Rect2I Rect;
	readonly public Vector2I Margin;
	readonly public TileMapLayer TileMap;
	readonly protected Vector2I TotalSize;
	readonly protected int SourceId;
	readonly protected T[] Tiles;

	public TileCache(Rect2I rect, Vector2I margin, TileMapLayer tileMap, int sourceId)
	{
		Rect = rect;
		Margin = margin;
		TileMap = tileMap;
		SourceId = sourceId;
		TotalSize = new(Rect.Size.X+2*Margin.X, Rect.Size.Y+2*Margin.Y);
		Tiles = new T[TotalSize.X*TotalSize.Y];
	}

	public T GetTile(Vector2I absolutePosition) => Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)];
	public void SetTile(Vector2I absolutePosition, T to) => Tiles[Fold(absolutePosition-Rect.Position+Margin,TotalSize)] = to;
	public abstract void WriteTileMap();
}

class TileExistsCache : TileCache<bool>
{
    public TileExistsCache(Rect2I rect, Vector2I margin, TileMapLayer tileMap, int sourceId)
		: base(rect, margin, tileMap, sourceId)
	{
		for (int x = 0; x < TotalSize.X; x++)
		for (int y = 0; y < TotalSize.Y; y++)
			Tiles[Fold(x,y,TotalSize)] = TileMap.GetCellAtlasCoords(Rect.Position - Margin + new Vector2I(x,y)) != Vector2I.One * -1;
	}

	public override void WriteTileMap()
	{
		for (int x = 0; x < Rect.Size.X; x++)
		for (int y = 0; y < Rect.Size.Y; y++) {
			TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId,
				Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)] ? Vector2I.Zero : Vector2I.One * -1
			);
		}
	}
}

class TileSetCache : TileCache<int>
{
	readonly public EnumeratedTileSet TileSet;

    public TileSetCache(Rect2I rect, Vector2I margin, EnumeratedTileSet tileSet, TileMapLayer tileMap, int sourceId)
		: base(rect, margin, tileMap, sourceId)
	{
		TileSet = tileSet;
		for (int x = 0; x < TotalSize.X; x++)
		for (int y = 0; y < TotalSize.Y; y++)
			Tiles[Fold(x,y,TotalSize)] = TileSet.Convert(TileMap.GetCellAtlasCoords(Rect.Position - Margin + new Vector2I(x,y)));
	}

    public bool AnyEmpty()
	{
		foreach (int tile in Tiles) if (tile == -1) return true;
		return false;
	}
	
	public override void WriteTileMap()
	{
		for (int x = 0; x < Rect.Size.X; x++)
		for (int y = 0; y < Rect.Size.Y; y++)
			TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId,
				TileSet.Convert(Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)])
			);
	}
}

class RotateableTileSetCache : TileSetCache
{
	readonly int[] TileRotations;
    public RotateableTileSetCache(Rect2I rect, Vector2I margin, EnumeratedTileSet tileSet, TileMapLayer tileMap, int sourceId)
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
			TileMap.SetCell(Rect.Position + new Vector2I(x,y), SourceId,
				TileSet.Convert(Tiles[Fold(x+Margin.X,y+Margin.Y,TotalSize)]), TileRotations[Fold(x+Margin.X,y+Margin.Y,TotalSize)]
			);
	}
}

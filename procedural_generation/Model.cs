public class Model()
{
	public static Vector2I PatternSize = new(3,3);
    public static Vector2I ConversionScale = new(2,2);
	public List<Pattern> Patterns = [];
	public EnumeratedTileSet PatternTiles = new();
	public EnumeratedTileSet ConvertedTiles = new();

    public Pattern? MatchPattern(int[] tiles)
    {
        foreach (Pattern pattern in Patterns) if (pattern.Matches(tiles)) return pattern;
        return null;
    }

    public List<Pattern> MatchPatterns(int[] tiles) { return MatchPatterns(tiles, Patterns); }

    public List<Pattern> MatchPatterns(int[] tiles, List<Pattern> patterns)
    {
        static bool IsEmpty(int[] tiles)
        {
            foreach (int tile in tiles) if (tile != -1) return false;
            return true;
        }

        if (IsEmpty(tiles)) return patterns;
        List<Pattern> result = [];
        foreach (Pattern pattern in Patterns) if (pattern.Matches(tiles)) result.Add(pattern);
        return result;
    }
}

public class Pattern
{
    public int Frequency;
    public int[] Tiles;
    public int[] Conversion;
    public int[] ConversionRotation;
    public bool Water;

    public Pattern(int[] tiles, int[] conversion, int[] conversionRotation, bool water)
    {
        Frequency = 1;
        Tiles = tiles;
        Conversion = conversion;
        ConversionRotation = conversionRotation;
        Water = water;
    }

    public Pattern(int frequency, int[] tiles, int[] conversion, int[] conversionRotation, bool water)
    {
        Frequency = frequency;
        Tiles = tiles;
        Conversion = conversion;
        ConversionRotation = conversionRotation;
        Water = water;
    }

    public bool Matches(int[] tiles)
    {
        for (int i = 0; i < tiles.Length; i++) if (tiles[i] != -1 && tiles[i] != Tiles[i]) return false;
        return true;
    }
}

public class EnumeratedTileSet
{
    protected readonly Vector2I EMPTY = new(-1, -1);
    
	public int Count = 0;
    public readonly Dictionary<Vector2I, int> CoordsMap = [];
	public readonly List<Vector2I> CoordsList = [];

	public void RegisterTile(Vector2I TileCoords)
	{
        if (TileCoords == EMPTY) return;
        if (CoordsList.Contains(TileCoords)) return;
		CoordsMap[TileCoords] = Count;
		CoordsList.Add(TileCoords);
		Count++;
	}

    public int Convert(Vector2I tile) => tile == EMPTY ? -1 : CoordsMap[tile];
    public Vector2I Convert(int tile) => tile == -1 ? EMPTY : CoordsList[tile];
}

public class TransitionModel() : Model
{
    public new List<TransitionPattern> Patterns = [];
    public new TransitionEnumeratedTileSet PatternTiles = new();
	public new TransitionEnumeratedTileSet ConvertedTiles = new();

    public TransitionPattern? MatchPattern(int[] tiles, int[] tileSourceIds)
    {
        foreach (TransitionPattern pattern in Patterns) if (pattern.Matches(tiles, tileSourceIds)) return pattern;
        return null;
    }

    public List<TransitionPattern> MatchPatterns(int[] tiles, int[] tileSourceIds) { return MatchPatterns(tiles, tileSourceIds, Patterns); }

    public List<TransitionPattern> MatchPatterns(int[] tiles, int[] tileSourceIds, List<TransitionPattern> patterns)
    {
        static bool IsEmpty(int[] tiles)
        {
            foreach (int tile in tiles) if (tile != -1) return false;
            return true;
        }

        if (IsEmpty(tiles)) return patterns;
        List<TransitionPattern> result = [];
        foreach (TransitionPattern pattern in Patterns) if (pattern.Matches(tiles, tileSourceIds)) result.Add(pattern);
        return result;
    }
}

public class TransitionPattern : Pattern
{
    public int[] TileSourceIds;
    public int[] ConversionSourceIds;

    public TransitionPattern(int[] tiles, int[] conversion, int[] conversionRotation, int[] tileSourceIds, int[] conversionSourceIds, bool water)
        : base(tiles, conversion, conversionRotation, water)
    {
        TileSourceIds = tileSourceIds;
        ConversionSourceIds = conversionSourceIds;
    }

    public TransitionPattern(int frequency, int[] tiles, int[] conversion, int[] conversionRotation, int[] tileSourceIds, int[] conversionSourceIds, bool water)
        : base(frequency, tiles, conversion, conversionRotation, water)
    {
        TileSourceIds = tileSourceIds;
        ConversionSourceIds = conversionSourceIds;
    }

    public bool Matches(int[] tiles, int[] tileSourceIds)
    {
        for (int i = 0; i < tiles.Length; i++) if (tiles[i] != -1 && (tiles[i] != Tiles[i] || tileSourceIds[i] != TileSourceIds[i])) return false;
        return true;
    }
}

public class TransitionEnumeratedTileSet : EnumeratedTileSet
{
    public new readonly Dictionary<Vector3I, int> CoordsMap = [];
	public new readonly List<Vector3I> CoordsList = [];

	public void RegisterTile(Vector3I TileCoords)
	{
        if (xy(TileCoords) == EMPTY) return;
        if (CoordsList.Contains(TileCoords)) return;
		CoordsMap[TileCoords] = Count;
		CoordsList.Add(TileCoords);
		Count++;
	}

    public int Convert(Vector3I tile) => xy(tile) == EMPTY ? -1 : CoordsMap[tile];
    public new Vector3I Convert(int tile) => tile == -1 ? xyz(EMPTY,0) : CoordsList[tile];
}

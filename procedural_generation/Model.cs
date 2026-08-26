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
    readonly Vector2I EMPTY = new(-1, -1);
    
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

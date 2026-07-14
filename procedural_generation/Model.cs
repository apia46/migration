public class Model
{
	public Vector2I PatternSize;
    public Vector2I ConversionScale;
	public List<Pattern> Patterns = [];
	public EnumeratedTileSet PatternTiles = new();
	public EnumeratedTileSet ConvertedTiles = new();

	public Model(Vector2I patternSize, Vector2I conversionScale)
    {
        PatternSize = patternSize;
        ConversionScale = conversionScale;
    }

    public Pattern? MatchPattern(int[] tiles)
    {
        foreach (Pattern pattern in Patterns) if (pattern.Matches(tiles)) return pattern;
        return null;
    }

    public List<Pattern> MatchPatterns(int[] tiles)
    {
        List<Pattern> patterns = [];
        foreach (Pattern pattern in Patterns) if (pattern.Matches(tiles)) patterns.Add(pattern);
        return patterns;
    }
}

public class Pattern
{
    public int Frequency;
    public int[] Tiles;
    public int[] Conversion;

    public Pattern(int[] tiles, int[] conversion)
    {
        Frequency = 1;
        Tiles = tiles;
        Conversion = conversion;
    }

    public Pattern(int frequency, int[] tiles, int[] conversion)
    {
        Frequency = frequency;
        Tiles = tiles;
        Conversion = conversion;
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

    public int Convert(Vector2I tile) { return tile == EMPTY ? -1 : CoordsMap[tile]; }
    public Vector2I Convert(int tile) { return tile == -1 ? EMPTY : CoordsList[tile]; }
}

using System.Linq;
using System.Collections.Generic;
using Godot;
using System.Diagnostics.CodeAnalysis;

[GlobalClass]
public partial class Model : Resource {
	[Export] public Godot.Collections.Array<Godot.Collections.Array> ExportedBasePatterns = [];
	[Export] public int[] BasePatternFrequencies = [];
	[Export] public Vector2I BasePatternSize;
	[Export] public Godot.Collections.Array<Vector2I> ExportedTileAtlasCoords = [];
	[Export] public Godot.Collections.Array<Vector2I> ExportedConvertedTileAtlasCoords = [];
	[Export] public Godot.Collections.Dictionary<int[], int[]> ExportedConversionMap = [];

	public List<int[,]> BasePatterns = [];
	public List<Vector2I> TileAtlasCoords = [];
	public List<Vector2I> ConvertedTileAtlasCoords = [];
	public Dictionary<Vector2I, int> TileAtlasCoordsMap = [];
	public Dictionary<Vector2I, int> ConvertedTileAtlasCoordsMap = [];
	public int TilesCount;
	public Dictionary<int[,], int[,]> ConversionMap = new(4, new ArrayComparer());

	public void ExportProperties()
	{
		ExportedBasePatterns = [.. BasePatterns.ConvertAll(
			pattern=>new Godot.Collections.Array(pattern.Cast<int>().Select(
				tile=>Variant.From(tile))))];
		ExportedTileAtlasCoords = [.. TileAtlasCoords];
		ExportedConvertedTileAtlasCoords = [.. ConvertedTileAtlasCoords];
		// foreach (KeyValuePair<int[,],int[,]> pair in ConversionMap) {
		// 	ExportedConversionMap[[..pair.Key.Cast<int>()]] = [..pair.Value.Cast<int>()];
		// }
		foreach (KeyValuePair<int[,],int[,]> pair in ConversionMap)
		{
			int[] key = new int[4];
			int[] value = new int[4];
			for (int y = 0; y < 2; y++) {
				for (int x = 0; x < 2; x++) {
					int index = y*2 + x;
					key[index] = pair.Key[x,y];
					value[index] = pair.Value[x,y];
				}
			}
			ExportedConversionMap[key] = value;
		}
	}

	public void ImportProperties()
	{
		BasePatterns = [.. ExportedBasePatterns.Select(
			pattern=>{
				int[,] result = new int[BasePatternSize.X, BasePatternSize.Y];
				for (int y = 0; y < BasePatternSize.Y; y++) {
					for (int x = 0; x < BasePatternSize.Y; x++) {
						int index = x*BasePatternSize.Y + y;
						result[x,y] = (int)pattern[index];
					}
				}
				return result;
			})];
		TileAtlasCoords = [.. ExportedTileAtlasCoords, Vector2I.One * -1];
		ConvertedTileAtlasCoords = [.. ExportedConvertedTileAtlasCoords, Vector2I.One * -1];
		for (int i = 0; i < TileAtlasCoords.Count; i++) {
			TileAtlasCoordsMap[TileAtlasCoords[i]] = i;
		}
		for (int i = 0; i < ConvertedTileAtlasCoords.Count; i++) {
			ConvertedTileAtlasCoordsMap[ConvertedTileAtlasCoords[i]] = i;
		}
		TileAtlasCoordsMap[Vector2I.One * -1] = -1;
		ConvertedTileAtlasCoordsMap[Vector2I.One * -1] = -1;
		TilesCount = ExportedTileAtlasCoords.Count;
		foreach (KeyValuePair<int[],int[]> pair in ExportedConversionMap)
		{
			int[,] key = new int[2,2];
			int[,] value = new int[2,2];
			for (int y = 0; y < 2; y++) {
				for (int x = 0; x < 2; x++) {
					int index = y*2 + x;
					key[x,y] = pair.Key[index];
					value[x,y] = pair.Value[index];
				}
			}
			ConversionMap[key] = value;
		}
	}
}

class ArrayComparer : IEqualityComparer<int[,]>
{
    public bool Equals(int[,]? x, int[,]? y)
    {
		if (x is not null && y is not null) return Enumerable.SequenceEqual(x.Cast<int>(), y.Cast<int>());
		else if (x is null && y is null) return true;
		return false;
    }

    public int GetHashCode([DisallowNull] int[,] array)
    {
		int hc = 0;
		foreach (int val in array.Cast<int>()) {
			hc = unchecked(hc * 314159 + val);
		}
        return hc;
    }
}

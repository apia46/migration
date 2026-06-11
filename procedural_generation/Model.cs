using System.Linq;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class Model : Resource {
	[Export] public Godot.Collections.Array<Godot.Collections.Array> ExportedBasePatterns = [];
	[Export] public int[] BasePatternFrequencies = [];
	[Export] public Vector2I BasePatternSize;
	[Export] public Godot.Collections.Array<Vector2I> ExportedTileAtlasCoords = [];

	public List<int[,]> BasePatterns = [];
	public List<Vector2I> TileAtlasCoords = [];
	public Dictionary<Vector2I, int> TileAtlasCoordsMap = [];
	public int TilesCount;

	public void ExportProperties()
	{
		ExportedBasePatterns = [.. BasePatterns.ConvertAll(
			pattern=>new Godot.Collections.Array(pattern.Cast<int>().Select(
				tile=>Variant.From(tile))))];
		ExportedTileAtlasCoords = [.. TileAtlasCoords];
	}

	public void ImportProperties()
	{
		BasePatterns = [.. ExportedBasePatterns.Select<Godot.Collections.Array, int[,]>(
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
		for (int i = 0; i < TileAtlasCoords.Count; i++) {
			TileAtlasCoordsMap[TileAtlasCoords[i]] = i;
		}
		TileAtlasCoordsMap[Vector2I.One * -1] = -1;
		TilesCount = TileAtlasCoords.Count - 1;
	}
}

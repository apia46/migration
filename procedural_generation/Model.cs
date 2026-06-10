using System.Linq;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class Model : Resource {
	[Export] public Godot.Collections.Array<Godot.Collections.Array> ExportedBasePatterns = [];
	[Export] public int[] BasePatternFrequencies = [];
	[Export] public Vector2I BasePatternSize;
	[Export] public Godot.Collections.Array<Vector2I> TileAtlasCoords = [];

	public List<int[]> BasePatterns = [];

	public void ExportProperties()
	{
		ExportedBasePatterns = [.. BasePatterns.ConvertAll(
			pattern=>new Godot.Collections.Array(pattern.Select(
				tile=>Variant.From(tile))))];
	}

	public void ImportProperties()
	{
		BasePatterns = [.. ExportedBasePatterns.Select<Godot.Collections.Array, int[]>(
			pattern=>[.. pattern.Select(
				tile=>(int)tile)])];
	}
}

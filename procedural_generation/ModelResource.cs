[GlobalClass]
public partial class ModelResource : Resource {
	[Export] public Godot.Collections.Array<int[]> PatternTiles = [];
	[Export] public Godot.Collections.Array<int> PatternFrequencies = [];
	[Export] public Godot.Collections.Array<int[]> PatternConversions = [];
	[Export] public Godot.Collections.Array<int[]> PatternConversionRotations = [];
	[Export] public Godot.Collections.Array<Vector2I> PatternTilesCoordsList = [];
	[Export] public Godot.Collections.Array<Vector2I> ConvertedTilesCoordsList =[];

	public ModelResource() {} // godot initialises your resource with no arguments

	public ModelResource(Model model)
	{
		PatternTiles = [.. model.Patterns.ConvertAll(pattern=>pattern.Tiles)];
		PatternFrequencies = [.. model.Patterns.ConvertAll(pattern=>pattern.Frequency)];
		PatternConversions = [.. model.Patterns.ConvertAll(pattern=>pattern.Conversion)];
		PatternConversionRotations = [.. model.Patterns.ConvertAll(pattern=>pattern.ConversionRotation)];
		PatternTilesCoordsList = [.. model.PatternTiles.CoordsList];
		ConvertedTilesCoordsList = [.. model.ConvertedTiles.CoordsList];
	}

	public Model ToModel()
	{
		Model model = new();
		for (int i = 0; i < PatternTiles.Count; i++) {
			model.Patterns.Add(new Pattern(
				PatternFrequencies[i],
				PatternTiles[i],
				PatternConversions[i],
				PatternConversionRotations[i]
			));
		}
		foreach (Vector2I tileCoords in PatternTilesCoordsList) model.PatternTiles.RegisterTile(tileCoords);
		foreach (Vector2I tileCoords in ConvertedTilesCoordsList) model.ConvertedTiles.RegisterTile(tileCoords);
		return model;
	}
}

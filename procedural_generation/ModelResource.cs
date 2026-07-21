[GlobalClass]
public partial class ModelResource : Resource {
	[Export] public Vector2I PatternSize = new(0,0);
	[Export] public Vector2I ConversionScale = new(0,0);
	[Export] public Godot.Collections.Array<int[]> PatternTiles = [];
	[Export] public Godot.Collections.Array<int> PatternFrequencies = [];
	[Export] public Godot.Collections.Array<int[]> PatternConversions = [];
	[Export] public Godot.Collections.Array<Vector2I> PatternTilesCoordsList = [];
	[Export] public Godot.Collections.Array<Vector2I> ConvertedTilesCoordsList =[];

	public ModelResource() {} // godot initialises your resource with no arguments

	public ModelResource(Model model)
	{
		PatternSize = model.PatternSize;
		ConversionScale = model.ConversionScale;
		PatternTiles = [.. model.Patterns.ConvertAll(pattern=>pattern.Tiles)];
		PatternFrequencies = [.. model.Patterns.ConvertAll(pattern=>pattern.Frequency)];
		PatternConversions = [.. model.Patterns.ConvertAll(pattern=>pattern.Conversion)];
		PatternTilesCoordsList = [.. model.PatternTiles.CoordsList];
		ConvertedTilesCoordsList = [.. model.ConvertedTiles.CoordsList];
	}

	public Model ToModel()
	{
		Model model = new(PatternSize, ConversionScale);
		for (int i = 0; i < PatternTiles.Count; i++) {
			model.Patterns.Add(new Pattern(
				PatternFrequencies[i],
				PatternTiles[i],
				PatternConversions[i]
			));
		}
		foreach (Vector2I tileCoords in PatternTilesCoordsList) model.PatternTiles.RegisterTile(tileCoords);
		foreach (Vector2I tileCoords in ConvertedTilesCoordsList) model.ConvertedTiles.RegisterTile(tileCoords);
		return model;
	}
}

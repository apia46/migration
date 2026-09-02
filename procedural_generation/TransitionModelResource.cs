[GlobalClass]
public partial class TransitionModelResource : ModelResource {
    [Export] public Godot.Collections.Array<int[]> PatternTileSourceIds = [];
    [Export] public Godot.Collections.Array<int[]> PatternConversionSourceIds = [];
    [Export] public Godot.Collections.Array<Vector3I> NewPatternTilesCoordsList = [];
	[Export] public Godot.Collections.Array<Vector3I> NewConvertedTilesCoordsList =[];

    public TransitionModelResource() {}

    public TransitionModelResource(TransitionModel model)
    {
        PatternTiles = [.. model.Patterns.ConvertAll(pattern=>pattern.Tiles)];
		PatternFrequencies = [.. model.Patterns.ConvertAll(pattern=>pattern.Frequency)];
		PatternConversions = [.. model.Patterns.ConvertAll(pattern=>pattern.Conversion)];
		PatternConversionRotations = [.. model.Patterns.ConvertAll(pattern=>pattern.ConversionRotation)];
		NewPatternTilesCoordsList = [.. model.PatternTiles.CoordsList];
		NewConvertedTilesCoordsList = [.. model.ConvertedTiles.CoordsList];
		PatternWaters = [.. model.Patterns.ConvertAll(pattern=>pattern.Water)];
        PatternTileSourceIds = [.. model.Patterns.ConvertAll(pattern=>pattern.TileSourceIds)];
		PatternConversionSourceIds = [.. model.Patterns.ConvertAll(pattern=>pattern.ConversionSourceIds)];
    }

    public new TransitionModel ToModel()
    {
        TransitionModel model = new();
		for (int i = 0; i < PatternTiles.Count; i++) {
			model.Patterns.Add(new TransitionPattern(
				Vector2I.Zero,
				PatternFrequencies[i],
				PatternTiles[i],
				PatternConversions[i],
				PatternConversionRotations[i],
                PatternTileSourceIds[i],
                PatternConversionSourceIds[i],
				PatternWaters[i]
			));
		}
		foreach (Vector3I tileCoords in NewPatternTilesCoordsList) model.PatternTiles.RegisterTile(tileCoords);
		foreach (Vector3I tileCoords in NewConvertedTilesCoordsList) model.ConvertedTiles.RegisterTile(tileCoords);
		return model;
    }
}

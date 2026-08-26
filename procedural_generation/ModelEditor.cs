[Tool]
[GlobalClass]
public partial class ModelEditor : Node2D
{
	[Export] public string PATH = "res://procedural_generation/model.tres";

	[ExportToolButton("Generate Model")]
	public Callable GenerateButton => Callable.From(Generate);

	TileMapLayer? PatternLayer;
	TileMapLayer? ConversionLayer;
	TileMapLayer? FlagsLayer;

	public void Generate() {
		PatternLayer = GetNode<TileMapLayer>("%PatternLayer");
		ConversionLayer = GetNode<TileMapLayer>("%ConversionLayer");
		FlagsLayer = GetNodeOrNull<TileMapLayer>("%FlagsLayer");

        Model model = new();

		foreach (Vector2I position in PatternLayer.GetUsedCells()) model.PatternTiles.RegisterTile(PatternLayer.GetCellAtlasCoords(position));
		foreach (Vector2I position in ConversionLayer.GetUsedCells()) model.ConvertedTiles.RegisterTile(ConversionLayer.GetCellAtlasCoords(position));

		foreach (Vector2I position in PatternLayer.GetUsedCells()) {
			if (GetTilesAtCell(position, Model.PatternSize, PatternLayer, model.PatternTiles) is int[] tiles) {
				Vector2I ConversionPosition = position*Model.ConversionScale+(Model.PatternSize-new Vector2I(1,1))*Model.ConversionScale/2;
				if (GetTilesAtCell(ConversionPosition, Model.ConversionScale, ConversionLayer, model.ConvertedTiles) is int[] conversion) {
					if (model.MatchPattern(tiles) is Pattern pattern) {
						if (pattern.Conversion.SequenceEqual(conversion)) pattern.Frequency++;
						else {
							GD.Print($"duplicate with differing conversion at position {position}");
							model.Patterns.Add(new Pattern(tiles, conversion,
								GetTileRotationsAtCell(ConversionPosition, Model.ConversionScale, ConversionLayer),
								GetTileFlagsAtCell(position, FlagsLayer)
							));
						}
					}
					else model.Patterns.Add(new Pattern(tiles, conversion,
						GetTileRotationsAtCell(ConversionPosition, Model.ConversionScale, ConversionLayer),
						GetTileFlagsAtCell(position, FlagsLayer)
					));
				} else GD.Print($"position {position} has no conversion!");
			}
		}

		ModelResource resource = new(model);
		resource.TakeOverPath(PATH);
		ResourceSaver.Save(resource);
		// EditorInterface.Singleton.CallDeferred("edit_resource", resource);
    }

	static PatternFlags GetTileFlagsAtCell(Vector2I position, TileMapLayer? flagsLayer) {
		Vector2I tile = flagsLayer?.GetCellAtlasCoords(position) ?? new(-1,-1);
		return new(
			tile.X == 0
		);
	}

	// returns whether or not to 
	int[]? GetTilesAtCell(Vector2I position, Vector2I size, TileMapLayer layer, EnumeratedTileSet tileset)
	{
		int[] pattern = new int[size.X*size.Y];
		for (int x = 0; x < size.Y; x++) {
			for (int y = 0; y < size.X; y++) {
				Vector2I tilePosition = position + new Vector2I(x, y);
				Vector2I tile = layer.GetCellAtlasCoords(tilePosition);
				if (tile == Vector2I.One * -1) return null;
				pattern[Fold(x,y,size)] = tileset.Convert(tile);
			}
		}
		return pattern;
	}

	int[] GetTileRotationsAtCell(Vector2I position, Vector2I size, TileMapLayer layer)
	{
		int[] pattern = new int[size.X*size.Y];
		for (int x = 0; x < size.Y; x++) {
			for (int y = 0; y < size.X; y++) {
				Vector2I tilePosition = position + new Vector2I(x, y);
				pattern[Fold(x,y,size)] = layer.GetCellAlternativeTile(tilePosition);
			}
		}
		return pattern;
	}
}

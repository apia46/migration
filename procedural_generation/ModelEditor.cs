using System.Linq;
using System.Collections.Generic;
using Godot;

[Tool]
[GlobalClass]
public partial class ModelEditor : TileMapLayer
{
	[Export] public Vector2I BasePatternSize;

	[ExportToolButton("Generate Model")]
	public Callable GenerateButton => Callable.From(Generate);

	TileMapLayer? ConversionLayer;

	public void Generate() {
		ConversionLayer = GetNode<TileMapLayer>("%ConversionLayer");

        Model model = new() { BasePatternSize = BasePatternSize };
		List<int> frequencies = [];

		foreach (Vector2I position in GetUsedCells()) {
			Vector2I tile = GetCellAtlasCoords(position);
			if (tile != Vector2I.One * -1 && !model.TileAtlasCoords.Contains(tile)) model.TileAtlasCoords.Add(tile);
		}
		foreach (Vector2I position in ConversionLayer.GetUsedCells()) {
			Vector2I tile = ConversionLayer.GetCellAtlasCoords(position);
			if (tile != Vector2I.One * -1 && !model.ConvertedTileAtlasCoords.Contains(tile)) model.ConvertedTileAtlasCoords.Add(tile);
		}
		
		Dictionary<Vector2I, int> tileAtlasCoordsMap = [];
		for (int i = 0; i < model.TileAtlasCoords.Count; i++) {
			tileAtlasCoordsMap[model.TileAtlasCoords[i]] = i;
		}
		Dictionary<Vector2I, int> convertedTileAtlasCoordsMap = [];
		for (int i = 0; i < model.ConvertedTileAtlasCoords.Count; i++) {
			convertedTileAtlasCoordsMap[model.ConvertedTileAtlasCoords[i]] = i;
		}

		foreach (Vector2I position in GetUsedCells()) {
			int[,] pattern = new int[BasePatternSize.X, BasePatternSize.Y];
			GenerateConversion(position, model.ConversionMap, tileAtlasCoordsMap, convertedTileAtlasCoordsMap);
			if (IterateAtCell(pattern, position, tileAtlasCoordsMap)) continue;
			for (int i = 0; i < model.BasePatterns.Count; i++) {
				if (pattern.Cast<int>().SequenceEqual(model.BasePatterns[i].Cast<int>())) {
					frequencies[i]++;
					goto cont;
				}
			}
			model.BasePatterns.Add(pattern);
			frequencies.Add(1);
			cont: continue;
		}
		model.BasePatternFrequencies = [.. frequencies];
		model.ExportProperties();
		GD.Print(ResourceSaver.Save(model, "res://model.tres"));
    }

	int ConvertTile(Vector2I tile)
	{
		return 0;
	}

	// returns whether or not to cancel
	bool IterateAtCell(int[,] pattern, Vector2I position, Dictionary<Vector2I, int> tileAtlasCoordsMap) {
		for (int x = 0; x < BasePatternSize.Y; x++) {
			for (int y = 0; y < BasePatternSize.X; y++) {
				Vector2I tilePosition = position + new Vector2I(x, y);
				Vector2I tile = GetCellAtlasCoords(tilePosition);
				if (tile == Vector2I.One * -1) return true;
				pattern[x,y] = tileAtlasCoordsMap[tile];
			}
		}
		return false;
	}

	void GenerateConversion(Vector2I position, Dictionary<int[,], int[,]> ConversionMap, Dictionary<Vector2I, int> tileAtlasCoordsMap, Dictionary<Vector2I, int> convertedTileAtlasCoordsMap) {
		int[,] pattern = new int[2,2];
		int[,] result = new int[2,2];
		for (int x = 0; x < 2; x++) {
			for (int y = 0; y < 2; y++) {
				Vector2I tilePosition = position + new Vector2I(x, y);
				Vector2I tile = GetCellAtlasCoords(tilePosition);
				if (tile == Vector2I.One * -1) return;
				pattern[x,y] = tileAtlasCoordsMap[tile];
				Vector2I resultPosition = position * 2 + Vector2I.One + new Vector2I(x, y);
				Vector2I resultTile = ConversionLayer!.GetCellAtlasCoords(resultPosition);
				if (resultTile == Vector2I.One * -1) return;
				result[x,y] = convertedTileAtlasCoordsMap[resultTile];
				}
		}
		ConversionMap[pattern] = result;
	}
}

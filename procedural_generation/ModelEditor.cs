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

	public void Generate() {
        Model model = new() { BasePatternSize = BasePatternSize };
		List<int> frequencies = [];

		foreach (Vector2I position in GetUsedCells()) {
			Vector2I tile = GetCellAtlasCoords(position);
			if (tile != Vector2I.One * -1 && !model.TileAtlasCoords.Contains(tile)) model.TileAtlasCoords.Add(tile);
		}

		Dictionary<Vector2I, int> tileAtlasCoordsMap = [];
		for (int i = 0; i < model.TileAtlasCoords.Count; i++) {
			tileAtlasCoordsMap[model.TileAtlasCoords[i]] = i;
		}

		foreach (Vector2I position in GetUsedCells()) {
			int[,] pattern = new int[BasePatternSize.X, BasePatternSize.Y];
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
				Vector2I tilePosition = new(position.X + x, position.Y + y);
				Vector2I tile = GetCellAtlasCoords(tilePosition);
				if (tile == Vector2I.One * -1) return true;
				pattern[x,y] = tileAtlasCoordsMap[tile];
			}
		}
		return false;
	}
}

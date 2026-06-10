using System.Linq;
using System.Collections.Generic;
using Godot;

[Tool] [GlobalClass]
public partial class ModelEditor : TileMapLayer
{
	[Export] public Vector2I BasePatternSize;

	[ExportToolButton("Generate")]
	public Callable GenerateButton => Callable.From(Generate);

	public void Generate() {
        Model model = new() { BasePatternSize = BasePatternSize };
		List<int> frequencies = [];
		List<Vector2I[]> patterns = [];

		foreach (Vector2I position in GetUsedCells()) {
			Vector2I[] pattern = new Vector2I[BasePatternSize.X * BasePatternSize.Y];
			if (IterateAtCell(model, pattern, position)) continue;
			for (int i = 0; i < patterns.Count; i++) {
				if (pattern.SequenceEqual(patterns[i])) {
					frequencies[i]++;
					goto cont;
				}
			}
			patterns.Add(pattern);
			frequencies.Add(1);
			cont: continue;
		}
		model.BasePatterns = [..patterns.ConvertAll<int[]>(pattern=>[..pattern.Select(tile=>ConvertTile(tile))])];
		model.BasePatternFrequencies = [.. frequencies];
		model.ExportProperties();
    }

	int ConvertTile(Vector2I tile)
	{
		return 0;
	}

	// returns whether or not to cancel
	bool IterateAtCell(Model model, Vector2I[] pattern, Vector2I position) {
		for (int x = 0; x < BasePatternSize.Y; x++) {
			for (int y = 0; y < BasePatternSize.X; y++) {
				Vector2I tilePosition = new(position.X + x, position.Y + y);
				Vector2I tile = GetCellAtlasCoords(tilePosition);
				if (tile == Vector2I.One * -1) return true;
				if (!model.TileAtlasCoords.Contains(tile)) model.TileAtlasCoords.Add(tile);
				pattern[y*BasePatternSize.X + x] = tile;
			}
		}
		return false;
	}
}

using System.Linq;
using System.Collections.Generic;
using Godot;

[Tool]
[GlobalClass]
public partial class ModelEditor : Node2D
{
	[Export] public Vector2I BasePatternSize;
	[Export] public Vector2I AbstractScale;

	[ExportToolButton("Generate Model")]
	public Callable GenerateButton => Callable.From(Generate);

	TileMapLayer abstractLayer;
	TileMapLayer concreteLayer;

	public void Generate() {
		abstractLayer = GetNode<TileMapLayer>("%AbstractLayer");
		concreteLayer = GetNode<TileMapLayer>("%ConcreteLayer");

        Model model = new() { BasePatternSize = BasePatternSize };
		List<int> frequencies = [];

		foreach (Vector2I position in abstractLayer.GetUsedCells()) {
			Vector2I tile = abstractLayer.GetCellAtlasCoords(position);
			if (tile != Vector2I.One * -1 && !model.TileAtlasCoordsAbstract.Contains(tile)) model.TileAtlasCoordsAbstract.Add(tile);
		}
		foreach (Vector2I position in concreteLayer.GetUsedCells()) {
			Vector2I tile = concreteLayer.GetCellAtlasCoords(position);
			if (tile != Vector2I.One * -1 && !model.TileAtlasCoordsConcrete.Contains(tile)) model.TileAtlasCoordsConcrete.Add(tile);
		}

		Dictionary<Vector2I, int> tileAtlasCoordsMapAbstract = [];
		for (int i = 0; i < model.TileAtlasCoordsAbstract.Count; i++) {
			tileAtlasCoordsMapAbstract[model.TileAtlasCoordsAbstract[i]] = i;
		}
		Dictionary<Vector2I, int> tileAtlasCoordsMapConcrete = [];
		for (int i = 0; i < model.TileAtlasCoordsConcrete.Count; i++) {
			tileAtlasCoordsMapConcrete[model.TileAtlasCoordsConcrete[i]] = i;
		}

		foreach (Vector2I position in abstractLayer.GetUsedCells()) {
			int[,] pattern = new int[BasePatternSize.X, BasePatternSize.Y];
			int[,] patternConcrete = new int[BasePatternSize.X*AbstractScale.X, BasePatternSize.Y*AbstractScale.Y];
			if (IterateAtCell(pattern, patternConcrete, position, tileAtlasCoordsMapAbstract, tileAtlasCoordsMapConcrete)) continue;
			for (int i = 0; i < model.BasePatternsAbstract.Count; i++) {
				if (pattern.Cast<int>().SequenceEqual(model.BasePatternsAbstract[i].Cast<int>())) {
					frequencies[i]++;
					goto cont;
				}
			}
			model.BasePatternsAbstract.Add(pattern);
			model.BasePatternsConcrete.Add(pattern);
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
	bool IterateAtCell(int[,] pattern, int[,] patternConcrete, Vector2I position, Dictionary<Vector2I, int> tileAtlasCoordsMapAbstract, Dictionary<Vector2I, int> tileAtlasCoordsMapConcrete) {
		for (int x = 0; x < BasePatternSize.Y; x++) {
			for (int y = 0; y < BasePatternSize.X; y++) {
				Vector2I tilePosition = new(x, y);
				Vector2I tile = abstractLayer.GetCellAtlasCoords(tilePosition + position);
				if (tile == Vector2I.One * -1) return true;
				pattern[x,y] = tileAtlasCoordsMapAbstract[tile];
				for (int sy = 0; sy < AbstractScale.Y; sy++) {
					for (int sx = 0; sx < AbstractScale.X; sx++) {
						Vector2I concreteTilePosition = tilePosition * AbstractScale + new Vector2I(sx, sy);
						Vector2I concreteTile = concreteLayer.GetCellAtlasCoords(concreteTilePosition + position * AbstractScale);
						patternConcrete[concreteTilePosition.X, concreteTilePosition.Y] = tileAtlasCoordsMapConcrete[concreteTile];
					}
				}
			}
		}
		return false;
	}
}

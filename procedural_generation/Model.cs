using System.Linq;
using System.Collections.Generic;
using Godot;
using System;

[GlobalClass]
public partial class Model : Resource {
	[Export] public Godot.Collections.Array<int[]> ExportedBasePatternsAbstract = [];
	[Export] public Godot.Collections.Array<int[]> ExportedBasePatternsConcrete = [];
	[Export] public int[] BasePatternFrequencies = [];
	[Export] public Vector2I BasePatternSize;
	[Export] public Vector2I AbstractScale;
	[Export] public Godot.Collections.Array<Vector2I> ExportedTileAtlasCoordsAbstract = [];
	[Export] public Godot.Collections.Array<Vector2I> ExportedTileAtlasCoordsConcrete = [];

	public List<int[,]> BasePatternsAbstract = [];
	public List<int[,]> BasePatternsConcrete = [];
	public List<Vector2I> TileAtlasCoordsAbstract = [];
	public List<Vector2I> TileAtlasCoordsConcrete = [];
	public Dictionary<Vector2I, int> TileAtlasCoordsMapAbstract = [];
	public Dictionary<Vector2I, int> TileAtlasCoordsMapConcrete = [];
	public int TilesCountAbstract;
	public int TilesCountConcrete;

	public void ExportProperties()
	{
		ExportedBasePatternsAbstract = [.. BasePatternsAbstract.ConvertAll<int[]>(pattern=>[..pattern.Cast<int>()])];
		ExportedBasePatternsConcrete = [.. BasePatternsConcrete.ConvertAll<int[]>(pattern=>[..pattern.Cast<int>()])];
		ExportedTileAtlasCoordsAbstract = [.. TileAtlasCoordsAbstract];
	}

	public void ImportProperties()
	{
		BasePatternsAbstract = [.. ExportedBasePatternsAbstract.Select(
			pattern=>{
				int[,] result = new int[BasePatternSize.X, BasePatternSize.Y];
				for (int y = 0; y < BasePatternSize.Y; y++) {
					for (int x = 0; x < BasePatternSize.Y; x++) {
						int index = x*BasePatternSize.Y + y;
						result[x,y] = pattern[index];
					}
				}
				return result;
			})];
		BasePatternsConcrete = [.. ExportedBasePatternsConcrete.Select(
			pattern=>{
				int[,] result = new int[BasePatternSize.X, BasePatternSize.Y];
				for (int y = 0; y < BasePatternSize.Y; y++) {
					for (int x = 0; x < BasePatternSize.Y; x++) {
						int index = x*BasePatternSize.Y + y;
						result[x,y] = pattern[index];
					}
				}
				return result;
			})];
		TileAtlasCoordsAbstract = [.. ExportedTileAtlasCoordsAbstract, Vector2I.One * -1];
		for (int i = 0; i < TileAtlasCoordsAbstract.Count; i++) {
			TileAtlasCoordsMapAbstract[TileAtlasCoordsAbstract[i]] = i;
		}
		TileAtlasCoordsConcrete = [.. ExportedTileAtlasCoordsConcrete, Vector2I.One * -1];
		for (int i = 0; i < TileAtlasCoordsConcrete.Count; i++) {
			TileAtlasCoordsMapConcrete[TileAtlasCoordsConcrete[i]] = i;
		}
		TileAtlasCoordsMapAbstract[Vector2I.One * -1] = -1;
		TileAtlasCoordsMapConcrete[Vector2I.One * -1] = -1;
		TilesCountAbstract = ExportedTileAtlasCoordsAbstract.Count;
		TilesCountConcrete = ExportedTileAtlasCoordsConcrete.Count;
	}
}

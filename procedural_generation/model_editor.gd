@tool
extends TileMapLayer

@export var base_pattern_size:Vector2i = Vector2i(2,2)
@export_tool_button("Generate Model") var g = generate

func generate() -> void:
	var model = ProceduralModel.new()
	model.base_pattern_size = base_pattern_size
	for pos in get_used_cells():
		var pattern:TileMapPattern = TileMapPattern.new()
		pattern.set_size(base_pattern_size)
		var no_pattern:bool = false
		for y in base_pattern_size.y:
			for x in base_pattern_size.x:
				var tile:Vector2i = get_cell_atlas_coords(pos+Vector2i(x,y))
				if tile not in model.tile_atlas_coords: model.tile_atlas_coords.append(tile)
				if tile == Vector2i(-1, -1):
					no_pattern = true
					break
				pattern.set_cell(Vector2i(x,y), 0, tile)
			if no_pattern: break
		if no_pattern: continue
		var matched:bool = false
		for i in len(model.base_patterns):
			if matches(pattern, model.base_patterns[i]):
				matched = true
				model.base_pattern_frequencies[i] += 1
				break
		if !matched:
			model.base_patterns.append(pattern)
			model.base_pattern_frequencies.append(1)
	ResourceSaver.save(model, "res://model.tres")

func matches(pattern:TileMapPattern, check_pattern:TileMapPattern) -> int:
	for y in base_pattern_size.y:
		for x in base_pattern_size.x:
			if pattern.get_cell_atlas_coords(Vector2i(x,y)) != check_pattern.get_cell_atlas_coords(Vector2i(x,y)):
				return false
	return true

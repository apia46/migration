extends Node
class_name ProceduralGenerator

signal queue_empty

var tile_map_layer:TileMapLayer
var model:ProceduralModel

var queue:Array[Rect2i] = []

var initial_gen_rect:Rect2i
var gen_rect:Rect2i
var tile_possibilities:Array[Array]
var entropies:Array[int]
var failed:bool = false
var fails:int = 0
var tiles_changed:Array[bool]
var tiles_completed:int
var cache:PackedInt32Array # all the tiles in Rect2i(gen_rect.position+Vector2i.ONE-model.base_pattern_size, gen_rect.size-Vector2i.ONE+model.base_pattern_size)

var lowest_index:int
var lowest_position:Vector2i

var next_tick:Result = Result.NEXT
enum Result { NEXT, RETRY, ADVANCE }

var ticks:int = 10

var setupped:bool = false

const EXPAND_MAX:int = 3

func _process(delta: float) -> void:
	print(delta," ", ticks)
	# setupped = false
	# for i in ceil(ticks):
	# 	tick()
	# if delta > 0.1 and ticks > 1 and !setupped: ticks -= 0.25
	# if delta < 0.03 and ticks < 10 and !setupped: ticks += 0.25
	for i in ticks:
		tick()

func set_context(_tile_map_layer:TileMapLayer, _model:ProceduralModel) -> void:
	tile_map_layer = _tile_map_layer
	model = _model
	cache = []
	cache.resize((6+(model.base_pattern_size.x+EXPAND_MAX)*2-2)*(6+(model.base_pattern_size.y+EXPAND_MAX)*2-2)*2)

func tick() -> void:
	match next_tick:
		Result.NEXT:
			var next = queue.pop_back()
			if !next:
				queue_empty.emit()
				return
			fails = 0
			initial_gen_rect = next
			build_cache(next)
			next_tick = setup(next)
		Result.RETRY:
			fails += 1
			@warning_ignore("integer_division")
			var size_increase:int = min(EXPAND_MAX, fails/4)
			var rect:Rect2i = Rect2i(initial_gen_rect.position-Vector2i.ONE*size_increase, initial_gen_rect.size+Vector2i.ONE*size_increase*2)
			if rect != gen_rect: build_cache(rect)
			next_tick = setup(rect)
		Result.ADVANCE: next_tick = advance()

func setup(rect:Rect2i) -> Result:
	gen_rect = rect
	# for each tile, for each overlap, possibility frequencies
	# array of array of dictionary[vector2i, int]
	tile_possibilities = []
	entropies = []
	tiles_changed = []
	tiles_completed = 0
	setupped = true

	for y in rect.size.y:
		for x in rect.size.x:
			if read_cache(Vector2i(x,y)) != Vector2i(-1,-1):
				tile_possibilities.append([])
				entropies.append(-1)
				tiles_changed.append(false)
				tiles_completed += 1
				continue
			var possibilities_set:Array[Dictionary] = []
			for py in range(1-model.base_pattern_size.y, 1):
				for px in range(1-model.base_pattern_size.x, 1): # in every placement overlapping this tile,
					var possibilities:Dictionary[Vector2i, int] = {}
					for tile in model.tile_atlas_coords: possibilities[tile] = 0
					var pattern_index:int = 0
					for pattern in model.base_patterns: # check each pattern there
						if matches(pattern, Vector2i(x+px, y+py)+rect.position): # if the pattern matches there
							# add the possibility
							var tile:Vector2i = pattern.get_cell_atlas_coords(-Vector2i(px, py))
							possibilities[tile] += model.base_pattern_frequencies[pattern_index]
						pattern_index += 1
					possibilities_set.append(possibilities)
			tile_possibilities.append(possibilities_set)
			entropies.append(get_entropy(possibilities_set))
			tiles_changed.append(false)
	
	if tiles_completed > rect.size.x * rect.size.y:
		return Result.NEXT
	
	lowest_index = get_lowest(entropies)
	@warning_ignore("integer_division")
	lowest_position = Vector2i(lowest_index % rect.size.x, lowest_index / rect.size.x)

	select_possibility(tile_possibilities[lowest_index], rect.position+lowest_position)
	
	tile_possibilities[lowest_index] = []
	entropies[lowest_index] = -1
	tiles_completed += 1
	return Result.ADVANCE

# O(p s^4 k^2) for patterns p, pattern size s, kernel size k... pretty bad
func advance() -> Result:
	# we have just completed a tile, and need to propagate the effects
	var pattern_index:int = 0
	for pattern in model.base_patterns: # for every pattern
		for y in range(1-pattern.get_size().y, 1):
			for x in range(1-pattern.get_size().x, 1): # in every placement overlapping the just completed tile,
				if newly_unmatches(pattern, gen_rect.position+lowest_position+Vector2i(x,y), gen_rect.position+lowest_position): # if this possibility is no longer valid,
					#print("newly unmatched pattern %s at %s, placement %s" % [pattern_index, lowest_position, Vector2i(x,y)])
					for py in range(0, pattern.get_size().y):
						for px in range(0, pattern.get_size().x): # for every cell in the pattern at that position,
							var cell_position:Vector2i = lowest_position+Vector2i(x,y)+Vector2i(px,py)
							var index:int = cell_position.x + cell_position.y*gen_rect.size.x
							if !gen_rect.has_point(cell_position+gen_rect.position): continue # if the cell is within the kernel,
							if entropies[index] == -1:
								#print("skipping            tile %s (%s) because it was already completed" % [index, cell_position])
								continue # and it isnt already completed,
							# remove the possibility
							var overlap_index = model.base_pattern_size.x-px-1 + (model.base_pattern_size.y-py-1)*model.base_pattern_size.x
							var cell:Vector2i = pattern.get_cell_atlas_coords(Vector2i(px,py))
							#print("removing %s for tile %s (%s) at overlap %s (%s) because of unmatched pattern %s at %s, placement %s (freq. %s)" % [cell, index, cell_position, overlap_index, Vector2i(px+1, py+1)-base_pattern_size, pattern_index, lowest_position+Vector2i(x,y), Vector2i(x,y), base_pattern_frequencies[pattern_index]])
							tile_possibilities[index][overlap_index][cell] -= model.base_pattern_frequencies[pattern_index]
							tiles_changed[index] = true
							assert(tile_possibilities[index][overlap_index][cell] >= 0)
				elif failed:
					failed = false
					for fx in gen_rect.size.x:
						for fy in gen_rect.size.y:
							write_cache(Vector2i(fx,fy), Vector2i(-1, -1))
					return Result.RETRY
		pattern_index += 1
	#print("patterns removed, %s" % [tile_possibilities])
	# update entropies
	for index in len(tiles_changed):
		if tiles_changed[index]:
			entropies[index] = get_entropy(tile_possibilities[index])
			tiles_changed[index] = false
	#await get_tree().process_frame
	# select the next tile
	lowest_index = get_lowest(entropies)
	@warning_ignore("integer_division")
	lowest_position = Vector2i(lowest_index % gen_rect.size.x, lowest_index / gen_rect.size.x)
	#print("lowest tile: %s (%s), possibilities: %s" % [lowest_index, lowest_position, collect_possibilities(tile_possibilities[lowest_index])])
	# complete the next tile
	select_possibility(tile_possibilities[lowest_index], gen_rect.position + lowest_position)
	
	tile_possibilities[lowest_index] = []
	entropies[lowest_index] = -1
	tiles_completed += 1

	if tiles_completed == gen_rect.size.x * gen_rect.size.y:
		set_cells_from_cache()
		return Result.NEXT
	return Result.ADVANCE

func collect_possibilities(possibilities_set:Array[Dictionary]) -> Dictionary[Vector2i, float]:
	var tile_frequencies:Dictionary[Vector2i, float] = {}
	for tile in model.tile_atlas_coords:
		tile_frequencies[tile] = 1.0
		for possibilities in possibilities_set:
			assert(possibilities[tile] >= 0)
			tile_frequencies[tile] *= possibilities[tile]
	for tile in tile_frequencies:
		tile_frequencies[tile] **= 0.25
	return tile_frequencies

func matches(pattern:TileMapPattern, pos:Vector2i) -> bool:
	for x in pattern.get_size().x: for y in pattern.get_size().y:
		var check_cell:Vector2i = read_cache(pos + Vector2i(x,y) - gen_rect.position)
		if check_cell == Vector2i(-1, -1): continue
		if check_cell != pattern.get_cell_atlas_coords(Vector2i(x,y)): return false
	return true

# if adding the tile at pos made the pattern unsatisfied
func newly_unmatches(pattern:TileMapPattern, pattern_pos:Vector2i, new_cell:Vector2i) -> bool:
	if read_cache(new_cell - gen_rect.position) == Vector2i(-1,-1):
		#print("FAIL!")
		failed = true
		return false
	for x in pattern.get_size().x: for y in pattern.get_size().y:
		var check_cell:Vector2i = read_cache(pattern_pos+Vector2i(x,y) - gen_rect.position)
		if check_cell == Vector2i(-1, -1): continue
		if (check_cell == pattern.get_cell_atlas_coords(Vector2i(x,y))) == (pattern_pos+Vector2i(x,y) == new_cell): return false
	return true

func select_possibility(possibilities_set:Array[Dictionary], pos:Vector2i) -> void:
	var total_frequency:float = 0
	var possibilities:Dictionary[Vector2i, float] = collect_possibilities(possibilities_set)
	for tile in possibilities: total_frequency += possibilities[tile]
	var value:float = randf_range(0, total_frequency-1)
	var sliding_window:float = 0
	for tile in possibilities:
		if sliding_window <= value and value < sliding_window + possibilities[tile]:
			#print("selected tile: " + str(tile) + " at: " + str(pos))
			#print("tiles completed: " + str(tiles_completed))
			write_cache(pos - gen_rect.position, tile)
			return
		sliding_window += possibilities[tile]

func get_entropy(possibilities_set:Array[Dictionary]) -> int:
	var total_frequency:float = 0
	var possibilities:Dictionary[Vector2i, float] = collect_possibilities(possibilities_set)
	for tile in possibilities: total_frequency += possibilities[tile]
	var total_entropy:float = 0
	var scaling:float = 1.0/total_frequency
	for tile in possibilities:
		var chance:float = possibilities[tile] * scaling
		if chance < 0.01: continue
		total_entropy -= chance * log(chance)
	return int(total_entropy * 1000) + randi_range(0, 8)

func get_lowest(array:Array[int]) -> int:
	var lowest_val:int = 9999999999
	var get_lowest_index:int = -1
	for i in len(array):
		if array[i] == -1: continue
		if array[i] < lowest_val:
			lowest_val = array[i]
			get_lowest_index = i
	assert(get_lowest_index != -1)
	return get_lowest_index

func build_cache(rect:Rect2i) -> void:
	var index:int = 0
	for y in range(rect.position.y+1-model.base_pattern_size.y, rect.end.y-1+model.base_pattern_size.y):
		for x in range(rect.position.x+1-model.base_pattern_size.x, rect.end.x-1+model.base_pattern_size.x):
			var tile:Vector2i = tile_map_layer.get_cell_atlas_coords(Vector2i(x,y))
			cache[index] = tile.x
			cache[index+1] = tile.y
			index += 2

func set_cells_from_cache() -> void:
	for y in range(gen_rect.position.y, gen_rect.end.y):
		for x in range(gen_rect.position.x, gen_rect.end.x):
			var pos:Vector2i = Vector2i(x, y)
			tile_map_layer.set_cell(pos, 0, read_cache(pos-gen_rect.position))

func read_cache(pos:Vector2i) -> Vector2i:
	var index:int = cache_index_from_position(pos)
	return Vector2i(cache[index], cache[index+1])

func write_cache(pos:Vector2i, tile:Vector2i) -> void:
	var index:int = cache_index_from_position(pos)
	cache[index] = tile.x
	cache[index+1] = tile.y

func cache_index_from_position(pos:Vector2i) -> int:
	# var padding:Vector2i = Vector2i(model.base_pattern_size.x - 1, model.base_pattern_size.y - 1)
	# pos -= gen_rect.position
	# return 2*(pos.x+padding.x + (pos.y+padding.y)*(gen_rect.size.x+padding.x*2))
	return 2*(pos.x+model.base_pattern_size.x-1 + (pos.y+model.base_pattern_size.y-1)*(gen_rect.size.x+model.base_pattern_size.x*2-2))

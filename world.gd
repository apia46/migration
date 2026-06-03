extends Node2D

@onready var generator:ProceduralGenerator = %ProceduralGenerator

var l:int = 0

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	generator.set_context(%tiles, preload("res://procedural_generation/model.tres"))
	generator.queue.append(chunk_at(Vector2i.ZERO))
	generator.queue_empty.connect(next_chunk)

func next_chunk() -> void:
	l += 1
	for x in l*2:
		generator.queue.append(chunk_at(Vector2i(l,l-x)))
		generator.queue.append(chunk_at(Vector2i(l-x,-l)))
		generator.queue.append(chunk_at(Vector2i(-l,x-l)))
		generator.queue.append(chunk_at(Vector2i(x-l,l)))

func chunk_at(pos:Vector2i) -> Rect2i:
	#print(pos)
	return Rect2i(pos*6, Vector2i.ONE*8)

extends CharacterBody2D

const SPEED = 3000.0
const JUMP_VELOCITY = -350.0
const WALL_JUMP_IMPULSE = 300
const GRAVITY = 1000
const DOUBLE_JUMP_REDIRECT = 250

var double_jump_available:bool = true

var coyote_time:float = 0

func _physics_process(delta: float) -> void:
	var horizontal_control:float = 1.0 if is_on_floor() else 0.2
	var direction := Input.get_axis("ui_left", "ui_right")

	if Input.is_action_just_pressed("ui_accept"):
		if is_on_wall_only():
			velocity.y = JUMP_VELOCITY
			velocity.x = get_wall_normal().x * WALL_JUMP_IMPULSE
			double_jump_available = true
		elif (is_on_floor() or double_jump_available):
			if !is_on_floor():
				#if !coyote_time: double_jump_available = false
				if direction and direction * velocity.x < DOUBLE_JUMP_REDIRECT: velocity.x = direction * DOUBLE_JUMP_REDIRECT
			velocity.y = JUMP_VELOCITY

	if direction:
		velocity.x += direction * SPEED * delta * horizontal_control
	else:
		velocity.x = move_toward(velocity.x, 0, SPEED * delta * horizontal_control)

	if is_on_floor():
		double_jump_available = true
		coyote_time = 0.2
		velocity.x *= 0.8
	else:
		velocity.y += delta * GRAVITY
		coyote_time = max(0, coyote_time-delta)
		velocity.x *= 0.98

	move_and_slide()

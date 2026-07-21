public partial class Player : CharacterBody2D
{
    const float MOVE_SPEED = 3000.0f;
    const float JUMP_VELOCITY = -350.0f;
    const float WALL_JUMP_IMPULSE = 300.0f;
    const float DOUBLE_JUMP_REDIRECT = 250.0f;

    #nullable disable
    Area2D grabArea;
    #nullable enable

    bool doubleJumpAvailable = false;
    double coyoteTime = 0.0f;

    Aawaga? grabbed = null;

    public double Hunger = 1.0;
    public double Stillness = 0.0;
    const double STILLNESS_CUTOFF = 600000;
    const float STILLNESS_DECAY = 0.998f;

    Vector2 distanceAccum = new();

    public override void _Ready()
    {
        grabArea = GetNode<Area2D>("%grabArea");
    }

    public override void _PhysicsProcess(double delta)
    {
        Hunger -= delta * 0.02;

        float horizontalControl = IsOnFloor() ? 1.0f : 0.2f;
        float moveDirection = Input.GetAxis("move_left", "move_right");

        Vector2 newVelocity = Velocity;

        if (Input.IsActionJustPressed("jump")) {
            if (IsOnWallOnly()) {
                newVelocity.X = GetWallNormal().X * WALL_JUMP_IMPULSE;
                doubleJumpAvailable = true;
                newVelocity.Y = JUMP_VELOCITY;
            } else if (IsOnFloor() || coyoteTime > 0.0) {
                newVelocity.Y = JUMP_VELOCITY;
            } else if (doubleJumpAvailable) {
                doubleJumpAvailable = false;
                newVelocity.Y = JUMP_VELOCITY;
                if (moveDirection != 0.0f && moveDirection * Velocity.X < DOUBLE_JUMP_REDIRECT)
                    newVelocity.X = moveDirection * DOUBLE_JUMP_REDIRECT;
            }
        }

        if (moveDirection != 0.0f) newVelocity.X += moveDirection * MOVE_SPEED * (float)delta * horizontalControl;
        else newVelocity.X = Mathf.MoveToward(newVelocity.X, 0.0f, MOVE_SPEED * (float)delta * horizontalControl);

        if (IsOnFloor()) {
            doubleJumpAvailable = true;
            coyoteTime = 0.2f;
            newVelocity.X *= 0.8f;
        } else {
            newVelocity.Y += (float)delta * Game.GRAVITY;
            coyoteTime = Math.Max(coyoteTime - delta, 0);
            newVelocity.X *= 0.98f;
        }

        Velocity = newVelocity;
        Vector2 previousPosition = Position;
        MoveAndSlide();
        distanceAccum += previousPosition-Position;
        distanceAccum *= STILLNESS_DECAY;
        Stillness = Math.Max(STILLNESS_CUTOFF - distanceAccum.LengthSquared(), 0) / STILLNESS_CUTOFF;

        if (grabbed is not null) {
            Transform2D grabTransform = grabbed.GlobalTransform;
            grabTransform.Origin = Position + GetLocalMousePosition().Normalized()*10;
            grabbed.GlobalTransform = grabTransform;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("use")) {
            if (grabbed is not null) UseItem();
		} else if (@event.IsActionPressed("grab")) {
            if (grabbed is null) TryGrab();
            else {
                grabbed.Throw(GetLocalMousePosition().Normalized() * 500);
                grabbed = null;
            }
        }
    }

    void UseItem()
    {
        if (grabbed is Aawaga creature) {
            // eat
            if (Hunger >= 1.0) return;
            creature.QueueFree();
            Hunger += 0.5;
            grabbed = null;
        }
    }

    void TryGrab()
    {
        foreach (Node2D node in grabArea.GetOverlappingBodies()) {
            if (node is Aawaga creature) {
                if (creature.Grabbable()) {
                    grabbed = creature;
                    creature.Grab();
                    return;
                }
            }
        }
    }
}

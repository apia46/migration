public partial class Player : CharacterBody2D
{
    const float MOVE_SPEED = 3000.0f;
    const float JUMP_VELOCITY = -350.0f;
    const float WALL_JUMP_IMPULSE = 300.0f;
    const float DOUBLE_JUMP_REDIRECT = 250.0f;

    #nullable disable
    public World World;
    public Camera2D Camera;
    Area2D GrabArea;
    DebugDrawer DebugDrawer;
    #nullable enable

    bool doubleJumpAvailable = false;
    double coyoteTime = 0.0f;

    Aawaga? grabbed = null;

    public double Hunger = 1.0;
    public double Stillness = 0.0;
    const double STILLNESS_CUTOFF = 600000;
    const float STILLNESS_DECAY = 0.998f;

    Vector2 distanceAccum = new();
    Vector2 CameraPosition;
    float CameraSpeed = 10f;

    public Vector2I CurrentChunk = Vector2I.One * -1;

    public override void _Ready()
    {
        GrabArea = GetNode<Area2D>("%GrabArea");
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
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
                if (moveDirection != 0.0f && moveDirection * Velocity.X < DOUBLE_JUMP_REDIRECT) {
                    newVelocity.X = moveDirection * DOUBLE_JUMP_REDIRECT;
                    CameraSpeed = 20f;
                }
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

        CameraPosition += (Position - CameraPosition) * Math.Min(CameraSpeed * (float)delta, 1f);
        CameraSpeed += (10f - CameraSpeed) * Math.Min((float)delta * 10, 1f);
        Vector2 cameraOffset = (World.GetLocalMousePosition() - CameraPosition)/Game.ScreenSize * 100f;
		World.Camera.Position = CameraPosition.Floor() + cameraOffset;

        Vector2I nextChunk = World.PositionToChunk(Position);
        if (CurrentChunk != nextChunk) {
            World.PlayerCrossedChunkBoundary(nextChunk, CurrentChunk);
            CurrentChunk = nextChunk;
            // DebugDrawer.AddText(new Vector2(40, 0), nextChunk.ToString(), Colors.White);
            // DebugDrawer.Evaluate();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("use")) {
            if (grabbed is not null) UseItem();
		} else if (@event.IsActionPressed("grab")) {
            if (grabbed is null) TryGrab();
            else {
                grabbed.Throw(Velocity+GetLocalMousePosition().Normalized() * 500);
                grabbed = null;
            }
        }
    }

    void UseItem()
    {
        if (grabbed is Aawaga aawaga) {
            // eat
            if (Hunger >= 1.0) return;
            CreaturesManager.RemoveCreature(aawaga);
            Hunger += 0.5;
            grabbed = null;
        }
    }

    void TryGrab()
    {
        foreach (Node2D node in GrabArea.GetOverlappingBodies()) {
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

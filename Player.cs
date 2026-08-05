public partial class Player : CharacterBody2D
{
    const float MOVE_SPEED = 3000.0f;
    const float JUMP_VELOCITY = -350.0f;
    const float WALL_JUMP_IMPULSE = 300.0f;
    const float DOUBLE_JUMP_REDIRECT = 250.0f;

    #nullable disable
    public World World;
    Area2D GrabArea;
    DebugDrawer DebugDrawer;
    #nullable enable

    bool DoubleJumpAvailable = false;
    double CoyoteTime = 0.0f;

    Aawaga? grabbed = null;

    public double Hunger = 1.0;
    public double Stillness = 0.0;
    const double STILLNESS_CUTOFF = 600000;
    const float STILLNESS_DECAY = 0.998f;

    Vector2 distanceAccum = new();
    Vector2 CameraPosition;
    float CameraSpeed = 10f;

    public Vector2I CurrentChunk = new(-1,-1);

    public Shelter? Shelter;

    public override void _Ready()
    {
        GrabArea = GetNode<Area2D>("%GrabArea");
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Shelter is not null) {
            Sheltering();
            return;
        }

        Hunger -= delta * 0.01;

        float horizontalControl = IsOnFloor() ? 1.0f : 0.2f;
        float moveDirection = Input.GetAxis("move_left", "move_right");

        Vector2 newVelocity = Velocity;

        if (Input.IsActionJustPressed("jump")) {
            if (IsOnWallOnly()) {
                newVelocity.X = GetWallNormal().X * WALL_JUMP_IMPULSE;
                DoubleJumpAvailable = true;
                newVelocity.Y = JUMP_VELOCITY;
            } else if (IsOnFloor() || CoyoteTime > 0.0) {
                newVelocity.Y = JUMP_VELOCITY;
            } else if (DoubleJumpAvailable) {
                DoubleJumpAvailable = false;
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
            DoubleJumpAvailable = true;
            CoyoteTime = 0.2f;
            newVelocity.X *= 0.8f;
        } else {
            newVelocity.Y += (float)delta * Game.GRAVITY;
            CoyoteTime = Math.Max(CoyoteTime - delta, 0);
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
		World.Camera.Position = CameraPosition.Floor() + GetCameraOffset();

        Vector2I nextChunk = World.PositionToChunk(Position);
        if (CurrentChunk != nextChunk) {
            World.PlayerCrossedChunkBoundary(nextChunk, CurrentChunk);
            CurrentChunk = nextChunk;
            // DebugDrawer.AddText(new Vector2(40, 0), nextChunk.ToString(), Colors.White);
            // DebugDrawer.Evaluate();
        }
    }

    Vector2 GetCameraOffset() => ((World.GetLocalMousePosition() - CameraPosition)/Game.ScreenSize * 100f).Floor();

    void Sheltering()
    {
        CameraPosition += (Shelter!.Position - CameraPosition) * 0.5f;
        World.Camera.Position = CameraPosition.Floor() + GetCameraOffset();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("use")) {
            if (Shelter is not null) ExitShelter(Shelter);
            else if (grabbed is not null) UseItem();
		} else if (@event.IsActionPressed("grab")) {
            if (grabbed is null) {
                foreach (Area2D node in GrabArea.GetOverlappingAreas())
                    if (node is Shelter shelter) {
                        shelter.Enter();
                        return;
                    }
                TryGrab();
            } else {
                grabbed.Throw(GetLocalMousePosition().Normalized() * 800);
                grabbed = null;
            }
        }
    }

    void EnterShelter(Shelter shelter)
    {
        shelter.Enter();
        Shelter = shelter;
        Velocity = Vector2.Zero;
        DoubleJumpAvailable = true;
        Visible = false;
    }

    void ExitShelter(Shelter shelter)
    {
        shelter.Exit();
        Shelter = null;
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

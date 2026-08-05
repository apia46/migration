using System.Text.RegularExpressions;

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

    public double Hunger = 0.5;
    public double Health = 1.0;
    public double Stillness = 0.0;
    const double STILLNESS_CUTOFF = 600000;
    const float STILLNESS_DECAY = 0.998f;

    Vector2 distanceAccum = new();
    Vector2 CameraPosition;
    float CameraSpeed = 10f;

    public Vector2I CurrentChunk = new(-1,-1);

    Shelter? Shelter;

    public enum States {Normal, Sheltering, Resting, Dying}
    public States State = States.Normal;

    const double REST_FOOD_COST = 0.5;
    Vector2 RespawnPosition;
    Shelter? RespawnShelter;

    public override void _Ready()
    {
        GrabArea = GetNode<Area2D>("%GrabArea");
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        RespawnPosition = Position;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Game.Loading) return;
        switch (State) {
            case States.Normal: Normal(delta); break;
            case States.Sheltering: Sheltering(delta); break;
        }
    }

    void Normal(double delta)
    {
        Hunger = Math.Max(0, Hunger + delta * -0.01);
        Health = Math.Min(1, Health+delta * ((Hunger == 0) ? -0.05 : 0.005));
        if (Health <= 0) Die();

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

    public void Hurt(double amount)
    {
        Health -= amount;
        if (Health <= 0) Die();
    }

    void Die()
    {
        grabbed?.Ungrab();
        Game.LivingUI.Visible = false;
        Tween dieTween = GetTree().CreateTween();
        State = States.Dying;
        Visible = false;
        dieTween.TweenProperty(Game.YouDied, "modulate:a", 1, 1);
        dieTween.TweenInterval(1);
        dieTween.TweenCallback(Callable.From(Respawn));
        dieTween.TweenProperty(Game.YouDied, "modulate:a", 0, 1);
    }
    
    void Respawn()
    {
        Position = RespawnPosition;
        Game.LivingUI.Visible = true;
        Visible = true;
        State = States.Normal;
        Health = 1;
        Hunger = 0.5;
        if (RespawnShelter is not null) EnterShelter(RespawnShelter);
    }

    Vector2 GetCameraOffset() => ((World.GetLocalMousePosition() - CameraPosition)/Game.ScreenSize * 100f).Floor();

    void Sheltering(double delta)
    {
        Health = Math.Min(1, Health+delta * 0.03);
        CameraPosition += (Shelter!.Position - CameraPosition) * 0.5f;
        World.Camera.Position = CameraPosition.Floor() + GetCameraOffset();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("use")) {
            if (State == States.Sheltering) ExitShelter();
            else if (grabbed is not null) UseItem();
		} else if (@event.IsActionPressed("grab")) {
            if (grabbed is null) {
                foreach (Area2D node in GrabArea.GetOverlappingAreas())
                    if (node is Shelter shelter) {
                        EnterShelter(shelter);
                        return;
                    }
                TryGrab();
            } else {
                grabbed.Throw(GetLocalMousePosition().Normalized() * 800);
                grabbed = null;
            }
        }
    }

    public void Rest()
    {
        if (State != States.Sheltering) return;
        State = States.Resting;
        Hunger -= REST_FOOD_COST;
        Game.RestButton.Visible = false;
        Tween restTween = GetTree().CreateTween();
        restTween.TweenProperty(Game.BlackScreen, "modulate:a", 1, 1);
        restTween.TweenInterval(1);
        restTween.TweenCallback(Callable.From(Rested));
        restTween.TweenProperty(Game.BlackScreen, "modulate:a", 0, 1);
    }

    void Rested()
    {
        RespawnShelter = Shelter!;
        RespawnPosition = Position;
        Health = 1;
        EnterShelter(Shelter!);
    }

    void EnterShelter(Shelter shelter)
    {
        if (Shelter != shelter) shelter.Enter();
        State = States.Sheltering;
        Shelter = shelter;
        Velocity = Vector2.Zero;
        DoubleJumpAvailable = true;
        Visible = false;
        Game.RestButton.Visible = true;
        Game.RestButton.Disabled = Hunger < REST_FOOD_COST + 0.1;
        Game.RestButton.Text = Hunger < REST_FOOD_COST + 0.1 ? "Not enough food to Rest!" : "Rest";
    }

    void ExitShelter()
    {
        Shelter!.Exit();
        State = States.Normal;
        Shelter = null;
        Visible = true;
        Game.RestButton.Visible = false;
    }

    void UseItem()
    {
        if (grabbed is Aawaga aawaga) {
            // eat
            if (Hunger >= 1.0) return;
            CreaturesManager.RemoveCreature(aawaga);
            Hunger += 0.3;
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

public interface IGrabbable
{
	public bool Grabbable();
	public void Grab();
	public void Ungrab();
	public void Throw(Vector2 force);
}

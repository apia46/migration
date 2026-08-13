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
    Line2D Body;
    Sprite2D Sprite;
    #nullable enable
    Rid WingL;

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

    WingSegment[] WingLSegments = [];
    Vector2[] WingL2PointsBase = [];
    Color[] WingLColors = [];
    Color[] WingPolygonColors = [];
    double WingT = WING_UP+TAU;
    double WingM = 0;
    double BodyR = 0;
    bool WingFlapping = false;
    const float WING_UP = -2;
    const float WING_DOWN = -0.5f;
    readonly Color WingEdgeColor = new("#d4d2b0");
    readonly Color WingWithinColor = new("#ffdaac");
    readonly Color WingBaseColor = new("#fffded");

    bool FacingRight = true;

    public override void _Ready()
    {
        GrabArea = GetNode<Area2D>("%GrabArea");
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        WingL = GetNode<Node2D>("%WingL").GetCanvasItem();
        WingLSegments = new WingSegment[5];
        WingLColors = FillArray(new Color[WingLSegments.Length], WingEdgeColor);
        WingPolygonColors = FillArray(new Color[3], WingBaseColor);
        RespawnPosition = Position;
        WingL2PointsBase = GetNode<Line2D>("%WingL2").Points;
        Body = GetNode<Line2D>("%Body");
        Sprite = GetNode<Sprite2D>("%Sprite");
    }

    public override void _Process(double delta)
    {
        if (WingFlapping) {
            double wingTarget = DoubleJumpAvailable ? WING_UP : WING_DOWN;
            WingT += (wingTarget - 1 - WingT) * 3 * delta;
            if (DoubleJumpAvailable && WingT-WING_UP < 0.1) {
                WingFlapping = false;
                WingT += TAU;
            } else if (WingT<wingTarget) WingT = wingTarget;
        }

        DebugDrawer.AddText(new(20, 20), WingT.ToString(), Colors.White);
        DebugDrawer.AddText(new(20, 40), WingM.ToString(), Colors.White);
        DebugDrawer.Evaluate();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Sprite.FlipH = FacingRight;
        Sprite.Position = new Vector2(FacingRight? 4 : -4, 0);

        WingLSegments[0].Angle = WingM * (0.1 + 0.6 * Math.Sin(WingT));
        WingLSegments[1].Angle = WingM * (0.1 + 0.5 * Math.Sin(WingT+0.375));
        WingLSegments[2].Angle = WingM * (0.1 + 0.3 * Math.Sin(WingT+0.75));
        WingLSegments[3].Angle = WingM * (0.1 + 0.2 * Math.Sin(WingT+1.125));
        WingLSegments[4].Angle = WingM * (0.1 + 0.1 * Math.Sin(WingT+1.5));

        Vector2[] wingLPoints = new Vector2[WingLSegments.Length];
        Vector2[] wingL2Points = new Vector2[WingLSegments.Length];
        Vector2[] bodyPoints = new Vector2[WingLSegments.Length+1];
        Vector2 pointAccum = Vector2.Zero;
        double angleAccum = 0;
        for (int i = 0; i < WingLSegments.Length; i++) {
            WingSegment segment = WingLSegments[i];
            angleAccum += segment.Angle;
            pointAccum += new Vector2(8,0).Rotated((float)angleAccum);
            wingLPoints[i] = new((FacingRight ? -pointAccum.X : pointAccum.X) * 1.5f, pointAccum.Y);
            Vector2 l2BasePoint = WingL2PointsBase[i];
            wingL2Points[i] = l2BasePoint.Rotated((float)BodyR) * new Vector2(FacingRight ? -1 : 1, 1);
            bodyPoints[i] = wingL2Points[i];
        }
        bodyPoints[WingLSegments.Length] = bodyPoints[WingLSegments.Length-1] + new Vector2(FacingRight ? -8 : 8, -5);
        Body.Points = bodyPoints;

        RenderingServer.CanvasItemClear(WingL);
        for (int i = 1; i < WingLSegments.Length; i++) {
            RenderingServer.CanvasItemAddPolygon(WingL, [wingLPoints[i-1], wingLPoints[i], wingL2Points[i-1]], WingPolygonColors);
            RenderingServer.CanvasItemAddPolygon(WingL, [wingL2Points[i-1], wingLPoints[i], wingL2Points[i]], WingPolygonColors);
        }
        for (int i = 0; i < WingLSegments.Length; i++) {
            bool isEnd = i == 0 || i == WingLSegments.Length - 1;
            RenderingServer.CanvasItemAddLine(WingL, wingLPoints[i], wingL2Points[i],
                isEnd?WingEdgeColor:WingWithinColor, isEnd?2:1);
        }
        RenderingServer.CanvasItemAddPolyline(WingL, wingLPoints, WingLColors, 2);
        RenderingServer.CanvasItemAddPolyline(WingL, wingL2Points, WingLColors, 2);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Game.State != Game.States.Play) return;
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
                WingT = WING_UP+TAU;
                WingFlapping = true;
            } else if (DoubleJumpAvailable) {
                DoubleJumpAvailable = false;
                newVelocity.Y = JUMP_VELOCITY;
                if (WingT < 0) WingT = 3;
                WingFlapping = true;
                if (moveDirection != 0.0f && moveDirection * Velocity.X < DOUBLE_JUMP_REDIRECT) {
                    newVelocity.X = moveDirection * DOUBLE_JUMP_REDIRECT;
                    CameraSpeed = 20f;
                }
            }
        }

        if (moveDirection != 0.0f) {
            WingM = Mathf.MoveToward(WingM, 1, (float)delta * 5);
            newVelocity.X += moveDirection * MOVE_SPEED * (float)delta * horizontalControl;
            FacingRight = moveDirection > 0;
        } else {
            WingM = Mathf.MoveToward(WingM, 0, (float)delta * 5);
            newVelocity.X = Mathf.MoveToward(newVelocity.X, 0.0f, MOVE_SPEED * (float)delta * horizontalControl);
        }

        if (IsOnFloor()) {
            DoubleJumpAvailable = true;
            CoyoteTime = 0.2f;
            newVelocity.X *= 0.8f;
            BodyR = 0;
        } else {
            newVelocity.Y += (float)delta * Game.GRAVITY;
            CoyoteTime = Math.Max(CoyoteTime - delta, 0);
            newVelocity.X *= 0.98f;
            BodyR = 0.4;
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
        if (Game.State != Game.States.Play) {
            if (Game.State == Game.States.Paused && @event.IsActionPressed("pause")) Unpause();
            return;
        }
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
        } else if (Game.CanPause && @event.IsActionPressed("pause")) Pause();
    }

    void Pause()
    {
        Game.Menu.Visible = true;
        Game.State = Game.States.Paused;
    }

    void Unpause()
    {
        Game.Menu.Visible = false;
        Game.State = Game.States.Play;
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
        Game.ShelterLabel.Visible = true;
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
        Game.ShelterLabel.Visible = false;
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

struct WingSegment()
{
    public double Angle = 0;
}

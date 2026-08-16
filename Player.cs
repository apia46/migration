public partial class Player : CharacterBody2D
{
    const float MOVE_SPEED = 3000.0f;
    const float LEG_SPEED = 30.0f;
    const float JUMP_VELOCITY = -350.0f;
    const float WALL_JUMP_IMPULSE = 300.0f;
    const float DOUBLE_JUMP_REDIRECT = 250.0f;
    
    const float LEG_LENGTH = 12;

    #nullable disable
    public World World;
    Area2D GrabArea;
    DebugDrawer DebugDrawer;
    Line2D Body;
    Sprite2D Sprite;
    Skeleton2D Skeleton;
    Node2D LegTargetsNode;
	Node2D[] LegTargets;
    SkeletonModification2DCcdik[] LegSkeletonModifications;
    #nullable enable
    readonly bool[] LegMoving = new bool[6];

    Rid WingL;
    Rid LegsL;
    Rid LegsR;

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
    Vector2[] BodyPointsBase = [];
    Color[] WingLColors = [];
    Color[] WingPolygonColors = [];
    Color[] WingPolygonColors2 = [];
    double WingT = WING_UP+TAU;
    double WingM = 0;
    double BodyR = 0;
    bool WingFlapping = false;
    const float WING_UP = -2;
    const float WING_DOWN = -0.5f;
    readonly Color WingEdgeColor = new("#d4d2b0");
    readonly Color WingWithinColor = new("#ffdaac");
    readonly Color WingBaseColor = new("#fffded");
    readonly Color LegColor = new("#fffded");

    bool FacingRight = true;

    public override void _Ready()
    {
        GrabArea = GetNode<Area2D>("%GrabArea");
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        WingL = GetNode<Node2D>("%WingL").GetCanvasItem();
        LegsL = GetNode<Node2D>("%LegsL").GetCanvasItem();
        LegsR = GetNode<Node2D>("%LegsR").GetCanvasItem();
        WingLSegments = new WingSegment[5];
        WingLColors = FillArray(new Color[WingLSegments.Length], WingEdgeColor);
        WingPolygonColors = FillArray(new Color[3], WingBaseColor);
        WingPolygonColors2 = FillArray(new Color[3], Colors.Green);
        RespawnPosition = Position;
        WingL2PointsBase = GetNode<Line2D>("%WingL2").Points;
        Body = GetNode<Line2D>("%Body");
        BodyPointsBase = Body.Points;
        Sprite = GetNode<Sprite2D>("%Sprite");
        Skeleton = GetNode<Skeleton2D>("%Skeleton");
        LegTargetsNode = GetNode<Node2D>("%LegTargets");
        LegTargets = [..LegTargetsNode.GetChildren().Select(c=>(Node2D)c)];
        LegSkeletonModifications = new SkeletonModification2DCcdik[6];
        SkeletonModificationStack2D stack = Skeleton.GetModificationStack();
        for (int i = 0; i < 6; i++) LegSkeletonModifications[i] = (SkeletonModification2DCcdik)stack.GetModification(i);
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

        // DebugDrawer.AddText(new(20, 20), WingT.ToString(), Colors.White);
        // DebugDrawer.AddText(new(20, 40), WingM.ToString(), Colors.White);
        // DebugDrawer.Evaluate();
        QueueRedraw();
    }

    public override void _Draw()
    {
        RenderingServer.CanvasItemClear(WingL);
        RenderingServer.CanvasItemClear(LegsL);
        RenderingServer.CanvasItemClear(LegsR);

        Sprite.FlipH = FacingRight;
        Sprite.Position = new Vector2(-9, 0).Rotated((float)BodyR) * new Vector2(FacingRight?-1:1, 1);
        Sprite.Rotation = FacingRight?-(float)BodyR:(float)BodyR;

        WingLSegments[0].Angle = WingM * (0.2 + 0.6 * Math.Sin(WingT));
        WingLSegments[1].Angle = WingM * (0.2 + 0.5 * Math.Sin(WingT+0.375));
        WingLSegments[2].Angle = WingM * (0.2 + 0.3 * Math.Sin(WingT+0.75));
        WingLSegments[3].Angle = WingM * (0.2 + 0.2 * Math.Sin(WingT+1.125));
        WingLSegments[4].Angle = WingM * (0.2 + 0.1 * Math.Sin(WingT+1.5));

        Vector2[] wingLPoints = new Vector2[WingLSegments.Length];
        Vector2[] wingL2Points = new Vector2[WingLSegments.Length];
        Vector2 pointAccum = new(-4,-8);
        double angleAccum = 0;
        for (int i = 0; i < WingLSegments.Length; i++) {
            WingSegment segment = WingLSegments[i];
            angleAccum += segment.Angle;
            pointAccum += new Vector2(8,0).Rotated((float)angleAccum);
            wingLPoints[i] = new((FacingRight ? -pointAccum.X : pointAccum.X) * 1.2f, pointAccum.Y);
            wingL2Points[i] = WingL2PointsBase[i].Rotated((float)BodyR) * new Vector2(FacingRight ? -1 : 1, 1);
        }
        Vector2[] bodyPoints = new Vector2[BodyPointsBase.Length];
        for (int i = 0; i < BodyPointsBase.Length; i++)
            bodyPoints[i] = BodyPointsBase[i].Rotated((float)BodyR) * new Vector2(FacingRight ? -1 : 1, 1);
        Body.Points = bodyPoints;

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

        Vector2 LocalPosition(Node2D node) => node.GlobalPosition - GlobalPosition;
		void DrawLeg(bool rightSide, Bone2D leg1, Bone2D leg2, Node2D leg3, Color color)
		{
			RenderingServer.CanvasItemAddLine(rightSide ? LegsL : LegsR, LocalPosition(leg1), LocalPosition(leg2), color, 2);
			RenderingServer.CanvasItemAddLine(rightSide ? LegsL : LegsR, LocalPosition(leg2), LocalPosition(leg3), color, 2);
		}
		for (int i = 0; i < 6; i++)
			DrawLeg(i < 3, Skeleton.GetBone(i*2+1), Skeleton.GetBone(i*2+2), (Node2D)Skeleton.GetBone(i*2+2).GetChild(0), LegColor);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Game.State != Game.States.Play) return;
        switch (State) {
            case States.Normal: Normal(delta); break;
            case States.Sheltering: Sheltering(delta); break;
        }
        LegTargetsNode.Position = -Position;
    }

    void Normal(double delta)
    {
        bool previousFacingRight = FacingRight;

        float wallDirection = IsOnWallOnly() ? Math.Sign(GetWallNormal().X) : 0;

        #pragma warning disable CS0162
        if (!Game.DEBUG_NO_SURVIVAL) {
            Hunger = Math.Max(0, Hunger + delta * -0.01);
            Health = Math.Min(1, Health+delta * ((Hunger == 0) ? -0.05 : 0.005));
            if (Health <= 0) Die();
        }
        #pragma warning restore CS0162

        float horizontalControl = IsOnFloor() ? 1.0f : 0.2f;
        float moveDirection = Input.GetAxis("move_left", "move_right");

        Vector2 newVelocity = Velocity;

        if (Input.IsActionJustPressed("jump")) {
            if (wallDirection != 0f) {
                newVelocity.X = wallDirection * WALL_JUMP_IMPULSE;
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

        // if (previousFacingRight != FacingRight)
        // for (int i = 0; i < 6; i++) {
        //     LegSkeletonModifications[i].SetCcdikJointConstraintAngleInvert(1, !FacingRight);
        //     LegSkeletonModifications[i].SetCcdikJointConstraintAngleMin(1, FacingRight?180:90);
        //     LegSkeletonModifications[i].SetCcdikJointConstraintAngleMax(1, FacingRight?90:360);
        //     // GD.Print(i, ((SkeletonModification2DCcdik)Skeleton.GetModificationStack().GetModification(i)).GetCcdikJointConstraintAngleInvert(1));
        // }

        if (IsOnFloor()) {
            DoubleJumpAvailable = true;
            CoyoteTime = 0.2f;
            newVelocity.X *= 0.8f;
            BodyR = 0;
        } else {
            newVelocity.Y += (float)delta * Game.GRAVITY;
            CoyoteTime = Math.Max(CoyoteTime - delta, 0);
            newVelocity.X *= 0.98f;
            if (wallDirection != 0f) BodyR = PI/2;
            else BodyR = 0.5;
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

        for (int i = 0; i < 6; i++)
        {
            Vector2 restPosition = new Vector2((i % 3 * 8) - 8 + (FacingRight ? 1 : -1) * (6+(float)(i/3)*3), 8) + Position;
            Node2D target = LegTargets[i];
            DebugDrawer.AddCircle(restPosition - Position, Color.FromHsv(i/6f, LegMoving[i] ? 0.5f : 1f, 0.5f));
            DebugDrawer.AddCircle(target.Position - Position, Color.FromHsv(i/6f, LegMoving[i] ? 0.5f : 1f, 0.5f));
            if (LegMoving[i]) {
                target.Position = target.Position.MoveToward(restPosition, Math.Abs(Velocity.X) + LEG_SPEED * (float)delta);
				if (target.Position.DistanceSquaredTo(restPosition) < 50) {
					target.Position = restPosition;
					LegMoving[i] = false;
				}
            } else {
                int legAfter = i switch {2 => 5, 5 => 2, _ => i+1};
                int legBefore = i switch {0 => 3, 3 => 0, _ => i-1};
                if (!LegMoving[legAfter] && !LegMoving[legBefore]
                    && target.Position.DistanceSquaredTo(restPosition) > LEG_LENGTH*LEG_LENGTH * 1.3) LegMoving[i] = true;
            }
        }
        DebugDrawer.Evaluate();
    }

    public void ResetTargets()
	{
		LegTargetsNode.Position = -Position;
		for (int i = 0; i < 6; i++)
		{
			Node2D target = LegTargets[i];
			target.Position = new Vector2(i % 3 * 10 - 10 + (FacingRight ? 5 : -5), 8) + Position;
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
            else if (State == States.Normal && grabbed is not null) UseItem();
		} else if (State == States.Normal && @event.IsActionPressed("grab")) {
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

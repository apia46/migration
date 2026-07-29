[GlobalClass]
public partial class Spider : CharacterBody2D, ICreature<Spider>
{

	#nullable disable
	public int Id { get; set; }
    public World World { get; set; }
	#nullable enable

	const float LEG_LENGTH = 34;
	const float SPEED = 240;
	readonly Color LEG_COLOR = new("#004928");

    public bool Grabbed { get; set; }
    public static PackedScene Scene { get; set; } = GD.Load<PackedScene>("creatures/spider.tscn");
    public static Dictionary<int, Spider> Creatures { get; set; } = [];
    public static int IdIterator { get; set; }

	#nullable disable
	DebugDrawer DebugDrawer;
	NavigationAgent2D NavigationAgent;
	Skeleton2D Skeleton;
	Node2D Visuals;
	Node2D TargetsNode;
	Node2D[] Targets;
	bool[] LegMoving = new bool[6];
	#nullable enable

	Rid MainDraw;

	public enum AIState {Idle, Wander, Chase, Evade};
	AIState State = AIState.Idle;
	double BoredomTimer;
	double WanderTimer;
	Vector2 WanderDirection;
	Vector2 ChaseTarget;
    public override void _Ready()
    {
		MainDraw = GetCanvasItem();
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        NavigationAgent = GetNode<NavigationAgent2D>("%NavigationAgent");
        Skeleton = GetNode<Skeleton2D>("%Skeleton");
        Visuals = GetNode<Node2D>("%Visuals");
		TargetsNode = GetNode<Node2D>("%Targets");
		Targets = [..TargetsNode.GetChildren().Select(c=>(Node2D)c)];
		SetState(AIState.Idle);
    }

	void SetState(AIState to)
	{
		State = to;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer = Game.RNG.Range(5.0, 20.0);
			} break;
			case AIState.Wander: {
				WanderDirection = new Vector2(1, 0).Rotated(Game.RNG.Range(0, TAU));
				WanderTimer = Game.RNG.Range(2.0, 4.0);
			} break;
			case AIState.Chase: {
				BoredomTimer = Game.RNG.Range(15.0, 20.0);
				UpdatePathfinding();
			} break;
			case AIState.Evade: {} break;
		}
	}

	float Chaseness() => 100000/(World.Player.Position - Position).LengthSquared();

	void UpdatePathfinding()
	{
		switch (State) {
			case AIState.Chase: {
				if (NavigationAgent.TargetPosition.DistanceSquaredTo(World.Player.GlobalPosition) > 1000) {
					NavigationAgent.TargetPosition = World.Player.GlobalPosition;
					ChaseTarget = NavigationAgent.GetNextPathPosition();
				} else if (NavigationAgent.IsNavigationFinished()) SetState(AIState.Idle);
				else if (GlobalPosition.DistanceSquaredTo(ChaseTarget) < 400) ChaseTarget = NavigationAgent.GetNextPathPosition();
			} break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 intendedDirection = Vector2.Zero;
		float speed = 0;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				else if (Chaseness() > 8) SetState(AIState.Chase);
			} break;
			case AIState.Wander: {
				WanderTimer -= delta;
				intendedDirection = WanderDirection;
				speed = 30;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				else if (Chaseness() > 12) SetState(AIState.Chase);
			} break;
			case AIState.Chase: {
				BoredomTimer -= delta;
				UpdatePathfinding();
				intendedDirection = GlobalPosition.DirectionTo(ChaseTarget);
				speed = SPEED;
				if (BoredomTimer <= 0) SetState(AIState.Idle);
			} break;
			case AIState.Evade: {} break;
		}
		Velocity = intendedDirection * speed;
		Visuals.Rotation = Velocity.Angle();
		// DebugDrawer.AddText(new Vector2(30,30), Chaseness().ToString(), Colors.White);
		// DebugDrawer.AddText(new Vector2(30,50), BoredomTimer.ToString(), Colors.White);
		MoveAndSlide();
		TargetsNode.Position = -Position;
		for (int i = 0; i < 6; i++)
		{
			Vector2 restPosition = Vector2.Right.Rotated(Visuals.Rotation+TAU*(i+0.5f)/6) * 30 + Velocity * 0.1f;
			restPosition = restPosition.LimitLength(LEG_LENGTH) + Position;
			Node2D target = Targets[i];
			// DebugDrawer.AddCircle(target.Position - Position, Color.FromHsv(i/6f, 1, 0.5f));
			// DebugDrawer.AddCircle(restPosition - Position, Color.FromHsv(i/6f, 1, 1));
			if (LegMoving[i]) {
				target.Position = target.Position.MoveToward(restPosition, SPEED * 3f * (float)delta);
				if (target.Position.DistanceSquaredTo(restPosition) < 50) {
					target.Position = restPosition;
					LegMoving[i] = false;
				}
			} else if (target.Position.DistanceSquaredTo(restPosition) > LEG_LENGTH*LEG_LENGTH * 0.7 && !LegMoving[(i+5) % 6] && !LegMoving[(i+1) % 6]) LegMoving[i] = true;
		}
		DebugDrawer.Evaluate();
	}

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        RenderingServer.CanvasItemClear(MainDraw);
		
		Vector2 LocalPosition(Node2D node) => node.GlobalPosition - GlobalPosition;
		void DrawLeg(Bone2D leg1, Bone2D leg2, Node2D leg3, Color color)
		{
			RenderingServer.CanvasItemAddLine(MainDraw, LocalPosition(leg1), LocalPosition(leg2), color, 3);
			RenderingServer.CanvasItemAddLine(MainDraw, LocalPosition(leg2), LocalPosition(leg3), color, 3);
		}
		for (int i = 0; i < 6; i++)
			DrawLeg(Skeleton.GetBone(i*2+1), Skeleton.GetBone(i*2+2), (Node2D)Skeleton.GetBone(i*2+2).GetChild(0), LEG_COLOR);
    }
}

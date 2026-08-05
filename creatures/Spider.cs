[GlobalClass]
public partial class Spider : CharacterBody2D, ICreature<Spider>
{
	public int Id { get; set; }

	const float LEG_LENGTH = 34;
	float Speed;
	readonly Color LEG_COLOR = new("#004928");

    public bool Grabbed { get; set; }
    public static PackedScene Scene { get; set; } = GD.Load<PackedScene>("res://creatures/spider.tscn");
    public static Dictionary<int, Spider> Creatures { get; set; } = [];
    public static int IdIterator { get; set; }
	public float CollisionRadius { get; set; } = 16;

	#nullable disable
	DebugDrawer DebugDrawer;
	NavigationAgent2D NavigationAgent;
	Skeleton2D Skeleton;
	Node2D Visuals;
	Node2D TargetsNode;
	Node2D[] Targets;
	#nullable enable
	readonly bool[] LegMoving = new bool[6];
	
	Rid MainDraw;

	public enum AIState {Idle, Wander, Chase, Evade, Bonking};
	AIState State = AIState.Idle;
	double BoredomTimer;
	Vector2 PathfindingTarget;
	Vector2 Home;
	double BonkTimer;
	double ChompTimer;

    public override void _Ready()
    {
		Home = Position;
		Speed = Game.RNG.Range(180, 220);
		MainDraw = GetCanvasItem();
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        NavigationAgent = GetNode<NavigationAgent2D>("%NavigationAgent");
        Skeleton = GetNode<Skeleton2D>("%Skeleton");
        Visuals = GetNode<Node2D>("%Visuals");
		TargetsNode = GetNode<Node2D>("%Targets");
		Targets = [..TargetsNode.GetChildren().Select(c=>(Node2D)c)];
		SetState(AIState.Idle);
		ResetTargets();
    }

	void SetState(AIState to)
	{
		State = to;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer = Game.RNG.Range(5.0, 20.0);
				ResetTargets();
			} break;
			case AIState.Wander: {
				Vector2 wanderLocation;
				do {
					wanderLocation = new Vector2(Game.RNG.Range(-800,800), Game.RNG.Range(-800,800)) + GlobalPosition;
				} while(!World.InteriorTile((Vector2I)(wanderLocation/World.CONVERTED_TILE_SIZE)));
				NavigationAgent.TargetPosition = wanderLocation;
				UpdatePathfinding(true);
			} break;
			case AIState.Chase: {
				BoredomTimer = 0;
				UpdatePathfinding();
			} break;
			case AIState.Evade: {} break;
			case AIState.Bonking: {
				BonkTimer = 0.4;
			} break;
		}
	}

	bool ChasePlayer() {
		if (World.Player.State != Player.States.Normal) return false;
		if (Home.DistanceSquaredTo(Position) > (State == AIState.Chase ? 1e9 : 1e7)) return false;
		// if (State == AIState.Chase) return Math.Min(10, 1e6f/dist)+10+Math.Max(-5,dist/5000-dist*dist/2e9f)-(float)BoredomTimer;
		return World.Player.Position.DistanceSquaredTo(Position) < 40000;
	}

	void UpdatePathfinding(bool reset=false)
	{
		switch (State) {
			case AIState.Wander: {
				if (NavigationAgent.IsNavigationFinished()) SetState(AIState.Idle);
				else if (reset || GlobalPosition.DistanceSquaredTo(PathfindingTarget) < 400) PathfindingTarget = NavigationAgent.GetNextPathPosition();
			} break;
			case AIState.Chase: {
				if (NavigationAgent.TargetPosition.DistanceSquaredTo(World.Player.GlobalPosition) > 1000) {
					NavigationAgent.TargetPosition = World.Player.GlobalPosition;
					PathfindingTarget = NavigationAgent.GetNextPathPosition();
				} else if (NavigationAgent.IsNavigationFinished()) {
					NavigationAgent.TargetPosition = World.Player.GlobalPosition;
				} else if (GlobalPosition.DistanceSquaredTo(PathfindingTarget) < 400) PathfindingTarget = NavigationAgent.GetNextPathPosition();
			} break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Game.Loading) return;
		Vector2 intendedDirection = Vector2.Zero;
		float speed = 0;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				else if (ChasePlayer()) SetState(AIState.Chase);
			} break;
			case AIState.Wander: {
				UpdatePathfinding();
				intendedDirection = GlobalPosition.DirectionTo(PathfindingTarget);
				speed = 30;
				if (ChasePlayer()) SetState(AIState.Chase);
			} break;
			case AIState.Chase: {
				UpdatePathfinding();
				intendedDirection = GlobalPosition.DirectionTo(PathfindingTarget);
				speed = Speed;
				if (!ChasePlayer()) SetState(AIState.Idle);
			} break;
			case AIState.Evade: {} break;
			case AIState.Bonking: {
				Vector2 velocity = Velocity;
				velocity.Y += Game.GRAVITY * (float)delta;
				velocity *= 0.9f;
				Velocity = velocity;
				Vector2 PreviousPosition = Position;
				MoveAndSlide();
				foreach (Node2D target in Targets) target.Position += Position-PreviousPosition;
				BonkTimer -= delta;
				if (BonkTimer <= 0) SetState(AIState.Idle);
			} return;
		}
		Velocity = intendedDirection * speed;
		if (Velocity != Vector2.Zero) Visuals.Rotation = Velocity.Angle();
		// DebugDrawer.AddText(new Vector2(30,50), BoredomTimer.ToString(), Colors.White);
		ChompTimer -= delta;
		if (World.Player.State == Player.States.Normal && ChompTimer <= 0 && Position.DistanceSquaredTo(World.Player.Position) < 800) {
			ChompTimer = 0.5;
			World.Player.Hurt(0.5);
		}
		MoveAndSlide();
		TargetsNode.Position = -Position;
		for (int i = 0; i < 6; i++)
		{
			Vector2 restPosition = TargetRestPosition(i, intendedDirection);
			restPosition = restPosition.LimitLength(LEG_LENGTH) + Position;
			Node2D target = Targets[i];
			// DebugDrawer.AddCircle(target.Position - Position, Color.FromHsv(i/6f, LegMoving[i] ? 0.5f : 1f, 0.5f));
			// DebugDrawer.AddCircle(restPosition - Position, Color.FromHsv(i/6f, 1, 1));
			if (LegMoving[i]) {
				target.Position = target.Position.MoveToward(restPosition, speed * 3f * (float)delta);
				if (target.Position.DistanceSquaredTo(restPosition) < 50) {
					target.Position = restPosition;
					LegMoving[i] = false;
				}
			} else if (target.Position.DistanceSquaredTo(restPosition) > LEG_LENGTH*LEG_LENGTH * 0.7 && !LegMoving[(i+5) % 6] && !LegMoving[(i+1) % 6]) LegMoving[i] = true;
		}
		DebugDrawer.Evaluate();
		CreaturesManager.CreatureMoved(this);
	}

	public void Bonk(Vector2 direction)
	{
		Velocity += direction * 1.25f;
		SetState(AIState.Bonking);
	}

	Vector2 TargetRestPosition(int target, Vector2 intendedDirection) => Vector2.Right.Rotated(Visuals.Rotation+TAU*(target+0.5f)/6) * 30 + intendedDirection * 15f;

	void ResetTargets()
	{
		TargetsNode.Position = -Position;
		for (int i = 0; i < 6; i++)
		{
			Node2D target = Targets[i];
			target.Position = TargetRestPosition(i, Vector2.Zero).LimitLength(LEG_LENGTH) + Position;
		}
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

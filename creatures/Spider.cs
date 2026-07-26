[GlobalClass]
public partial class Spider : CharacterBody2D, ICreature<Spider>
{

	#nullable disable
	public int Id { get; set; }
    public World World { get; set; }
	#nullable enable

    public bool Grabbed { get; set; }
    public static PackedScene Scene { get; set; } = GD.Load<PackedScene>("creatures/spider.tscn");
    public static Dictionary<int, Spider> Creatures { get; set; } = [];
    public static int IdIterator { get; set; }

	#nullable disable
	DebugDrawer DebugDrawer;
	NavigationAgent2D NavigationAgent;
	#nullable enable

	public enum AIState {Idle, Wander, Chase, Evade};
	AIState State = AIState.Idle;
	double BoredomTimer;
	double WanderTimer;
	Vector2 WanderDirection;

    public override void _Ready()
    {
        DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
        NavigationAgent = GetNode<NavigationAgent2D>("%NavigationAgent");
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

			} break;
			case AIState.Evade: {} break;
		}
	}

	float Chaseness() => 100000/(World.Player.Position - Position).LengthSquared();

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
				NavigationAgent.TargetPosition = World.Player.GlobalPosition;
				if (NavigationAgent.IsNavigationFinished()) SetState(AIState.Idle);
				else {
					intendedDirection = GlobalPosition.DirectionTo(NavigationAgent.GetNextPathPosition());
					speed = 100;
				}
				if (BoredomTimer <= 0) SetState(AIState.Idle);
			} break;
			case AIState.Evade: {} break;
		}
		Velocity = intendedDirection * speed;
		Rotation = Velocity.Angle();
		DebugDrawer.AddText(new Vector2(30,30), Chaseness().ToString(), Colors.White);
		DebugDrawer.AddText(new Vector2(30,50), BoredomTimer.ToString(), Colors.White);
		DebugDrawer.Evaluate();
		MoveAndSlide();
	}
}

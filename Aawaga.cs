using System.Text.RegularExpressions;

[GlobalClass]
public partial class Aawaga : CharacterBody2D, IGrabbable
{
	static readonly GameRandom RNG = new();

	#nullable disable
	public CharacterBody2D Player;
	Node2D Visuals;
	#nullable enable

	float Size;

	bool grabbed;
    public bool Grabbed {get=>grabbed;set=>grabbed=value;}
	
	public enum AIState {Idle, Wander, Evade, Grabbed, Thrown};
	AIState State = AIState.Idle;
	Vector2 WanderDirection;
	double BoredomTimer;
	double WanderTimer;
	double JumpTimer;
	double ThrownTimer;

    public override void _Ready()
	{
		Size = RNG.Range(0.8f, 1.2f);
		Visuals = GetNode<Node2D>("%Visuals");
		Visuals.Scale *= Size;
		foreach (CollisionShape2D shape in CollisionShapes()) {
			shape.Scale *= Size;
			shape.Position *= Size;
		}
		SetState(AIState.Idle);
	}

	void SetState(AIState to)
	{
		State = to;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer = RNG.Range(5.0, 20.0);
			} break;
			case AIState.Wander: {
				WanderDirection = new Vector2(RNG.FlipCoin() ? -1f : 1f, 1f);
				WanderTimer = RNG.Range(2.0, 4.0);
			} break;
			case AIState.Evade: {
				
			} break;
			case AIState.Grabbed: {
				Velocity = Vector2.Zero;
			} break;
			case AIState.Thrown: {
				ThrownTimer = 0.1;
			} break;
		}
	}

    public override void _Process(double delta)
	{
		
	}

	Vector2 GetSurfaceDirection() {
		if (IsOnWall()) return new(-Math.Sign(GetWallNormal().X), 0);
		else if (IsOnFloor()) return new(0, 1);
		else if (IsOnCeiling()) return new(0, -1);
		else return Vector2.Zero;
	}

    public override void _PhysicsProcess(double delta)
    {
		Vector2 newVelocity = Velocity;

		Vector2 intendedDirection = Vector2.Zero;
		Vector2 surfaceDirection = GetSurfaceDirection();
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				if (Danger() > 40) SetState(AIState.Evade);
				Rotation -= Rotation % (float)(Math.Tau/3);
			} break;
			case AIState.Wander: {
				intendedDirection = WanderDirection;

				WanderTimer -= delta;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				if (Danger() > 80) SetState(AIState.Evade);
			} break;
			case AIState.Evade: {
				intendedDirection = Position - Player.Position;

				if (Danger() < 5 && RNG.FlipCoin() && IsOnFloorOnly()) SetState(AIState.Idle);
			} break;
			case AIState.Grabbed: return;
			case AIState.Thrown: {
				ThrownTimer -= delta;
				if (ThrownTimer <= 0) SetState(AIState.Idle);
			} break;
		}
		JumpTimer -= delta;
		float moveDirection = Math.Sign(intendedDirection.AngleTo(surfaceDirection));
		if (IsOnFloor() && State != AIState.Thrown) {
			if (JumpTimer < 0) {
				JumpTimer = RNG.Range(6.0, 12.0);
				newVelocity += new Vector2(0f, RNG.Range(-100f, -300f));
			}
			Rotation += moveDirection * 12 * (float)delta;
			newVelocity.X = moveDirection * 60;
		}
		if (IsOnWall() && State != AIState.Thrown) {
			float wallDirection = surfaceDirection.X;
			newVelocity.Y = -wallDirection * moveDirection * 60;
			newVelocity.X = wallDirection * 30;
			Rotation += moveDirection * 12 * (float)delta;
		} else {
			newVelocity.Y += (float)delta * Game.GRAVITY;
		}
		Velocity = newVelocity;
		MoveAndSlide();
    }

	IEnumerable<CollisionShape2D> CollisionShapes() {
		foreach (Node node in GetChildren()) {
			if (node is CollisionShape2D shape) yield return shape;
		}
	}

	float Danger() {
		return 100000/(Player.Position - Position).LengthSquared();
	}

	public bool Grabbable() { return true; }

	public void Grab()
	{
		foreach (CollisionShape2D shape in CollisionShapes()) shape.Disabled = true;
		SetState(AIState.Grabbed);
	}
	public void Ungrab()
	{
		foreach (CollisionShape2D shape in CollisionShapes()) shape.Disabled = false;
		SetState(AIState.Idle);
	}
	public void Throw(Vector2 force)
	{
		Velocity += force;
		SetState(AIState.Thrown);
	}
}

public interface IGrabbable
{
	public bool Grabbable();
	public void Grab();
	public void Ungrab();
	public void Throw(Vector2 force);
}

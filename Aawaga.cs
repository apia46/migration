using System.Text.RegularExpressions;

[GlobalClass]
public partial class Aawaga : CharacterBody2D, IGrabbable
{
	static readonly GameRandom RNG = new();

	#nullable disable
	public CharacterBody2D Player;
	CollisionPolygon2D Collision;
	Node2D Visuals;
	#nullable enable

	float Size;

	bool grabbed;
    public bool Grabbed {get=>grabbed;set=>grabbed=value;}
	
	public enum AIState {Idle, Wander, Evade, Grabbed};
	AIState State = AIState.Idle;
	float WanderDirection;
	double BoredomTimer;
	double WanderTimer;
	double JumpTimer;

    public override void _Ready()
	{
		Size = RNG.Range(1.0f, 1.0f);
		Collision = GetNode<CollisionPolygon2D>("%Collision");
		Visuals = GetNode<Node2D>("%Visuals");
		Visuals.Scale *= Size;
		Collision.Scale *= Size;
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
				WanderDirection = RNG.FlipCoin() ? 1.0f : -1.0f;
				WanderTimer = RNG.Range(2.0, 4.0);
			} break;
			case AIState.Evade: {
				
			} break;
			case AIState.Grabbed: break;
		}
	}

    public override void _Process(double delta)
	{
		
	}

    public override void _PhysicsProcess(double delta)
    {
		Vector2 newVelocity = Velocity;

		float moveDirection = 0f;
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				if (Danger() > 50) SetState(AIState.Evade);
				Rotation -= Rotation % (float)(Math.Tau/3);
			} break;
			case AIState.Wander: {
				moveDirection = WanderDirection;

				WanderTimer -= delta;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				if (Danger() > 100) SetState(AIState.Evade);
			} break;
			case AIState.Evade: {
				moveDirection = Math.Sign(Position.X - Player.Position.X);

				if (Danger() < 5 && RNG.FlipCoin() && IsOnFloorOnly()) SetState(AIState.Idle);
			} break;
			case AIState.Grabbed: break;
		}
		JumpTimer -= delta;
		if (IsOnFloor()) {
			if (JumpTimer < 0) {
				JumpTimer = RNG.Range(6.0, 12.0);
				newVelocity += new Vector2(0f, RNG.Range(-100f, -300f));
			}
			Rotation += moveDirection * 12 * (float)delta;
			newVelocity.X = moveDirection * 60;
		}
		if (IsOnWall()) {
			float wallDirection = GetWallNormal().X > 0 ? 1 : -1;
			newVelocity.Y = wallDirection * moveDirection * 60;
			newVelocity.X = wallDirection * -30;
			Rotation += moveDirection * 12 * (float)delta;
		} else {
			newVelocity.Y += (float)delta * Game.GRAVITY;
		}
		Velocity = newVelocity;
		MoveAndSlide();
    }

	float Danger() {
		return 100000/(Player.Position - Position).LengthSquared();
	}

	public bool Grabbable() { return true; }

	public void Grab()
	{
		Collision.Disabled = true;
		State = AIState.Grabbed;
	}
	public void Ungrab()
	{
		Collision.Disabled = false;
		State = AIState.Idle;
	}
	public void ApplyForce(Vector2 force)
	{
		Velocity += force;
	}
}

public interface IGrabbable
{
	public bool Grabbable();
	public void Grab();
	public void Ungrab();
	public void ApplyForce(Vector2 force);
}

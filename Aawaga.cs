using System.Text.RegularExpressions;

[GlobalClass]
public partial class Aawaga : RigidBody2D, IGrabbable
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
	float WalkDirection;
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
				WalkDirection = RNG.FlipCoin() ? 1.0f : -1.0f;
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
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				if (Danger() > 50) SetState(AIState.Evade);
			} break;
			case AIState.Wander: {
				WanderTimer -= delta;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				if (Danger() > 100) SetState(AIState.Evade);
			} break;
			case AIState.Evade: {
				if (Danger() < 5) SetState(AIState.Idle);
			} break;
			case AIState.Grabbed: break;
		}
		JumpTimer -= delta;
    }

	float Danger() {
		return 100000/(Player.Position - Position).LengthSquared();
	}

	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		float torque = 0.0f;
		float wallStick = 0.0f;
		switch (State) {
			case AIState.Idle: break;
			case AIState.Wander: {
				torque = WalkDirection;
			} break;
			case AIState.Evade: {
				torque = Math.Sign(Position.X - Player.Position.X);
			} break;
			case AIState.Grabbed: return;
		}
		int contactCount = GetContactCount();

		for (int contact = 0; contact < contactCount; contact++) {
			Vector2 normal = state.GetContactLocalNormal(contact);
			if (normal.Y < 0.5 && normal.Y > -0.5) wallStick = Math.Sign(normal.X);
		}

		if (contactCount > 0) {
			if (JumpTimer < 0) {
				JumpTimer = RNG.Range(3.0, 6.0);
				state.ApplyImpulse(new(0, RNG.Range(-100f, -300f)));
			}
			state.ApplyCentralForce(new Vector2(-2000 * wallStick, -1200) * Scale);
			
			state.ApplyTorque(torque * Size * 18000);
		}
	}

	public bool Grabbable() { return true; }

	public void Grab()
	{
		Collision.Disabled = true;
		Freeze = true;
		State = AIState.Grabbed;
		GravityScale = 0.0f;
	}
	public void Ungrab()
	{
		Collision.Disabled = false;
		Freeze = false;
		State = AIState.Idle;
		GravityScale = 1.0f;
	}
}

public interface IGrabbable
{
	public bool Grabbable();
	public void Grab();
	public void Ungrab();
}

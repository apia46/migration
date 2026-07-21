using System.Text.RegularExpressions;

[GlobalClass]
public partial class Aawaga : RigidBody2D, IGrabbable
{
	static readonly GameRandom RNG = new();

	#nullable disable
	public CharacterBody2D Player;
	Node2D Visuals;
	DebugDrawer DebugDrawer;
	#nullable enable

	float Size;

	bool grabbed;
    public bool Grabbed {get=>grabbed;set=>grabbed=value;}
	
	Vector2 SurfacesNormal = Vector2.Zero;

	public enum AIState {Idle, Wander, Evade, Grabbed};
	AIState State = AIState.Idle;
	Vector2 WanderDirection;
	double BoredomTimer;
	double WanderTimer;
	double JumpTimer;

    public override void _Ready()
	{
		Size = RNG.Range(0.8f, 1.2f);
		Visuals = GetNode<Node2D>("%Visuals");
		DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");
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
				WanderDirection = new Vector2(RNG.FlipCoin() ? -1f : 1f, -1f);
				WanderTimer = RNG.Range(2.0, 4.0);
			} break;
			case AIState.Evade: {
				
			} break;
			case AIState.Grabbed: {
				// Velocity = Vector2.Zero;
			} break;
		}
	}

    public override void _Process(double delta)
	{
		
	}

    public override void _PhysicsProcess(double delta)
    {
		// Vector2 newVelocity = Velocity;

		Vector2 intendedDirection = Vector2.Zero;
		// Vector2 surfaceDirection = GetSurfaceDirection();
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				if (Danger() > 8) SetState(AIState.Evade);
			} break;
			case AIState.Wander: {
				intendedDirection = WanderDirection;

				WanderTimer -= delta;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				if (Danger() > 12) SetState(AIState.Evade);
			} break;
			case AIState.Evade: {
				intendedDirection = Position - Player.Position;

				if (Danger() < 5 && RNG.FlipCoin()) SetState(AIState.Idle);
				// if (Danger() < 5 && RNG.FlipCoin() && IsOnFloorOnly()) SetState(AIState.Idle);
			} break;
			case AIState.Grabbed: return;
		}
		// JumpTimer -= delta;
		// float moveDirection = Math.Sign(intendedDirection.AngleTo(surfaceDirection));
		// if (IsOnFloor() && State != AIState.Thrown) {
		// 	if (JumpTimer < 0) {
		// 		JumpTimer = RNG.Range(6.0, 12.0);
		// 		newVelocity += new Vector2(0f, RNG.Range(-100f, -300f));
		// 	}
		// 	Rotation += moveDirection * 12 * (float)delta;
		// 	newVelocity.X = moveDirection * 60;
		// }
		// if (IsOnWall() && State != AIState.Thrown) {
		// 	float wallDirection = surfaceDirection.X;
		// 	newVelocity.Y = -wallDirection * moveDirection * 60;
		// 	newVelocity.X = wallDirection * 30;
		// 	Rotation += moveDirection * 12 * (float)delta;
		// } else {
		// 	newVelocity.Y += (float)delta * Game.GRAVITY;
		// }
		// Velocity = newVelocity;
		// MoveAndSlide();
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		SurfacesNormal *= 0.7f;
        for (int i = 0; i < GetContactCount(); i++) {
			// Color color;
			switch (state.GetContactLocalNormal(i).Angle()) {
				case < 0.75f*PI and >= 0.25f*PI: {
					SurfacesNormal.Y = 1;
					// color = Colors.Red;
				} break;
				case < 0.25f*PI and >= -0.25f*PI: {
					SurfacesNormal.X = 1;
					// color = Colors.Blue;
				} break;
				case < -0.75f*PI or >= 0.75f*PI: {
					SurfacesNormal.X = -1;
					// color = Colors.Cyan;
				} break;
				default: {
					SurfacesNormal.Y = -1;
					// color = Colors.Green;
				} break;
			}
			// DebugDrawer.AddArrow(state.GetContactLocalPosition(i)-Position, state.GetContactLocalNormal(i) * 10, color);
		}
		Vector2 intendedDirection = Vector2.Zero;
		switch (State) {
			case AIState.Idle: break;
			case AIState.Wander: {
				intendedDirection = WanderDirection;
			} break;
			case AIState.Evade: {
				intendedDirection = Position - Player.Position;
				if (SurfacesNormal.Y < -0.3 && Math.Abs(SurfacesNormal.X) > 0.3) intendedDirection.Y -= 50;
				if (SurfacesNormal.Y < -0.3 && intendedDirection.LengthSquared() < 4000 && LinearVelocity.Dot(intendedDirection.Normalized()) < 100) {
					ApplyImpulse(intendedDirection.Normalized() * 300 * Size);
				}
			} break;
			case AIState.Grabbed: return;
		}
		if (intendedDirection.LengthSquared() > 0) {
			float moveDirection = Math.Sign(intendedDirection.AngleTo(SurfacesNormal));
			if (SurfacesNormal.X == 0 && SurfacesNormal.Y == -1 && JumpTimer < 0) {
				JumpTimer = RNG.Range(6.0, 12.0);
				ApplyImpulse(new Vector2(0f, RNG.Range(-100f, -300f)) * Size);
			}
			if (SurfacesNormal.LengthSquared() > 0.5f) ApplyTorque(moveDirection * -18000 * Size);
			ApplyForce(intendedDirection.Normalized() * 200 * Size);
		}
		if (SurfacesNormal.LengthSquared() > 0.01f && intendedDirection.Y < 0.2) ApplyCentralForce(-SurfacesNormal.Normalized() * Game.GRAVITY);
		else ApplyCentralForce(new(0,Game.GRAVITY));
		
		if (SurfacesNormal.LengthSquared() > 0.01f) DebugDrawer.AddArrow(SurfacesNormal.Normalized()*20, Colors.White);
		if (SurfacesNormal.LengthSquared() > 0.01f) DebugDrawer.AddArrow(SurfacesNormal*20, Colors.Green);
		DebugDrawer.AddArrow(intendedDirection.Normalized()*40, Colors.Yellow);
		DebugDrawer.AddArrow(LinearVelocity, Colors.Cyan);
		DebugDrawer.AddText(new(15, 15), LinearVelocity.Dot(intendedDirection.Normalized()).ToString(), Colors.White);
		DebugDrawer.Rotation = -Rotation;
		DebugDrawer.Evaluate();
    }

	IEnumerable<CollisionShape2D> CollisionShapes() {
		foreach (Node node in GetChildren()) {
			if (node is CollisionShape2D shape) yield return shape;
		}
	}

	float Danger() => 100000/(Player.Position - Position).LengthSquared();

	public bool Grabbable() { return true; }

	public void Grab()
	{
		foreach (CollisionShape2D shape in CollisionShapes()) shape.Disabled = true;
		SetState(AIState.Grabbed);
	}
	public void Ungrab()
	{
		foreach (CollisionShape2D shape in CollisionShapes()) shape.Disabled = false;
		SurfacesNormal = Vector2.Zero;
		SetState(AIState.Idle);
	}
	public void Throw(Vector2 force)
	{
		ApplyImpulse(force);
		Ungrab();
	}
}

public interface IGrabbable
{
	public bool Grabbable();
	public void Grab();
	public void Ungrab();
	public void Throw(Vector2 force);
}

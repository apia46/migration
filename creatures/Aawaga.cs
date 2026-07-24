using System.Text.RegularExpressions;

[GlobalClass]
public partial class Aawaga : RigidBody2D, IGrabbable
{
	static readonly GameRandom RNG = new();

	#nullable disable
	public World World;
	Node2D Visuals;
	DebugDrawer DebugDrawer;
	#nullable enable
	public int Id;

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

	public bool FloodFilled;
	public bool ConnectedToSurface;

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

			} break;
		}
	}

    public override void _PhysicsProcess(double delta)
    {
		switch (State) {
			case AIState.Idle: {
				BoredomTimer -= delta;
				if (BoredomTimer <= 0) SetState(AIState.Wander);
				if (Danger() > 8) SetState(AIState.Evade);
			} break;
			case AIState.Wander: {
				WanderTimer -= delta;
				if (WanderTimer <= 0) SetState(AIState.Idle);
				if (Danger() > 12) SetState(AIState.Evade);
			} break;
			case AIState.Evade: {
				if (Danger() < 3 && RNG.FlipCoin()) SetState(AIState.Idle);
			} break;
			case AIState.Grabbed: return;
		}
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		SurfacesNormal *= 0.7f;
        if (ConnectedToSurface)
			for (int i = 0; i < GetContactCount(); i++)
				switch (state.GetContactLocalNormal(i).Angle()) {
					case < 0.75f*PI and >= 0.25f*PI: SurfacesNormal.Y = 1; break;
					case < 0.25f*PI and >= -0.25f*PI: SurfacesNormal.X = 1; break;
					case < -0.75f*PI or >= 0.75f*PI: SurfacesNormal.X = -1; break;
					default: SurfacesNormal.Y = -1; break;
				}
		
		Vector2 intendedDirection = Vector2.Zero;
		switch (State) {
			case AIState.Idle: break;
			case AIState.Wander: {
				intendedDirection = WanderDirection;
			} break;
			case AIState.Evade: {
				intendedDirection = Position - World.Player.Position;
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
		// DebugDrawer.AddArrow(intendedDirection.Normalized()*40, Colors.Yellow);
		// DebugDrawer.AddArrow(LinearVelocity, Colors.Cyan);
		// DebugDrawer.AddText(new(15, 15), LinearVelocity.Dot(intendedDirection.Normalized()).ToString(), Colors.White);
		// DebugDrawer.Rotation = -Rotation;
		// DebugDrawer.Evaluate();
    }

	IEnumerable<CollisionShape2D> CollisionShapes() {
		foreach (Node node in GetChildren())
			if (node is CollisionShape2D shape) yield return shape;
	}

	float Danger() => 100000/(World.Player.Position - Position).LengthSquared();

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

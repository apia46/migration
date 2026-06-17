using Godot;
using System;

public partial class Aawaga : RigidBody2D
{
	static readonly Random RNG = new();

	CharacterBody2D player;

	double jumpTimer = 2;

	double walkTimer = 0;
	float walkDirection = 1;

	double mood = 0;

	float radius = 5;
	float length = 18;

	public void Initiate(float radius, float length, CharacterBody2D player)
	{
		this.radius = radius;
		this.length = length;
		this.player = player;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CollisionShape2D collideTop = GetNode<CollisionShape2D>("%CollideTop");
		CollisionShape2D collideRight = GetNode<CollisionShape2D>("%CollideRight");
		CollisionShape2D collideLeft = GetNode<CollisionShape2D>("%CollideLeft");
		if (collideTop.Shape is CapsuleShape2D collideShape) {
			collideShape.Radius = radius;
			collideShape.Height = length + radius*2;
		}
		Vector2 top = new Vector2(0, -length);
		Vector2 right = top.Rotated((float)(Math.PI * 2.0/3.0));
		Vector2 left = top.Rotated((float)(Math.PI * 4.0/3.0));
		collideTop.Position = top/2;
		collideRight.Position = right/2;
		collideLeft.Position = left/2;
		Line2D lineRight = GetNode<Line2D>("%LineRight");
		Line2D lineLeftTop = GetNode<Line2D>("%LineLeftTop");
		lineRight.SetPointPosition(1, right);
		lineLeftTop.SetPointPosition(0, top);
		lineLeftTop.SetPointPosition(2, left);
		lineRight.Width = radius * 2;
		lineLeftTop.Width = radius * 2;
	}

    public override void _PhysicsProcess(double delta)
    {
        jumpTimer -= delta;
        if (walkTimer > 0) walkTimer -= delta;
		else if (RNG.NextDouble() < delta)
		{
			walkTimer = (RNG.NextDouble() + 0.5) * 2;
			walkDirection = RNG.NextSingle() > 0.5 ? 1 : -1;
		}
    }
	
	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		Vector2 diff = player.Position - Position;
		if (diff.LengthSquared() > 1e7 && RNG.NextDouble() < 0.01) QueueFree();
        if (diff.LengthSquared() < 10000 && mood < 0.4) state.ApplyTorque(20000 * -Math.Sign(diff.X));
		else if (walkTimer > 0) {
			state.ApplyTorque(15000 * walkDirection);
		}

		if (jumpTimer < 0) {
			jumpTimer = RNG.NextDouble() * 3 + 1;
			state.ApplyImpulse(new Vector2(0, (float)((RNG.NextDouble() + 0.5) * -200)));
			mood = (mood + RNG.NextDouble()) / 2;
		}
	}
}

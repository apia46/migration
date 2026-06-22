using Godot;
using System;

public partial class Aawaga : RigidBody2D
{
	static readonly Random RNG = new();

	public CharacterBody2D Player;

	Line2D lineRight;
	Line2D lineLeftTop;

	double jumpTimer = 2;

	double walkTimer = 0;
	float walkDirection = 1;

	float radius = 5;
	float length = 18;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		radius = RNG.NextSingle() * 5 + 0.8f;
		length = radius * 3 + RNG.NextSingle() * 3;
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
		lineRight = GetNode<Line2D>("%LineRight");
		lineLeftTop = GetNode<Line2D>("%LineLeftTop");
		lineRight.SetPointPosition(1, right);
		lineLeftTop.SetPointPosition(0, top);
		lineLeftTop.SetPointPosition(2, left);
		lineRight.Width = radius * 2;
		lineLeftTop.Width = radius * 2;
	}

    public override void _Process(double delta)
    {
        Color color = new Color("#ffffff").Blend(new Color("#0066ff", (float)jumpTimer));
		lineRight.DefaultColor = color;
		lineLeftTop.DefaultColor = color;
	}

    public override void _PhysicsProcess(double delta)
    {
        jumpTimer -= delta;
        if (walkTimer > 0) walkTimer -= delta;
		else if (RNG.NextDouble()*3 < delta)
		{
			walkTimer = (RNG.NextDouble() + 0.5) * 2;
			walkDirection = RNG.NextSingle() > 0.5 ? 1 : -1;
		}
    }
	
	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		if (jumpTimer < -1) GD.Print("here");
		Vector2 diff = Player.Position - Position;

		if (jumpTimer < 0) {
			jumpTimer = RNG.NextDouble() * 6 + 1;
			state.ApplyImpulse(new Vector2(0, (RNG.NextSingle() + 0.5f) * -200));
		}

		if (diff.LengthSquared() > 1e7 && RNG.NextDouble() < 0.01) QueueFree();
		if (GetContactCount() > 0) {
			float torque = 7000 * radius;
			if (radius < 2 && diff.LengthSquared() < 10000) state.ApplyTorque(torque * -Math.Sign(diff.X));
			else if (walkTimer > 0) state.ApplyTorque(torque * walkDirection);
		}
	}
}

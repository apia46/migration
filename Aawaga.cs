using Godot;
using System;

public partial class Aawaga : RigidBody2D
{
	static readonly Random RNG = new();

	public CharacterBody2D player;

	double jumpTimer = 2;

	double walkTimer = 0;
	float walkDirection = 1;

	double mood = 0;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
	}

    public override void _PhysicsProcess(double delta)
    {
        jumpTimer -= delta;
        if (walkTimer > 0) walkTimer -= delta;
		else if (RNG.NextDouble() < delta * 5)
		{
			walkTimer = (RNG.NextDouble() + 0.5) * 2;
			walkDirection = RNG.NextSingle() > 0.5 ? 1 : -1;
		}
    }
	
	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
		Vector2 diff = player.Position - Position;
        if (Math.Abs(diff.X) < 100 && mood < 0.4) state.ApplyTorque(20000 * -Math.Sign(diff.X));
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

// verlet integration rope thing
// https://www.youtube.com/watch?v=FcnvwtyxLds

[GlobalClass]
public partial class Vine : Line2D
{
    #nullable disable
    public VineGroup Group;
    #nullable enable

    static readonly Vector2 GRAVITY = new(0, 100);

    Segment[] Segments = [];
    public const float SEGMENT_LENGTH = 16f;
    bool Simulated;

    public void Initialise(float targetLength, bool simulated)
    {
        DefaultColor = Color.FromHsv(0.44f,Game.RNG.Range(0.2f,0.3f),Game.RNG.Range(0.2f,0.3f));
        Width = 4;
        Simulated = simulated;
        int segments = (int)(targetLength / SEGMENT_LENGTH);
        Segments = new Segment[segments];
        Vector2 NextStartPoint = Vector2.Zero;
        for (int i = 0; i < Segments.Length; i++) {
            Segments[i] = new Segment(NextStartPoint);
            NextStartPoint.Y += SEGMENT_LENGTH;
        }
        if (!Simulated) Points = [..Segments.Select(s => s.Position)];
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Game.State != Game.States.Play) return;
        if (!Simulated) return;
        if (!Settings.FancyVisuals) return;
        if (GlobalPosition.DistanceSquaredTo(World.Player.GlobalPosition) > Player.OFFSCREEN_RANGE_SQUARED) return;
        for (int i = 0; i < Segments.Length; i++) {
            Segment segment = Segments[i];
            Vector2 velocity = segment.Position - segment.OldPosition;
            segment.OldPosition = segment.Position;
            segment.Position += velocity;
            segment.Position += GRAVITY * (float)delta;
            Segments[i] = segment;
        }
  
        Segments[0].Position = Vector2.Zero;

        const float HALF_TILESIZE = World.CONVERTED_TILE_SIZE/2;

        Vector2 position = Position;
        Vector2 globalPosition = GlobalPosition;

        for (int c = 0; c < 15; c++) // constraints
        for (int i = 0; i < Segments.Length - 1; i++) {
            Segment firstSegment = Segments[i];
            Segment secondSegment = Segments[i+1];

            // keep together
            float dist = firstSegment.Position.DistanceTo(secondSegment.Position);
            float error = dist - SEGMENT_LENGTH;
            Vector2 changeAmount = (firstSegment.Position - secondSegment.Position).Normalized() * error;
            if (i != 0) {
                firstSegment.Position -= changeAmount * 0.5f;
                secondSegment.Position += changeAmount * 0.5f;
            } else secondSegment.Position += changeAmount;

            // circle collision
            if (Math.Abs(secondSegment.Position.X) <= 50)
                for (int coll = 0; coll < DetailManager.CircleColliders.Length; coll++) {
                    CircleCollider collider = DetailManager.CircleColliders[coll];
                    Vector2 toCollide = collider.Position - secondSegment.Position - globalPosition;
                    if (toCollide.LengthSquared() < collider.Radius*collider.Radius) {
                        float distToCollide = toCollide.Length();
                        float penalty = Math.Sign(toCollide.X) != Math.Sign(secondSegment.Position.X) ? 1 : 0.5f;
                        secondSegment.Position -= toCollide/distToCollide * (collider.Radius-distToCollide) * penalty * (50-Math.Abs(secondSegment.Position.X))/250;
                    }
                }

            // tile collision
            for (int coll = 0; coll < Group.TileColliders.Length; coll++) {
                Vector2 fromCollide = secondSegment.Position + position - Group.TileColliders[coll];
                fromCollide.X *= 0.99f;
                if (fromCollide.LengthSquared() > 512) continue;
                float m = fromCollide.Y / fromCollide.X;
                if (float.IsNaN(m)) continue;
                Vector2 squareSurface = (Math.Abs(m) > 1 ? new Vector2(1/m,1)*Math.Sign(fromCollide.Y) : new Vector2(1,m)*Math.Sign(fromCollide.X)) * HALF_TILESIZE;
                if (fromCollide.LengthSquared() < squareSurface.LengthSquared()) {
                    secondSegment.Position += squareSurface-fromCollide;
                }
            }

            Segments[i] = firstSegment;
            Segments[i+1] = secondSegment;
        }
    }

    public override void _Process(double delta)
    {
        if (!Simulated) return;
        if (GlobalPosition.DistanceSquaredTo(World.Player.GlobalPosition) > Player.OFFSCREEN_RANGE_SQUARED) return;
        if (!Settings.FancyVisuals) {
            Points = [Vector2.Zero, new(0, SEGMENT_LENGTH * Segments.Length)];
            return;
        }
        Points = [..Segments.Select(s => s.Position)];
    }
}

struct Segment(Vector2 Position)
{
    public Vector2 Position = Position;
    public Vector2 OldPosition = Position;
}

// verlet integration rope thing
// https://www.youtube.com/watch?v=FcnvwtyxLds

[GlobalClass]
public partial class Vine : Line2D
{
    #nullable disable
    public VineGroup Group;
    #nullable enable

    static readonly Vector2 GRAVITY = new(0, 100);
    readonly Color COLOR = new("#2a3330");

    Segment[] Segments = [];
    float SegmentLength = 7f;

    public void Initialise(float targetLength)
    {
        DefaultColor = COLOR;
        Width = 4;
        Segments = new Segment[Math.Min(30,(int)(targetLength / SegmentLength))];
        Vector2 NextStartPoint = Vector2.Zero;
        for (int i = 0; i < Segments.Length; i++) {
            Segments[i] = new Segment(NextStartPoint);
            NextStartPoint.Y += SegmentLength;
        }

        Vector2I TilePosition = (Vector2I)(GlobalPosition / World.CONVERTED_TILE_SIZE).Floor();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GlobalPosition.DistanceSquaredTo(World.Player.GlobalPosition) > 400000) return;
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

        for (int c = 0; c < 15; c++) // constraints
        for (int i = 0; i < Segments.Length - 1; i++) {
            Segment firstSegment = Segments[i];
            Segment secondSegment = Segments[i+1];

            // keep together
            float dist = firstSegment.Position.DistanceTo(secondSegment.Position);
            float error = dist - SegmentLength;
            Vector2 changeAmount = (firstSegment.Position - secondSegment.Position).Normalized() * error;
            if (i != 0) {
                firstSegment.Position -= changeAmount * 0.5f;
                secondSegment.Position += changeAmount * 0.5f;
            } else secondSegment.Position += changeAmount;

            // circle collision
            if (Math.Abs(secondSegment.Position.X) <= 50)
                foreach (CircleCollider collider in DetailPlacer.CircleColliders) {
                    Vector2 toCollide = collider.Position - secondSegment.Position - GlobalPosition;
                    float distToCollide = toCollide.Length();
                    if (distToCollide < collider.Radius) {
                        float penalty = Math.Sign(toCollide.X) != Math.Sign(secondSegment.Position.X) ? 1 : 0.25f;
                        secondSegment.Position -= toCollide/distToCollide * (collider.Radius-distToCollide) * penalty * (50-Math.Abs(secondSegment.Position.X))/250;
                    }
                }

            // tile collision
            foreach (Vector2 collider in Group.TileColliders) {
                Vector2 fromCollide = secondSegment.Position + GlobalPosition - collider;
                fromCollide.X *= 0.9f;
                float m = fromCollide.Y / fromCollide.X;
                Vector2 squareSurface = (Math.Abs(m) > 1 ? new Vector2(1/m,1)*Math.Sign(fromCollide.Y) : new Vector2(1,m)*Math.Sign(fromCollide.X)) * HALF_TILESIZE;
                if (fromCollide.LengthSquared() < squareSurface.LengthSquared())
                    secondSegment.Position += squareSurface-fromCollide;
            }

            Segments[i] = firstSegment;
            Segments[i+1] = secondSegment;
        }
    }

    public override void _Process(double delta)
    {
        if (GlobalPosition.DistanceSquaredTo(World.Player.GlobalPosition) > 400000) return;
        Points = [..Segments.Select(s => s.Position)];
    }
}

struct Segment(Vector2 Position)
{
    public Vector2 Position = Position;
    public Vector2 OldPosition = Position;
}

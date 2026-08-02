// verlet integration rope thing
// https://www.youtube.com/watch?v=FcnvwtyxLds

[GlobalClass]
public partial class Vine : Line2D, IDetail<Vine>
{
    public static PackedScene Scene {get; set;} = GD.Load<PackedScene>("res://details/vine.tscn");
    public static Dictionary<int, Vine> Instances {get; set;} = [];
    public static int IdIterator {get; set;}
    public int Id {get; set;}

    static readonly Vector2 GRAVITY = new(0, 100);
    readonly Color COLOR = new("#2a3330");

    Segment[] Segments = [];
    float SegmentLength = 7f;

    List<TileCollider> TileColliders = [];

    public override void _Ready()
    {
        DefaultColor = COLOR;
        Width = 4;
        Segments = new Segment[Game.RNG.Range(10,30)];
        Vector2 NextStartPoint = Vector2.Zero;
        for (int i = 0; i < Segments.Length; i++) {
            Segments[i] = new Segment(NextStartPoint);
            NextStartPoint.Y += SegmentLength * 0.25f;
        }

        Vector2I TilePosition = (Vector2I)(Position / World.CONVERTED_TILE_SIZE).Floor();
        
        TileColliders = [];
        int TileDistance = (int)Math.Ceiling(SegmentLength*Segments.Length / World.CONVERTED_TILE_SIZE + 0.5);
        for (int x = -TileDistance; x <= TileDistance; x++)
        for (int y = -TileDistance; y <= TileDistance; y++) {
            Vector2I tile = TilePosition + new Vector2I(x,y);
            if (World.SolidTile(tile)) TileColliders.Add(new((new Vector2(0.5f,0.5f)+tile) * World.CONVERTED_TILE_SIZE));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Position.DistanceSquaredTo(World.Player.Position) > 40000) return; 
        for (int i = 0; i < Segments.Length; i++) {
            Segment segment = Segments[i];
            Vector2 velocity = segment.Position - segment.OldPosition;
            segment.OldPosition = segment.Position;
            segment.Position += velocity;
            segment.Position += GRAVITY * (float)delta;
            Segments[i] = segment;
        }
  
        Segments[0].Position = Vector2.Zero;

        float MinSquaredLength = (float)Math.Pow(SegmentLength*Segments.Length+50, 2);

        IEnumerable<CircleCollider> GetCreatureColliders<T>() where T : Node2D, ICreature<T> {
            return T.Creatures.Values.Where(c=>
                c.CollisionRadius != 0 &&
                c.Position.DistanceSquaredTo(Position) < MinSquaredLength
            ).Select(c=>new CircleCollider(c.Position, c.CollisionRadius));
        }

        CircleCollider[] circleColliders = [..GetCreatureColliders<Aawaga>(), ..GetCreatureColliders<Spider>(), new CircleCollider(World.Player.Position, 15)];
        
        const float HALF_TILESIZE = World.CONVERTED_TILE_SIZE/2;

        for (int c = 0; c < 50; c++) // constraints
        for (int i = 0; i < Segments.Length - 1; i++) {
            Segment firstSegment = Segments[i];
            Segment secondSegment = Segments[i+1];

            // circle collision
            if (Math.Abs(secondSegment.Position.X) <= 50)
                foreach (CircleCollider collider in circleColliders) {
                    Vector2 toCollide = collider.Position - secondSegment.Position - Position;
                    float distToCollide = toCollide.Length();
                    if (distToCollide < collider.Radius) {
                        float penalty = Math.Sign(toCollide.X) != Math.Sign(secondSegment.Position.X) ? 1 : 0.25f;
                        secondSegment.Position -= toCollide/distToCollide * (collider.Radius-distToCollide) * penalty * (50-Math.Abs(secondSegment.Position.X))/250;
                    }
                }

            // keep together
            float dist = firstSegment.Position.DistanceTo(secondSegment.Position);
            float error = dist - SegmentLength;
            Vector2 changeAmount = (firstSegment.Position - secondSegment.Position).Normalized() * error;
            if (i != 0) {
                firstSegment.Position -= changeAmount * 0.5f;
                secondSegment.Position += changeAmount * 0.5f;
            } else secondSegment.Position += changeAmount;

            // tile collision
            foreach (TileCollider collider in TileColliders) {
                Vector2 fromCollide = secondSegment.Position + Position - collider.Position;
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
        Points = [..Segments.Select(s => s.Position)];
    }
}
struct CircleCollider(Vector2 Position, float Radius)
{
    public Vector2 Position = Position;
    public float Radius = Radius;
}

struct TileCollider(Vector2 Position)
{
    public Vector2 Position = Position;
}

struct Segment(Vector2 Position)
{
    public Vector2 Position = Position;
    public Vector2 OldPosition = Position;
}

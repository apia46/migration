[GlobalClass]
public partial class EyeflowerGroup : Node2D, IDetail<EyeflowerGroup>
{
    public int Id { get; set; }

    public static PackedScene Scene { get; set; } = GD.Load<PackedScene>("res://details/eyeflower_group.tscn");
    public static Dictionary<int, EyeflowerGroup> Instances { get; set; } = [];
    public static int IdIterator { get; set; }
	public float CollisionRadius { get; set; }

    #nullable disable
    Line2D Line;
    #nullable restore

    Rid MainDraw;

    Node2D[] Nodes = [];
    Vector2[] NodePositions = []; // +2 margin

    public override void _Ready()
    {
        MainDraw = GetCanvasItem();
        Line = GetNode<Line2D>("%Line");
        Vector2 start = new(0,0);
        Vector2 end = new(100,100);
        Nodes = new Node2D[Game.RNG.Range(6, 10)];
        NodePositions = new Vector2[Nodes.Length+2];
        for (int i = -1; i < Nodes.Length+1; i++) {
            NodePositions[i+1] = start.Lerp(end, (float)i/Nodes.Length);
            if (i != -1 && i != Nodes.Length) {
                NodePositions[i+1] += new Vector2(Game.RNG.Range(-8,8), Game.RNG.Range(-8,8));
            }
        }
        for (int i = 0; i < Nodes.Length; i++) {
            float angle = ((NodePositions[i+1]-NodePositions[i]).Angle() + (NodePositions[i+2]-NodePositions[i+1]).Angle())/2;
            Nodes[i] = Game.RNG.FlipCoin(0.8f) ? EyeflowerLeaf.Scene.Instantiate<EyeflowerLeaf>() : Eyeflower.Scene.Instantiate<Eyeflower>();
            Nodes[i].Position = NodePositions[i+1];
            Nodes[i].Rotation = (Game.RNG.FlipCoin() ? PI/2 : -PI/2) + Game.RNG.Range(-0.3f,0.3f) + angle;
            Nodes[i].Scale = Vector2.One * Game.RNG.Range(0.8f, 1);
            AddChild(Nodes[i]);
        }
        Line.Points = NodePositions;
    }
}

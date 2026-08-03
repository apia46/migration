[GlobalClass]
public partial class VineGroup : Node2D, IDetail<VineGroup>
{
	#nullable disable
	DebugDrawer DebugDrawer;
    #nullable enable

	public static PackedScene Scene {get; set;} = GD.Load<PackedScene>("res://details/vine_group.tscn");
    public static Dictionary<int, VineGroup> Instances {get; set;} = [];
    public static int IdIterator {get; set;}
    public int Id {get; set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");

		Vector2I tile = World.PositionToConvertedTile(Position);

		void CreateVinesWithOffset(Vector2I offset) {
			int length = 0;
			while(World.InteriorTile(tile + offset + new Vector2I(0,++length)));
			for (int i = 0; i < 4; i++) {
				Vine vine = new();
				vine.Initialise((length+Game.RNG.Range(-1.25f, -0.25f)) * World.CONVERTED_TILE_SIZE);
				vine.Position = (offset + new Vector2(Game.RNG.Range(-0.5f,0.5f), -0.5f)) * World.CONVERTED_TILE_SIZE;
				AddChild(vine);
			}
		}

		CreateVinesWithOffset(new(0,0));
		CreateVinesWithOffset(new(1,0));
		CreateVinesWithOffset(new(-1,0));
	}

    public override void _Process(double delta)
    {
        // DebugDrawer.AddArrow(World.Player.Position-Position, Colors.Green);
		// DebugDrawer.Evaluate();
    }
}

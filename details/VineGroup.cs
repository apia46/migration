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

	public List<Vector2> TileColliders = [];

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");

		Vector2I tile = World.PositionToConvertedTile(Position);

		Vector2 RangeExpand(Vector2 range, float point)
		{
			if (point < range.X) range.X = point;
			if (point > range.Y) range.Y = point;
			return range;
		}
		Vector2 lengthRange = new(99999, 0);

		int CreateVinesWithOffset(Vector2I offset) {
			int length = 0;
			while(World.InteriorTile(tile + offset + new Vector2I(0,++length)) && length < 12)
			{
				DebugDrawer.AddCircle((new Vector2(0,length)+offset) * World.CONVERTED_TILE_SIZE, Colors.White, 4);
			}
			for (int i = 0; i < 4; i++) {
				Vine vine = new();
				vine.Initialise((length+Game.RNG.Range(-1.25f, -0.25f)) * World.CONVERTED_TILE_SIZE, Game.RNG.FlipCoin());
				vine.Position = (offset + new Vector2(Game.RNG.Range(-0.5f,0.5f), -0.5f)) * World.CONVERTED_TILE_SIZE;
				vine.Group = this;
				AddChild(vine);
			}
			return length;
		}

		lengthRange = RangeExpand(lengthRange,CreateVinesWithOffset(new(0,0)));
		lengthRange = RangeExpand(lengthRange,CreateVinesWithOffset(new(1,0)));
		lengthRange = RangeExpand(lengthRange,CreateVinesWithOffset(new(-1,0)));

        for (int x = -3; x <= 3; x++)
        for (int y = lengthRange.X < 5 ? -1 : 0; y < lengthRange.Y; y++) {
            Vector2I collideTile = tile + new Vector2I(x,y);
			DebugDrawer.AddCircle((new Vector2(0.5f,0.5f) + collideTile)*World.CONVERTED_TILE_SIZE - Position, World.SolidTile(collideTile) ? Colors.Red : Colors.Green);
			if (World.SolidTile(collideTile)) {
				TileColliders.Add(new Vector2(x,y) * World.CONVERTED_TILE_SIZE);
				// GD.Print(TileColliders[^1]);
			}
        }
		DebugDrawer.Evaluate();
	}

    // public override void _Process(double delta)
    // {
    //     foreach (Vector2 tile in TileColliders) DebugDrawer.AddCircle(tile-Position,Colors.Green);
		
    // }
}

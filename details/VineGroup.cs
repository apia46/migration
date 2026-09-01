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

	public Vector2[] TileColliders = [];

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DebugDrawer = GetNode<DebugDrawer>("%DebugDrawer");

		Vector2I tile = World.PositionToConvertedTile(Position);

		int expandLeft = 0;
		while (expandLeft < 4 && World.InteriorTile(tile + new Vector2I(-1-expandLeft,0)) && World.SolidTile(tile + new Vector2I(-1-expandLeft,-1))) expandLeft++;
		int expandRight = 0;
		while (expandLeft+expandRight < 8 && World.InteriorTile(tile + new Vector2I(1+expandRight,0)) && World.SolidTile(tile + new Vector2I(1+expandRight,-1))) expandRight++;

		static Vector2 RangeExpand(Vector2 range, float point)
		{
			if (point < range.X) range.X = point;
			if (point > range.Y) range.Y = point;
			return range;
		}
		Vector2 lengthRange = new(99999, 0);

		int[] heights = new int[expandLeft+expandRight+1];
		for (int x = -expandLeft; x <= expandRight; x++) {
			int height = 1;
			while(World.InteriorTile(tile + new Vector2I(x,height)) && height < 12) height++;
			heights[x+expandLeft] = height;
			lengthRange = RangeExpand(lengthRange, height);
		}

		float vineX = (expandLeft+0.5f) * -World.CONVERTED_TILE_SIZE + 2;
		float end = (expandRight+0.5f) * World.CONVERTED_TILE_SIZE - 2;
		float middle = (vineX+end)/2;
		while (vineX <= end) {
			float heightScale = 1f-2.5f*(float)Math.Pow((vineX-middle)/(1+expandLeft+expandRight)/World.CONVERTED_TILE_SIZE, 2);
			float tileHeight = heights[Math.Clamp((int)Math.Round(vineX/World.CONVERTED_TILE_SIZE)+expandLeft,0,heights.Length-1)];
			float vineHeight = tileHeight * World.CONVERTED_TILE_SIZE * heightScale + Game.RNG.Range(-0.5f, 0.5f)*World.CONVERTED_TILE_SIZE;
			if (vineHeight >= Vine.SEGMENT_LENGTH * 1) {
                Vine vine = new();
                vine.Initialise(vineHeight, Game.RNG.FlipCoin(0.4f));
				vine.Position = new(vineX, -World.CONVERTED_TILE_SIZE/2);
				vine.Group = this;
				AddChild(vine);
			}
			vineX += Game.RNG.Range(6,12);
		}
		
		List<Vector2> tileColliders = [];
        for (int x = -expandLeft; x <= expandRight; x++)
        for (int y = lengthRange.X < 5 ? -1 : 0; y < lengthRange.Y; y++)
			if (World.SolidTile(tile + new Vector2I(x,y)))
				tileColliders.Add(new Vector2(x,y) * World.CONVERTED_TILE_SIZE);
        
		TileColliders = [..tileColliders];

		DebugDrawer.Evaluate();
	}
}

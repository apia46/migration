[GlobalClass]
public partial class World : Node2D
{
	#nullable disable
	public static TileMapLayer ConvertedTileMapLayer;
	public static ProceduralGenerator ProceduralGenerator;
	public static Camera2D Camera;
	public static Player Player;
	public static CreaturesManager CreaturesManager;
	public static DetailManager DetailManager;
	public static Line2D DebugDraw;
	#nullable enable
	static double time = 0;

	public override void _Ready()
	{
		ProceduralGenerator = GetNode<ProceduralGenerator>("%ProceduralGenerator");
		Camera = GetNode<Camera2D>("%Camera");
		Player = GetNode<Player>("%Player");
		Player.World = this;
		CreaturesManager = GetNode<CreaturesManager>("%CreaturesManager");
		CreaturesManager.World = this;
		DetailManager = GetNode<DetailManager>("%DetailManager");
		DetailManager.World = this;
		ConvertedTileMapLayer = GetNode<TileMapLayer>("%ConvertedTileMapLayer");
		DebugDraw = GetNode<Line2D>("%DebugDraw");
		ProceduralGenerator.StartingArea();
	}

    public override void _Process(double delta)
    {
        time += delta;
    }

	public static Vector2I PositionToChunk(Vector2 position) => (Vector2I)(position / PATTERN_TILE_SIZE / ProceduralGenerator.PATTERN_CHUNK_SIZE).Floor();
	public static Vector2I PositionToConvertedTile(Vector2 position) => (Vector2I)(position / CONVERTED_TILE_SIZE).Floor();

	public static void InitialProcGenFinished()
	{
		GD.Print($"Load time: {time}");
		DetailManager.StartingArea();
		CreaturesManager.StartingArea();
		Game.State = Game.States.Menu;
		Game.StartButton.Text = "Start";
		Game.StartButton.Disabled = false;
	}

	 public static void PlayerCrossedChunkBoundary(Vector2I to, Vector2I from)
	{
		#pragma warning disable CS0162
		if (Game.DEBUG_NO_PROCGEN) return;

		void Update(Vector2I nextTo, Vector2I direction) {
			DetailManager.PlayerCrossedChunkBoundary(nextTo,direction);
			CreaturesManager.PlayerCrossedChunkBoundary(nextTo,direction);
			ProceduralGenerator.PlayerCrossedChunkBoundary(nextTo,direction);
		}

		Vector2I difference = to-from;
		Vector2I direction = difference.Sign();
		for (int i = 1; i <= Math.Abs(difference.X); i++)
			Update(from+new Vector2I(direction.X*i, 0), new(direction.X,0));
		for (int i = 1; i <= Math.Abs(difference.Y); i++)
			Update(from+new Vector2I(difference.X, direction.Y*i), new(0, direction.Y));
		#pragma warning restore CS0162
	}

	public const int PATTERN_TILE_SIZE = 64;
	public const int CONVERTED_TILE_SIZE = 32;

	public static bool InteriorTile(Vector2I tile)
	{
        return GetTileDetails(ConvertedTileMapLayer, tile) switch {
        	{Y:0,Z:0} or {X:1,Y:1 or 2,Z:0} => false,
			{X:0,Y:1,Z:1} => true,
			{Y:2,Z:1} => true,
			{Z:1} => false,
			{X:1,Y:1,Z:2} => true,
            _ => true,
        };
    }
	public static bool SolidTile(Vector2I tile)
	{
        return GetTileDetails(ConvertedTileMapLayer, tile) switch {
            {X:1,Y:0 or 1,Z:0} or {X:0,Y:0,Z:1} => true,
			{X:1,Y:1,Z:2} => false,
			{Z:2} => true,
            _ => false,
        };
    }

	public static float? TileSlopeNormal(Vector2I tile)
	{
        return GetTileDetails(ConvertedTileMapLayer, tile) switch {
            {X:1,Y:0,Z:1} => PI/4,
            {X:1,Y:1,Z:1} => -PI/4,
            {X:2,Y:0,Z:1} => 3*PI/4,
            {X:2,Y:1,Z:1} => -3*PI/4,
            _ => null,
        };
    }

	public static bool TileCorridorCenter(Vector2I tile)
	{
		return GetTileDetails(ConvertedTileMapLayer, tile) switch {
			{X:0 or 2,Y:1,Z:0} or {X:1,Y:2,Z:1} => true,
			_ => false,
		};
	}

	public override void _Input(InputEvent @event)
    {
		#pragma warning disable CS0162
		if (Game.DEBUG_CONTROLS) {
			if (@event.IsActionPressed("toggle")) {	
				GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled = !GetNode<TileMapLayer>("%PatternTileMapLayer").Enabled;
				GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled = !GetNode<TileMapLayer>("%ConvertedTileMapLayer").Enabled;
			} else if (@event.IsActionPressed("spawn")) {	
				CreaturesManager.SpawnCreature<Fish>(Player.Position + new Vector2(30,-30));
			}
		}
		#pragma warning restore CS0162
	}

	public static void DrawDebug(Rect2I rect)
	{
		DebugDraw.SetPointPosition(0, rect.Position * PATTERN_TILE_SIZE);
		DebugDraw.SetPointPosition(1, new Vector2(rect.End.X, rect.Position.Y) * PATTERN_TILE_SIZE);
		DebugDraw.SetPointPosition(2, rect.End * PATTERN_TILE_SIZE);
		DebugDraw.SetPointPosition(3, new Vector2(rect.Position.X, rect.End.Y) * PATTERN_TILE_SIZE);
	}
}

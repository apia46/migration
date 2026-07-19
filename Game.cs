public partial class Game : Control
{
	public const float GRAVITY = 1000.0f;
	#nullable disable
	World world;
	Camera2D minimapCamera;
	ProgressBar hungerBar;
	#nullable enable
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		world = GetNode<World>("%World");
		minimapCamera = GetNode<Camera2D>("%MinimapCamera2D");
		hungerBar = GetNode<ProgressBar>("%HungerBar");
		GetNode<SubViewport>("%SubViewport").World2D = world.GetWorld2D();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		minimapCamera.Position = world.player.Position;
		hungerBar.Value = world.player.Hunger;
		GetNode<Label>("%Label2").Text = world.player.Stillness.ToString();
	}
}

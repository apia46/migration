public partial class Game : Control
{
	public const float GRAVITY = 1000.0f;
	#nullable disable
	World World;
	Camera2D MinimapCamera;
	ProgressBar HungerBar;
	#nullable enable
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		World = GetNode<World>("%World");
		MinimapCamera = GetNode<Camera2D>("%MinimapCamera");
		HungerBar = GetNode<ProgressBar>("%HungerBar");
		GetNode<SubViewport>("%SubViewport").World2D = World.GetWorld2D();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MinimapCamera.Position = World.Player.Position;
		HungerBar.Value = World.Player.Hunger;
		GetNode<Label>("%Label2").Text = World.Player.Stillness.ToString();
	}
}

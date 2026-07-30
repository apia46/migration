public partial class Game : Control
{
	public static readonly GameRandom RNG = new();
	
	public const float GRAVITY = 1000.0f;
	#nullable disable
	World World;
	Camera2D MinimapCamera;
	ProgressBar HungerBar;
	SubViewportContainer GameViewportContainer;
	ShaderMaterial GameViewportShader;
	#nullable enable
	public static Vector2 ScreenSize = new Vector2(400, 400);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		World = GetNode<World>("%World");
		MinimapCamera = GetNode<Camera2D>("%MinimapCamera");
		HungerBar = GetNode<ProgressBar>("%HungerBar");
		GetNode<SubViewport>("%SubViewport").World2D = World.GetWorld2D();
		GameViewportContainer = GetNode<SubViewportContainer>("%GameViewportContainer");
		GameViewportShader = (ShaderMaterial)GameViewportContainer.Material;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MinimapCamera.Position = World.Player.Position;
		HungerBar.Value = World.Player.Hunger;
		GetNode<Label>("%Label2").Text = World.Player.Stillness.ToString();
		ScreenSize = GameViewportContainer.Size;
		GameViewportShader.SetShaderParameter("ScreenSize", ScreenSize);
		GameViewportShader.SetShaderParameter("CameraPosition", World.Player.Position);
	}
}

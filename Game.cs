public partial class Game : Control
{
	public static readonly GameRandom RNG = new();
	
	public const float GRAVITY = 1000.0f;
	#nullable disable
	public static World World;
	Camera2D MinimapCamera;
	ProgressBar HungerBar;
	ProgressBar HealthBar;
	SubViewportContainer GameViewportContainer;
	ShaderMaterial GameViewportShader;
	Label HeightLabel;
	TextureRect Ouchies;
	public static Control LivingUI;
	public static ColorRect BlackScreen;
	public static Button RestButton;
	public static TextureRect YouDied;
	#nullable enable
	public static Vector2 ScreenSize = new Vector2(400, 400);
	public static bool Loading = true;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		World = GetNode<World>("%World");
		MinimapCamera = GetNode<Camera2D>("%MinimapCamera");
		HungerBar = GetNode<ProgressBar>("%HungerBar");
		HealthBar = GetNode<ProgressBar>("%HealthBar");
		GetNode<SubViewport>("%SubViewport").World2D = World.GetWorld2D();
		GameViewportContainer = GetNode<SubViewportContainer>("%GameViewportContainer");
		HeightLabel = GetNode<Label>("%HeightLabel");
		Ouchies = GetNode<TextureRect>("%Ouchies");
		BlackScreen = GetNode<ColorRect>("%BlackScreen");
		RestButton = GetNode<Button>("%RestButton");
		LivingUI = GetNode<Control>("%LivingUI");
		YouDied = GetNode<TextureRect>("%YouDied");
		GameViewportShader = (ShaderMaterial)GameViewportContainer.Material;
		RestButton.Pressed += World.Player.Rest;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		MinimapCamera.Position = World.Player.Position;
		HungerBar.Value = World.Player.Hunger;
		HealthBar.Value = World.Player.Health;
		Color modulate = Ouchies.Modulate;
		Ouchies.Modulate = new(modulate){A=1-(float)World.Player.Health};
		HeightLabel.Text = ((int)(World.Player.Position.Y/-World.PATTERN_TILE_SIZE)).ToString() + "m";
		ScreenSize = GameViewportContainer.Size;
		GameViewportShader.SetShaderParameter("ScreenSize", ScreenSize);
		GameViewportShader.SetShaderParameter("CameraPosition", World.Player.Position);
	}
}

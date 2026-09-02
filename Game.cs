public partial class Game : Control
{
	public const bool DEBUG_CONTROLS = true;
	public const bool DEBUG_NO_PROCGEN = false;
	public const bool DEBUG_NO_SURVIVAL = true;

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
	public static Label ShelterLabel;
	public static Button StartButton;
	public static Control Menu;
	public static TextureRect Sky;
	#nullable enable
	public static Vector2 ScreenSize = new Vector2(400, 400);
	
	public enum States {Play, Loading, Menu, Paused, Starting}
	public static States State = States.Loading;
	public static bool CanPause = false;

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
		StartButton = GetNode<Button>("%StartButton");
		LivingUI = GetNode<Control>("%LivingUI");
		YouDied = GetNode<TextureRect>("%YouDied");
		Sky = GetNode<TextureRect>("%Sky");
		ShelterLabel = GetNode<Label>("%ShelterLabel");
		Menu = GetNode<Control>("%Menu");
		GameViewportShader = (ShaderMaterial)GameViewportContainer.Material;
		RestButton.Pressed += World.Player.Rest;
		StartButton.Pressed += Start;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (State == States.Loading) {
			const int L = ProceduralGenerator.GENERATE_CHUNKS_AROUND_PLAYER*2+1;
			StartButton.Text = $"Loading Map ({ProceduralGenerator.GenCount}/{L*L})";
		} else {
			MinimapCamera.Position = World.Player.Position;
			HungerBar.Value = World.Player.Hunger;
			HealthBar.Value = World.Player.Health;
			Ouchies.Modulate = new(Ouchies.Modulate){A=1-(float)World.Player.Health};
			HeightLabel.Text = ((int)(World.Player.Position.Y/-World.PATTERN_TILE_SIZE)-128).ToString() + "m";
			ScreenSize = GameViewportContainer.Size;
			GameViewportShader.SetShaderParameter("ScreenSize", ScreenSize);
			GameViewportShader.SetShaderParameter("CameraPosition", World.Player.Position);
		}
	}

	public void Start()
	{
		GameViewportContainer.Visible = true;
		Ouchies.Visible = true;
		LivingUI.Visible = true;
		YouDied.Visible = true;
		StartButton.Visible = false;
		State = States.Starting;
		Tween startTween = GetTree().CreateTween();
		startTween.TweenProperty(GetNode<TextureRect>("%Title"), "modulate:a", 0, 0.5);
		startTween.TweenInterval(0.5);
		startTween.TweenProperty(GetNode<TextureRect>("%Title2"), "modulate:a", 1, 0.5);
		startTween.TweenCallback(Callable.From(() => State = States.Play));
		startTween.TweenProperty(Menu, "modulate:a", 0, 2);
		startTween.TweenCallback(Callable.From(Started));
	}

	public void Started()
	{
		Menu.Modulate = Colors.White;
		GetNode<TextureRect>("%Title").Modulate = Colors.White;
		GetNode<TextureRect>("%Title2").Modulate = Colors.Transparent;
		Menu.Visible = false;
		CanPause = true;
		World.Player.ResetTargets();
	}
}

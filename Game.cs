public partial class Game : Control
{
	public const float GRAVITY = 1000.0f;
	#nullable disable
	World World;
	Camera2D MinimapCamera;
	Camera2D Camera;
	ProgressBar HungerBar;
	SubViewportContainer GameViewportContainer;
	ShaderMaterial GameViewportShader;
	#nullable enable

	Vector2 CameraPosition;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		World = GetNode<World>("%World");
		MinimapCamera = GetNode<Camera2D>("%MinimapCamera");
		Camera = GetNode<Camera2D>("%Camera");
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
		GameViewportShader.SetShaderParameter("ScreenSize", GameViewportContainer.Size);
		GameViewportShader.SetShaderParameter("CameraPosition", World.Player.Position);
	}

    public override void _PhysicsProcess(double delta)
    {
        CameraPosition += (World.Player.Position - CameraPosition) * Math.Min(10f * (float)delta, 1f);
		Camera.Position = CameraPosition.Floor();
    }
}

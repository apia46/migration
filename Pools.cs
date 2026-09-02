[GlobalClass]
public partial class Pools : Node2D
{
    #nullable disable
    readonly Vector2 IntendedBgPosition = new(0,(ProceduralGenerator.START_POOLS_TRANSITION+1)*ProceduralGenerator.CONVERTED_CHUNK_SIZE*World.CONVERTED_TILE_SIZE);
    TextureRect Bg;
    CanvasModulate CanvasModulate;
    #nullable enable

    public override void _Ready()
    {
        Bg = GetNode<TextureRect>("%Bg");
        CanvasModulate = GetNode<CanvasModulate>("%CanvasModulate");
    }

    public override void _Process(double delta)
    {
        // 1 for start, 0 for pools
        float transition = Mathf.Clamp(
            World.Player.Position.Y/ProceduralGenerator.CONVERTED_CHUNK_SIZE/World.CONVERTED_TILE_SIZE
            - ProceduralGenerator.START_POOLS_TRANSITION,0f,1f);
        CanvasModulate.Color = Color.FromHsv(0,0,Mathf.Lerp(0.05f,0.7f,transition));
        Vector2 newBgPosition = IntendedBgPosition+(World.Camera.Position-IntendedBgPosition) * 0.3f;
        newBgPosition.X = World.Camera.Position.X+(newBgPosition.X-World.Camera.Position.X)%World.CONVERTED_TILE_SIZE;
        if (newBgPosition.Y < IntendedBgPosition.Y) newBgPosition.Y = Mathf.Min(newBgPosition.Y,World.Camera.Position.Y+(newBgPosition.Y-World.Camera.Position.Y)%World.CONVERTED_TILE_SIZE+World.CONVERTED_TILE_SIZE*15);
        Bg.Position = newBgPosition;
        World.Player.Light1.Energy = Mathf.Lerp(1,0.4f,transition);
        World.Player.Light2.Energy = Mathf.Lerp(1,0.4f,transition);
    }
}

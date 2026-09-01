[GlobalClass]
public partial class Eyeflower : Area2D
{
    public static PackedScene Scene = GD.Load<PackedScene>("res://details/eyeflower.tscn");

    #nullable disable
    Sprite2D Pupil;
    #nullable restore

    static readonly Vector2 PUPIL_CENTER = new(18.5f,0);
    static readonly Color HALLUCINATION_COLOR = new Color("#eeeeee33");
    const float EYE_OFFSET_MAGNITUDE = 2;
    Vector2[] HallucinationBaseLine = [];
    Rid Hallucination;
    double Timer;

    public override void _Ready()
    {
        Hallucination = GetNode<Node2D>("%Hallucination").GetCanvasItem();
        HallucinationBaseLine = GetNode<Line2D>("%HallucinationLine").Points;
        Pupil = GetNode<Sprite2D>("%Pupil");
    }

    public override void _Process(double delta)
    {
        Timer -= delta;
        if (Timer < 0f) {
            Timer = Game.RNG.Range(0.3, 0.4);
            QueueRedraw();
        }
        Vector2 IntendedPosition = PUPIL_CENTER+5*(World.Player.GlobalPosition-GlobalPosition-PUPIL_CENTER).Normalized().Rotated(-Rotation);
        Pupil.Position += (IntendedPosition-Pupil.Position) * 0.5f * (float)delta;
    }

    public override void _Draw()
    {
        RenderingServer.CanvasItemClear(Hallucination);
        for (int j = 0; j < 5; j++) {
            Vector2[] Eye = new Vector2[HallucinationBaseLine.Length];
            Color[] EyeColor = new Color[HallucinationBaseLine.Length];
            for (int i = 0; i < HallucinationBaseLine.Length; i++) {
                Eye[i] = HallucinationBaseLine[i] + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
                EyeColor[i] = HALLUCINATION_COLOR;
            }
            RenderingServer.CanvasItemAddPolyline(Hallucination, Eye, EyeColor, 2);
        }
    }
}

[GlobalClass]
public partial class Eyeflower : Area2D
{
    public static PackedScene Scene = GD.Load<PackedScene>("res://details/eyeflower.tscn");

    #nullable disable
    Node2D Hallucination;
    Sprite2D Pupil;
    #nullable restore

    static readonly Vector2 PUPIL_CENTER = new(18.5f,0);
    static readonly Color HALLUCINATION_COLOR = new Color("#eeeeee33");
    const float EYE_OFFSET_MAGNITUDE = 2;
    Vector2[] HallucinationBaseLine = [];
    Rid HallucinationItem;
    double Timer;
    double HurtTimer = 0;

    public override void _Ready()
    {
        Hallucination = GetNode<Node2D>("%Hallucination");
        HallucinationItem = Hallucination.GetCanvasItem();
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
        
        float minDistanceSquared = 1e8f;
        foreach (Fish fish in Fish.Creatures.Values)
            minDistanceSquared = Mathf.Min(GlobalPosition.DistanceSquaredTo(fish.GlobalPosition), minDistanceSquared);
        
        if (HurtTimer > 0) HurtTimer -= delta;
        else if (minDistanceSquared > 1e4) foreach (Node2D body in GetOverlappingBodies()) {
             if (body is Player player) {
                player.Hurt(0.5);
                player.Velocity = Vector2.Zero;
                HurtTimer = 2f;
            }
        }
        float A = Mathf.Clamp(1-(float)HurtTimer/2, 0, 1) * Mathf.Clamp(minDistanceSquared/1e5f, 0, 1);
        Hallucination.Modulate = new(Hallucination.Modulate){A=A};
        Pupil.Modulate = new(Pupil.Modulate){A=A};
    }

    public override void _Draw()
    {
        RenderingServer.CanvasItemClear(HallucinationItem);
        for (int j = 0; j < 5; j++) {
            Vector2[] Eye = new Vector2[HallucinationBaseLine.Length];
            Color[] EyeColor = new Color[HallucinationBaseLine.Length];
            for (int i = 0; i < HallucinationBaseLine.Length; i++) {
                Eye[i] = HallucinationBaseLine[i] + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
                EyeColor[i] = HALLUCINATION_COLOR;
            }
            RenderingServer.CanvasItemAddPolyline(HallucinationItem, Eye, EyeColor, 2);
        }
    }
}

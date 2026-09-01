[GlobalClass]
public partial class EyeflowerLeaf : Area2D
{
    public static PackedScene Scene = GD.Load<PackedScene>("res://details/eyeflower_leaf.tscn");

    static readonly Vector2 PUPIL_CENTER = new(13,0);
    static readonly Color HALLUCINATION_COLOR = new Color("#eeeeee33");
    static readonly Color[] EYE_COLORS = [HALLUCINATION_COLOR,HALLUCINATION_COLOR,HALLUCINATION_COLOR,HALLUCINATION_COLOR];
    const int PUPIL_INNER_RADIUS = 6;
    const int PUPIL_OUTER_RADIUS = 8;
    const float EYE_OFFSET_MAGNITUDE = 2;
    const float EYE_PINCH = 4;

    #nullable disable
    Node2D Hallucination;
    #nullable restore
    Rid HallucinationItem;
    double Timer;
    double HurtTimer = 0;

    public override void _Ready()
    {
        Hallucination = GetNode<Node2D>("%Hallucination");
        HallucinationItem = Hallucination.GetCanvasItem();
    }

    public override void _Process(double delta)
    {
        float A = Mathf.Clamp(1-(float)HurtTimer/2, 0, 1);
        Hallucination.Modulate = new(Hallucination.Modulate){A=A};
        Timer -= delta;
        if (Timer < 0f) {
            Timer = Game.RNG.Range(0.3, 0.4);
            QueueRedraw();
        }
        if (HurtTimer > 0) HurtTimer -= delta;
        else foreach (Node2D body in GetOverlappingBodies()) {
             if (body is Player player) {
                player.Hurt(0.3);
                player.Velocity = Vector2.Zero;
                HurtTimer = 2f;
            }
        }
    }

    public override void _Draw()
    {
        RenderingServer.CanvasItemClear(HallucinationItem);
        Vector2[] Pupil = new Vector2[20];
        Color[] PupilColors = new Color[Pupil.Length];
        for (int i = 0; i < Pupil.Length; i++) {
            Pupil[i] = PUPIL_CENTER + PUPIL_INNER_RADIUS*Vector2.Right.Rotated(Game.RNG.Range(0,TAU));
            PupilColors[i] = HALLUCINATION_COLOR;
        }
        RenderingServer.CanvasItemAddPolyline(HallucinationItem, Pupil, PupilColors, 3f);
        for (int i = 0; i < 5; i++) {
            Vector2[] Eye = new Vector2[5];
            Eye[0] = new Vector2(-EYE_PINCH,0) + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
            Eye[1] = PUPIL_CENTER+new Vector2(0, -PUPIL_OUTER_RADIUS) + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
            Eye[2] = new Vector2(EYE_PINCH,0) + PUPIL_CENTER*2 + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
            Eye[3] = PUPIL_CENTER+new Vector2(0, PUPIL_OUTER_RADIUS) + Game.RNG.Offset(EYE_OFFSET_MAGNITUDE);
            Eye[4] = Eye[0];
            RenderingServer.CanvasItemAddPolyline(HallucinationItem, Eye, EYE_COLORS, 1f);
        }
    }
}

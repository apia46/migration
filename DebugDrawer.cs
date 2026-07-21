[GlobalClass]
public partial class DebugDrawer : Node2D
{
    public static readonly FontFile SpaceMono = GD.Load<FontFile>("res://assets/SpaceMono-Bold.woff");

    List<Drawing> Drawings = [];
    int DrawingsIndex = 0;
    bool DrawingsChanged = false;
    Rid MainDraw;

    public override void _Ready()
    {
        MainDraw = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(MainDraw, GetCanvasItem());
    }

    public override void _ExitTree()
    {
        RenderingServer.FreeRid(MainDraw);
    }

    public void AddArrow(Vector2 position, Vector2 point, Color color, float width = 2) => AddDrawing(new Drawing() with {Type = Type.Arrow, Position = position, Point = point, Color = color, Width = width});
    public void AddArrow(Vector2 point, Color color, float width = 2) => AddArrow(Vector2.Zero, point, color, width);
    public void AddText(Vector2 position, string text, Color color) => AddDrawing(new Drawing() with {Type = Type.Text, Position = position, Color = color, Text = text});

    void AddDrawing(Drawing drawing)
    {
        if (DrawingsIndex >= Drawings.Count) {
            Drawings.Add(drawing);
            DrawingsChanged = true;
        } else if (Drawings[DrawingsIndex] != drawing) {
            Drawings[DrawingsIndex] = drawing;
            DrawingsChanged = true;
        }
	    DrawingsIndex++;
    }

    public void Evaluate()
    {
        if (DrawingsIndex < Drawings.Count) {
            DrawingsChanged = true;
            Drawings = Drawings[..DrawingsIndex];
        }
        DrawingsIndex = 0;
        if (!DrawingsChanged) return;
        DrawingsChanged = false;
        DrawDrawings();
    }

    public override void _Draw() => DrawDrawings();

    void DrawDrawings()
    {
        RenderingServer.CanvasItemClear(MainDraw);
        foreach (Drawing drawing in Drawings) {
            switch (drawing.Type) {
                case Type.Arrow: {
                    Vector2 arrowPoint = drawing.Position+drawing.Point;
                    RenderingServer.CanvasItemAddLine(MainDraw, drawing.Position, arrowPoint, drawing.Color, drawing.Width);
                    float angle = drawing.Point.Angle();
                    RenderingServer.CanvasItemAddPolygon(MainDraw,
                        [.. (new Vector2[]{new(5,0),new(0,5),new(0,-5)}).Select(v=>v.Rotated(angle)+arrowPoint)],
                        [drawing.Color,drawing.Color,drawing.Color]);
                } break;
                case Type.Text: {
                    SpaceMono.DrawString(MainDraw, drawing.Position, drawing.Text, HorizontalAlignment.Left, -1, 10, drawing.Color);
                } break;
            }
        }
    }

    public enum Type {Arrow, Text}
    readonly struct Drawing
    {
        readonly public Type Type { get; init; }

        readonly public Vector2 Position { get; init; }
        readonly public Color Color { get; init; }
        readonly public Vector2 Point { get; init; } // for Arrow
        readonly public float Width { get; init; } // for Arrow
        readonly public string Text { get; init; } // for Text

        public override bool Equals(object? obj)
        {
            if (obj is Drawing d)
                return d.Type == Type && d.Position == Position && d.Color == Color && d.Point == Point && d.Width == Width && d.Text == Text;
            return false;
        }
        public override int GetHashCode() => HashCode.Combine(Type, Position, Color, Point, Width, Text);
        public static bool operator ==(Drawing c1, Drawing c2) => c1.Equals(c2);
        public static bool operator !=(Drawing c1, Drawing c2)  => !c1.Equals(c2);
    }

}

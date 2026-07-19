namespace migration
{
    class Util
    {
        public static int Fold(Vector2I position, Vector2I size) { return position.X+position.Y*size.X; }
        public static int Fold(int x, int y, Vector2I size) { return x+y*size.X; }
        public static int Fold(int x, int y, int width) { return x+y*width; }
    }
}

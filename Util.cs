namespace migration
{
    class Util
    {
        public const float TAU = 6.283185307179586f;
        public const float PI = 3.141592653589793f;

        public static int Fold(Vector2I position, Vector2I size) => position.X+position.Y*size.X;
        public static int Fold(int x, int y, Vector2I size) => x+y*size.X;
        public static int Fold(int x, int y, int width) => x+y*width;
    }
}

using System.Runtime.CompilerServices; 
namespace migration
{
    class Util
    {
        public const float TAU = 6.283185307179586f;
        public const float PI = 3.141592653589793f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Fold(Vector2I position, Vector2I size) => position.X+position.Y*size.X;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Fold(int x, int y, Vector2I size) => x+y*size.X;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Fold(int x, int y, int width) => x+y*width;

        public static T[] FillArray<T>(T[] array, T value){
            Array.Fill(array, value);
            return array;
        }

        public static Rect2I ExpandRect(Rect2I input, Vector2I amount) => new(input.Position-amount, input.Size+amount*2);
        public static Rect2I ExpandRect(Rect2I input, int amount) => ExpandRect(input, new Vector2I(amount, amount));

        public static Vector3I GetTileDetails(TileMapLayer tileMapLayer, Vector2I tile) {
            Vector2I atlasCoords = tileMapLayer.GetCellAtlasCoords(tile);
            return new(atlasCoords.X, atlasCoords.Y, tileMapLayer.GetCellSourceId(tile));
        }
    }
}

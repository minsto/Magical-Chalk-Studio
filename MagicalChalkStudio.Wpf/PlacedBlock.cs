using MediaColor = System.Windows.Media.Color;

namespace MagicalChalkStudio
{
    public sealed class PlacedBlock
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string BlockId { get; set; } = "minecraft:stone";
        public string BlockState { get; set; } = "";
        public int Layer { get; set; }
        public MediaColor Fill { get; set; } = MediaColor.FromRgb(0xBF, 0xBF, 0xBF);

        public PlacedBlock Clone()
        {
            return new PlacedBlock
            {
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                BlockId = BlockId,
                BlockState = BlockState,
                Layer = Layer,
                Fill = Fill
            };
        }
    }
}

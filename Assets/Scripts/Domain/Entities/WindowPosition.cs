namespace Yobi.Domain.Entities
{
    public sealed class WindowPosition
    {
        public double X { get; }
        public double Y { get; }

        public WindowPosition(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}

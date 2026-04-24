using System;
using System.Collections.Generic;
using System.Windows;

namespace MagicalChalkStudio
{
    public static class GridGeometry
    {
        public static double Snap(double v, double grid) => Math.Round(v / grid) * grid;

        public static List<Point> LineCells(Point a, Point b, double grid)
        {
            int x0 = (int)(a.X / grid);
            int y0 = (int)(a.Y / grid);
            int x1 = (int)(b.X / grid);
            int y1 = (int)(b.Y / grid);
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            var pts = new List<Point>();
            while (true)
            {
                pts.Add(new Point(x0 * grid, y0 * grid));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
            return pts;
        }
    }
}

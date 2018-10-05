using System;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{
    public static class Move
    {
        public static DxfLine CreateLine(DxfPoint from, DxfPoint @to, double z)
        {
            var fromWithHeight = new DxfPoint(from.X, from.Y, z);
            var toWithHeight = new DxfPoint(to.X, to.Y, z);
            return new DxfLine(fromWithHeight, toWithHeight)
            {
                Thickness = 2,
                ColorName = "Draw"
            };
        }

        public static DxfLine CreateMove(DxfPoint from, DxfPoint @to, double z)
        {
            var fromWithHeight = new DxfPoint(from.X, from.Y, z);
            var toWithHeight = new DxfPoint(to.X, to.Y, z);
            return new DxfLine(fromWithHeight, toWithHeight)
            {
                Thickness = 0,
                ColorName = null
            };
        }

        public static DxfLine Scale(DxfLine line, double ratio)
        {
            var p1 = new DxfPoint(
                line.P1.X * ratio,
                line.P1.Y * ratio,
                line.P1.Z
            );

            var p2 = new DxfPoint(
                line.P2.X * ratio,
                line.P2.Y * ratio,
                line.P2.Z
            );

            var result = IsMove(line) ? CreateMove(p1, p2, p2.Z) : CreateLine(p1, p2, p2.Z);
            return result;
        }

        public static DxfLine Transform(DxfLine line, double negativeOffsetX, double negativeOffsetY, double z)
        {
            var p1 = new DxfPoint(
                line.P1.X + negativeOffsetX,
                line.P1.Y + negativeOffsetY,
                0
            );

            var p2 = new DxfPoint(
                line.P2.X + negativeOffsetX,
                line.P2.Y + negativeOffsetY,
                0
            );

            var result = IsMove(line) ? CreateMove(p1, p2, z) : CreateLine(p1, p2, z);
            return result;
        }

        public static bool IsMove(DxfLine line)
        {
            return line.ColorName == null;
        }
    }
}

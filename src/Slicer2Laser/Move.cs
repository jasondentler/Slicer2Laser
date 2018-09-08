using System;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{
    public static class Move
    {
        public static DxfLine CreateLine(DxfPoint from, DxfPoint @to)
        {
            return new DxfLine(from, to)
            {
                Thickness = 2,
                ColorName = "Draw"
            };
        }

        public static DxfLine CreateMove(DxfPoint from, DxfPoint @to)
        {
            return new DxfLine(from, to)
            {
                Thickness = 0,
                ColorName = null
            };
        }

        public static DxfLine TransformAndScale(DxfLine line, double scaleX, double scaleY, double negativeOffsetX, double negativeOffsetY)
        {
            var p1 = new DxfPoint(
                line.P1.X * scaleX - negativeOffsetX,
                line.P1.Y * scaleY - negativeOffsetY,
                0
            );

            var p2 = new DxfPoint(
                line.P2.X * scaleX - negativeOffsetX,
                line.P2.Y * scaleY - negativeOffsetY,
                0
            );

            var result = IsMove(line) ? CreateMove(p1, p2) : CreateLine(p1, p2);
            return result;
        }

        public static bool IsMove(DxfLine line)
        {
            return line.ColorName == null;
        }
    }
}

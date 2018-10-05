using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{
    public static class DxfExtensions
    {

        public static DxfPoint AtHeight(this DxfPoint point, double z)
        {
            return new DxfPoint(point.X, point.Y, z);
        }

        public static DxfLine AtHeight(this DxfLine line, double z)
        {
            var newLine = new DxfLine(line.P1.AtHeight(z), line.P2.AtHeight(z))
            {
                Thickness = line.Thickness,
                ColorName = line.ColorName
            };
            return newLine;
        }

    }
}

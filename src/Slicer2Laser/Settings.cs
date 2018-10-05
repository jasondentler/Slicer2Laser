using System;
using System.Collections.Generic;
using System.Text;

namespace Slicer2Laser
{
    public class Settings
    {

        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public int Passes { get; set; }
        public double Depth { get; set; }
        public int MoveSpeed { get; set; }
        public int DrawSpeed { get; set; }
        public int LaserPowerPercent { get; set; }
    }
}

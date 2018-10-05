using System;
using System.Collections.Generic;
using System.Linq;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{
    public class ShapeTracer
    {
        private readonly IDictionary<DxfPoint, DxfLine> _pathStarts;
        private readonly IDictionary<DxfPoint, DxfLine[]> _linesByPoint;

        public ShapeTracer(IDictionary<DxfPoint, DxfLine> pathStarts, IDictionary<DxfPoint, DxfLine[]> linesByPoint)
        {
            _pathStarts = pathStarts;
            _linesByPoint = linesByPoint;
        }

        public IEnumerable<DxfLine> Trace(Settings settings, ISet<DxfPoint> shape)
        {
            var shapePathStarts = shape.Where(p => _pathStarts.ContainsKey(p)).ToArray();
            var undrawnLinesInShape = shape.SelectMany(point => _linesByPoint[point]).ToHashSet();
            var currentLocation = shapePathStarts.Any() ? shapePathStarts.First() : shape.First();

            while (true)
            {
                while (_linesByPoint[currentLocation].Any(l => undrawnLinesInShape.Contains(l)))
                {
                    var lineToDraw = _linesByPoint[currentLocation].First(l => undrawnLinesInShape.Contains(l));
                    undrawnLinesInShape.Remove(lineToDraw);

                    var nextLocation = lineToDraw.P1 == currentLocation
                        ? lineToDraw.P2
                        : lineToDraw.P1; // Move to the other end of the line

                    yield return Move.CreateLine(currentLocation, nextLocation, 0);
                    currentLocation = nextLocation;
                }

                var distances = shape
                    .Select(point => new
                    {
                        point,
                        distance = Math.Sqrt(Math.Pow(currentLocation.X - point.X, 2.0) +
                                             Math.Pow(currentLocation.Y - point.Y, 2))
                    })
                    .ToDictionary(x => x.point, x => x.distance);

                var nextClosestStart = shapePathStarts
                    .Where(point => _linesByPoint[point].Any(l => undrawnLinesInShape.Contains(l)))
                    .OrderBy(point => distances[point])
                    .Select(p => (DxfPoint?)p)
                    .FirstOrDefault();

                if (!nextClosestStart.HasValue)
                {
                    // No other starts or ends, so go to the nearest point with an undrawn line
                    nextClosestStart = shape
                        .Where(point => _linesByPoint[point].Any(l => undrawnLinesInShape.Contains(l)))
                        .OrderBy(point => distances[point])
                        .Select(p => (DxfPoint?)p)
                        .FirstOrDefault();
                }

                if (!nextClosestStart.HasValue)
                    yield break; // All done with this shape

                yield return Move.CreateMove(currentLocation, nextClosestStart.Value, 0);

                currentLocation = nextClosestStart.Value;
            }
        }

    }
}
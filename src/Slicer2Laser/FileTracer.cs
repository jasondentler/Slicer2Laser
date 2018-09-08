using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IxMilia.Dxf;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{
    public class FileTracer
    {

        public IEnumerable<DxfLine> Trace(Stream data, double totalX, double totalY, double offsetX, double offsetY,
            int passes)
        {
            var lines = GetLines(data, passes).ToArray();

            var points = lines.SelectMany(l => new[] {l.P1, l.P2}).Distinct().ToArray();

            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);

            var drawnTotalX = maxX - minX;
            var drawnTotalY = maxY - minY;

            var scaleX = totalX / drawnTotalX;
            var scaleY = totalY / drawnTotalY;

            var offsetAndTransformOriginX = -offsetX - minX;
            var offsetAndTransformOriginY = -offsetY - minY;

            yield return Move.TransformAndScale(
                Move.CreateMove(new DxfPoint(minX, minY, 0), lines[0].P1),
                scaleX, 
                scaleY, 
                offsetAndTransformOriginX, 
                offsetAndTransformOriginY
            );

            foreach (var line in lines)
            {
                yield return Move.TransformAndScale(line, scaleX, scaleY, offsetAndTransformOriginX, offsetAndTransformOriginY);
            }

            yield return Move.CreateMove(lines.Last().P2, new DxfPoint(offsetX, offsetY, 0));
        }

        private static IEnumerable<DxfLine> GetLines(Stream data, int passes)
        {
            var dxf = DxfFile.Load(data);

            var lines = dxf.Entities
                .OfType<DxfLine>()
                .Where(l => !l.Layer.Equals("annotation", StringComparison.InvariantCultureIgnoreCase))
                .Where(l => !l.Layer.Equals("frame", StringComparison.CurrentCultureIgnoreCase))
                .ToArray();

            var linesByPoint = GroupLinesByPoint(lines);

            var pathStarts = GetPathStarts(linesByPoint);

            var shapeTracer = new ShapeTracer(pathStarts, linesByPoint);

            var shapes = GroupPointsByShape(lines);

            var current = new DxfPoint(0, 0, 0);

            var isFirstShape = true;
            foreach (var shape in shapes)
            {
                var linesForShape = shapeTracer.Trace(shape).ToArray();

                if (!linesForShape.Any()) continue;

                for (var pass = 0; pass < passes; pass++)
                {
                    if (!isFirstShape)
                        yield return Move.CreateMove(current, linesForShape[0].P1); // Move between shapes

                    current = linesForShape[0].P1;

                    foreach (var line in linesForShape)
                    {
                        yield return line;
                        current = line.P2;
                    }

                    isFirstShape = false;
                }
            }
        }

        private static IDictionary<DxfPoint, DxfLine[]> GroupLinesByPoint(IEnumerable<DxfLine> lines)
        {
            var linesByP1 = lines
                .GroupBy(l => l.P1)
                .ToDictionary(g => g.Key, g => g.ToArray());

            var linesByP2 = lines
                .GroupBy(l => l.P2)
                .ToDictionary(g => g.Key, g => g.ToArray());

            var linesByPoints = linesByP1.Keys.Concat(linesByP2.Keys)
                .Distinct()
                .ToDictionary(p => p, p => new List<DxfLine>());

            foreach (var item in linesByPoints)
            {
                if (linesByP1.ContainsKey(item.Key))
                    item.Value.AddRange(linesByP1[item.Key]);

                if (linesByP2.ContainsKey(item.Key))
                    item.Value.AddRange(linesByP2[item.Key]);
            }

            return linesByPoints.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        }

        private static Dictionary<DxfPoint, DxfLine> GetPathStarts(IDictionary<DxfPoint, DxfLine[]> linesByPoint)
        {
            return linesByPoint
                .Where(x => x.Value.Length == 1)
                .ToDictionary(x => x.Key, x => x.Value.Single());
        }

        private static List<HashSet<DxfPoint>> GroupPointsByShape(DxfLine[] lines)
        {
            var shapes = new List<HashSet<DxfPoint>>();

            foreach (var line in lines)
            {
                var memberOf = shapes
                    .Where(set => set.Contains(line.P1) || set.Contains(line.P2))
                    .ToHashSet();

                switch (memberOf.Count)
                {
                    case 0:
                        // New shape
                        var newShape = new HashSet<DxfPoint>();
                        newShape.Add(line.P1);
                        newShape.Add(line.P2);
                        shapes.Add(newShape);
                        break;
                    case 1:
                        // Existing shape
                        var existingShape = memberOf.Single();
                        existingShape.Add(line.P1);
                        existingShape.Add(line.P2);
                        break;
                    default:
                        // Multiple shapes need to be merged

                        var mergedShape = memberOf.SelectMany(s => s).ToHashSet();
                        mergedShape.Add(line.P1);
                        mergedShape.Add(line.P2);

                        shapes.RemoveAll(s => memberOf.Contains(s));
                        shapes.Add(mergedShape);
                        break;
                }
            }

            return shapes;
        }
    }
}
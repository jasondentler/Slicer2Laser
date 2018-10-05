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

        public IEnumerable<DxfLine> Trace(Stream data, Settings settings)
        {
            var dxf = DxfFile.Load(data);
            var unitsConversion = GetConversionToMillimeters(dxf);
            var lines = TraceDxf(dxf, settings, unitsConversion);
            return lines.ToArray();
        }

        private IEnumerable<DxfLine> TraceDxf(DxfFile dxf, Settings settings, double unitsConversion)
        {

            var lines = GetLines(dxf, settings)
                .Select(l => Move.Scale(l, unitsConversion))
                .ToArray();

            if (!lines.Any())
                yield break;

            var points = lines.SelectMany(l => new[] { l.P1, l.P2 }).Distinct().ToArray();

            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);
            var totalX = maxX - minX;
            var totalY = maxY - minY;

            Console.WriteLine($"{totalX:F4}w x {totalY:F4}y");

            var offsetAndTransformOriginX = -settings.OffsetX - minX;
            var offsetAndTransformOriginY = -settings.OffsetY - minY;

            var moveHeight = 0;

            var aboveOrigin = new DxfPoint(0, 0, moveHeight);

            yield return Move.CreateMove(aboveOrigin, aboveOrigin, moveHeight);

            yield return Move.Transform(
                Move.CreateMove(
                    new DxfPoint(minX, minY, moveHeight),
                    lines[0].P1, moveHeight),
                offsetAndTransformOriginX,
                offsetAndTransformOriginY,
                moveHeight
            );

            DxfLine lastLine = null;
            foreach (var line in lines)
            {
                lastLine = Move.Transform(line, offsetAndTransformOriginX, offsetAndTransformOriginY, line.P1.Z);
                yield return lastLine;
            }

            yield return Move.CreateMove(lastLine.P2, lastLine.P2, moveHeight);
            yield return Move.CreateMove(lastLine.P2, new DxfPoint(settings.OffsetX, settings.OffsetY, moveHeight), moveHeight);
        }


        private double GetConversionToMillimeters(DxfFile dxf)
        {
            switch (dxf.Header.DefaultDrawingUnits)
            {
                case DxfUnits.Meters:
                    return 1000;
                case DxfUnits.Unitless: // assume millimeters
                    Console.WriteLine("Unit of measure not defined in DXF. Assuming drawing is in millimeters.");
                    return 1.0;
                case DxfUnits.Millimeters:
                    return 1.0;
                case DxfUnits.Feet:
                    return 25.4 * 12.0;
                case DxfUnits.Inches:
                    return 25.4;
                default:
                    throw new NotSupportedException("DefaultDrawingUnits=" + dxf.Header.DefaultDrawingUnits);
            }
        }

        private static IEnumerable<DxfLine> GetLines(DxfFile dxf, Settings settings)
        {
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
                var linesForShape = shapeTracer.Trace(settings, shape).ToArray();

                if (!linesForShape.Any()) continue;

                var currentHeight = 0.0;
                var stepDownPerPass = settings.Depth / (double) settings.Passes;

                for (var pass = 0; pass < settings.Passes; pass++)
                {
                    if (!isFirstShape || pass != 0)
                    {
                        yield return Move.CreateMove(current, linesForShape[0].P1, currentHeight); // Move between shapes
                    }

                    current = linesForShape[0].P1;

                    foreach (var line in linesForShape)
                    {
                        yield return line.AtHeight(currentHeight);
                        current = line.P2;
                    }

                    currentHeight -= stepDownPerPass;
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
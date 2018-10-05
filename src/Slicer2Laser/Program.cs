using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using IxMilia.Dxf.Entities;

namespace Slicer2Laser
{

    class Program
    {
        static void Main(string[] args)
        {
            var settings = new Settings()
            {
                DrawSpeed = 75,
                MoveSpeed = 1000,
                OffsetX = 0,
                OffsetY = 0,
                Passes = 1,
                Depth = 4,
                LaserPowerPercent = 100
            };

            using (var stream = File.OpenRead(@"C:\Users\Jason\Documents\ToCut\DnD\Ankylosaurus\Ankylosaurus Mount dxf.zip"))
            {
                ZipOfDxfToGCode(
                    stream,
                    @"C:\Users\Jason\Documents\ToCut\DnD\Ankylosaurus\Ankylosaurus Mount dxf",
                    settings);
            }

            Console.WriteLine("Done");
            Console.ReadKey();
        }

        private static void ZipOfDxfToGCode(Stream stream, string outputFolder, Settings settings)
        {

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var entries = GetFiles(stream);
            foreach (var zipEntry in entries)
            {
                var metadata = zipEntry.Item1;
                var data = zipEntry.Item2;

                Console.WriteLine($"Processing {metadata.Name}");

                var path = Path.Combine(outputFolder, Path.GetFileName(metadata.Name));
                path = Path.ChangeExtension(path, "gcode");

                var lines = new FileTracer().Trace(data, settings).ToArray();

                using (var output = new StreamWriter(path, false))
                {
                    WriteGCodeHeader(metadata, output);
                    WriteGCodeMoves(settings, output, lines);
                    WriteGCodeFooter(metadata, output);
                    output.Close();
                }

                Console.WriteLine();
            }
        }

        private static void WriteGCodeHeader(ZipEntry metadata, TextWriter output)
        {
            var asm = Assembly.GetExecutingAssembly();
            output.WriteLine($";Project: {metadata.Name}");
            output.WriteLine($"Created with {asm.GetName().Name} {asm.GetName().Version}");
            output.WriteLine();
            output.WriteLine("G90 ; Absolute positioning");
            output.WriteLine("G21 ; Set units to millimeters");
            output.WriteLine("M84 S1800 ; Change Stepper disable timeout to 30 minutes");
            output.WriteLine("G92 X0 Y0 Z0 ; Set origin");
        }

        private static void WriteGCodeMoves(Settings settings, TextWriter output, IEnumerable<DxfLine> lines)
        {
            var moveSpeed = settings.MoveSpeed;
            var cutSpeed = settings.DrawSpeed;
            const int laserMaxValue = 255;
            var laserOnValue = (int) (laserMaxValue * (settings.LaserPowerPercent / 100.0));

            var laserOnScript = string.Join(Environment.NewLine, new[]
            {
                "M400 ; Don't execute early",
                "M106 S" + laserOnValue + " ; Laser on"
            });

            var laserOffScript = string.Join(Environment.NewLine, new[]
            {
                "M400 ; Don't execute early",
                "M107 ; Laser off"
            });

            output.WriteLine(laserOffScript);
            var wasMove = true;

            foreach (var line in lines)
            {
                var isMove = Move.IsMove(line);

                if (wasMove != isMove)
                {
                    output.WriteLine(isMove ? laserOffScript : laserOnScript);
                }

                output.Write(isMove ? "G1 " : "G0 ");
                output.Write(" X");
                output.Write(line.P2.X.ToString("F4"));
                output.Write(" Y");
                output.Write(line.P2.Y.ToString("F4"));
                output.Write(" Z");
                output.Write(line.P2.Z.ToString("F4"));
                output.Write(" F");
                output.WriteLine(isMove ? moveSpeed : cutSpeed);

                wasMove = isMove;
            }
        }

        private static void WriteGCodeFooter(ZipEntry metadata, TextWriter output)
        {
            output.WriteLine("M400 ; Wait until all moves complete");
            output.WriteLine("M107 ; Laser off");
            output.WriteLine("G4 P2000 ; Pause for 4 seconds");
            output.WriteLine("M400 ; Wait again");
            output.WriteLine("M84; Turn steppers off");
        }

        private static IEnumerable<Tuple<ZipEntry, Stream>> GetFiles(Stream zipFile)
        {
            var buffer = new byte[1024 * 1024 * 10];
            using (var zipStream = new ZipInputStream(zipFile, 1024 * 1024 * 10))
            {
                ZipEntry entry;
                do
                {
                    entry = zipStream.GetNextEntry();
                    if (entry == null) continue;
                    if (!entry.IsFile) continue;
                    if (!Path.GetExtension(entry.Name).EndsWith("dxf", StringComparison.InvariantCultureIgnoreCase)) continue;

                    using (var ms = new MemoryStream())
                    {
                        StreamUtils.Copy(zipStream, ms, buffer);
                        ms.Seek(0, SeekOrigin.Begin);
                        yield return Tuple.Create(entry, (Stream) ms);
                    }
                } while (entry != null);
            }
        }
    }
}

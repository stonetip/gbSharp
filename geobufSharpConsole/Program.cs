using System;
using System.Diagnostics;
using System.IO;
using Stonetip.Geobuf;
using Newtonsoft.Json;

namespace geobufSharpConsole
{
	internal class Program
	{
		private static bool runDecode = true;

		private static void Main(string[] args)
		{
			if (runDecode)
			{
				// Current PBF being parsed...
				using (
					var file =
						File.OpenRead(@"D:\projects\mapture\experiments\geobufSharp\geobufSharpConsole\geo\bhead.pbf")
				)
				{
					var stopWatch = new Stopwatch();
					stopWatch.Start();

					var geoJsonObject = GeobufDecode.ParseGeobufFile(file);

				//	var jsonTest = JsonConvert.SerializeObject(geoJsonObject);

					stopWatch.Stop();

					var ts = stopWatch.Elapsed;

					// Format and display the TimeSpan value.
					var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
					Console.WriteLine("decoding time: " + elapsedTime);

#if DEBUG
				//	 Console.ReadLine();
#endif
				}
			}


			// Encode GeoJSON into PBF
			using (
				var file =
					File.OpenRead(@"D:\projects\mapture\experiments\geobufSharp\geobufSharpConsole\geo\tl_2016_us_county.json")
			)
			{
				var stopWatch = new Stopwatch();
				stopWatch.Start();

				GeobufEncode.ParseGeoJsonFile(file);

				stopWatch.Stop();

				var ts = stopWatch.Elapsed;

				// Format and display the TimeSpan value.
				var elapsedTime = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
				Console.WriteLine("encoding time: " + elapsedTime);

				Console.ReadLine();
			}


		}
	}
}
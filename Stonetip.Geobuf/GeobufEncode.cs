﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Google.Protobuf;
using Google.Protobuf.Collections;
using MapboxGeobuf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stonetip.Geobuf
{
	public static class GeobufEncode
	{
		// constants
		private const int DefaultPrecision = 6; // no need to go beyond meter accuracy so max out at 6

		public static object ParseGeoJsonFile(FileStream file)
		{
			using (var sr = new StreamReader(file))
			{
				using (var jr = new JsonTextReader(sr))
				{
					try
					{
						var genericObj = new JsonSerializer().Deserialize(jr) as JObject;

						if (genericObj == null)
							throw new Exception("Object is null");

						var typeValStr = (string)genericObj.SelectToken("type");

						var enumFound = Enum.TryParse(typeValStr, true, out GeoJSONObjectType geoJsonObjectType);

						if (!enumFound) { throw new Exception("No type found."); }


						var gbd = new Data
						{
							Geometry = new Data.Types.Geometry(),
							Dimensions = 2,
							Precision = DefaultPrecision // For compression, set to default (relying on SanitizePosition to make all coordinates match decimal places)
					};



						switch (geoJsonObjectType)
						{
							case GeoJSONObjectType.Feature:

								var feature = genericObj.ToObject<Feature>();

								var gbdFeature = new Data.Types.Feature();

								var featureInfo = FeatureInfo(feature.Properties, gbd.Keys);

								gbdFeature.Properties.AddRange(featureInfo.Properties);
								gbdFeature.Values.AddRange(featureInfo.Values);

								gbdFeature.Geometry = RouteGeometryParsing(feature.Geometry.Type, JObject.FromObject(feature.Geometry), gbd);

								gbd.Feature = gbdFeature;

								break;


							case GeoJSONObjectType.FeatureCollection:

								var featureCollection = genericObj.ToObject<FeatureCollection>();



								gbd.FeatureCollection = new Data.Types.FeatureCollection();

								foreach (var feature1 in featureCollection.Features)
								{
									try
									{
										var gbdFeature1 = new Data.Types.Feature();

										var featureInfo1 = FeatureInfo(feature1.Properties, gbd.Keys);

										gbdFeature1.Properties.AddRange(featureInfo1.Properties);
										gbdFeature1.Values.AddRange(featureInfo1.Values);

										gbdFeature1.Geometry = RouteGeometryParsing(feature1.Geometry.Type, JObject.FromObject(feature1.Geometry),
											gbd);

										gbd.FeatureCollection.Features.Add(gbdFeature1);
									}
									catch (Exception err)
									{
										Debug.WriteLine(err.Message);
									}
								}


								//Parallel.ForEach(featureCollection.Features, geoJsonFeature =>
								//  {
								//	  try
								//	  {
								//		  var gbDataFeature = new Data.Types.Feature();

								//		  var featureInfo1 = FeatureInfo(geoJsonFeature.Properties, gbd.Keys);

								//		  gbDataFeature.Properties.AddRange(featureInfo1.Properties);
								//		  gbDataFeature.Values.AddRange(featureInfo1.Values);

								//		  gbDataFeature.Geometry = RouteGeometryParsing(geoJsonFeature.Geometry.Type,
								//			  JObject.FromObject(geoJsonFeature.Geometry),
								//			  gbd);

								//		  gbd.FeatureCollection.Features.Add(gbDataFeature);
								//	  }
								//	  catch (Exception err)
								//	  {
								//		  Debug.WriteLine(err.Message);
								//	  }
								//  }
								//);

								//var processedFeatures = featureCollection.Features.AsParallel().Select(geoJsonFeature =>

								//  {
								//      var gbDataFeature = new Data.Types.Feature();

								//      var featureInfo1 = FeatureInfo(geoJsonFeature.Properties, gbd.Keys);

								//      gbDataFeature.Properties.AddRange(featureInfo1.Properties);
								//      gbDataFeature.Values.AddRange(featureInfo1.Values);

								//      gbDataFeature.Geometry = RouteGeometryParsing(geoJsonFeature.Geometry.Type,
								//          JObject.FromObject(geoJsonFeature.Geometry),
								//          gbd);

								//      gbd.FeatureCollection.Features.Add(gbDataFeature);
								//  }



								//).ToList();



								//GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
								//GC.Collect();

								sr.Close();

								sr.Dispose();
								jr.Close();

								featureCollection.Features.Clear();
								genericObj.RemoveAll();

								break;

							case GeoJSONObjectType.Point:
							case GeoJSONObjectType.MultiPoint:
							case GeoJSONObjectType.LineString:
							case GeoJSONObjectType.MultiLineString:
							case GeoJSONObjectType.Polygon:
							case GeoJSONObjectType.MultiPolygon:

								gbd.Geometry = RouteGeometryParsing(geoJsonObjectType, genericObj, gbd);

								break;
							case GeoJSONObjectType.GeometryCollection: // TODO: get this grouped with other geometry types (redundant)

								var geometryCollection = genericObj.ToObject<GeometryCollection>();

								foreach (var geometry in geometryCollection.Geometries)
								{
									var dataGeom = RouteGeometryParsing(geometry.Type, JObject.FromObject(geometry), gbd);

									gbd.Geometry.Geometries.Add(dataGeom);
								}

								gbd.Geometry.Type = Data.Types.Geometry.Types.Type.Geometrycollection;

								break;
							default:
								{
									throw new Exception("No usable type found.");
								}
						}


						var filePath = @"/appFiles/gbSharp/geobufSharpConsole/geo/";

						filePath = @"D:\projects\mapture\experiments\gbSharp\geobufSharpConsole\geo\";

						using (var output =
                               File.Create($"{filePath}test.pbf"))
						{
							var count = gbd.FeatureCollection.Features.Count;

							Console.WriteLine("features count: {0}", count);

							gbd.WriteTo(output);
						}
					}
					catch (Exception err)
					{
						Debug.WriteLine(err.Message);
					}
					var foo = string.Empty;
				}
			}


			return null;
		}



		public class DataFeatureInfo
		{
			public RepeatedField<Data.Types.Value> Values { get; set; }

			public RepeatedField<uint> Properties { get; set; }
		}


		private static DataFeatureInfo FeatureInfo(Dictionary<string, object> inputProperties, RepeatedField<string> gbdKeys)
		{
			var featureInfo = new DataFeatureInfo { Properties = new RepeatedField<uint>(), Values = new RepeatedField<Data.Types.Value>() };


			// TODO: Work in progress
			foreach (var prop in inputProperties)
			{
				if (prop.Value != null)
				{
					var valType = prop.Value.GetType();

					var val = new Data.Types.Value();

					switch (Type.GetTypeCode(valType))
					{
						case TypeCode.String:

							val.StringValue = (string)prop.Value;
							break;
						case TypeCode.Int64:

							var propVal = (long)prop.Value;

							if (propVal > 0)
							{
								val.PosIntValue = Convert.ToUInt64(propVal);
							}
							else
							{
								propVal = propVal * -1;
								val.NegIntValue = Convert.ToUInt64(propVal);
							}

							break;
						case TypeCode.Boolean:

							val.BoolValue = (bool)prop.Value;
							break;
						case TypeCode.Double:

							val.DoubleValue = (double)prop.Value;
							break;
						default:
							if (valType == typeof(JObject)
							) // Would test for jsonValue - use along with others in case statement to parse type
							{
								val.JsonValue = JsonConvert.SerializeObject(prop.Value);
							}
							break;
					}

					featureInfo.Values.Add(val);
				}

				if (!gbdKeys.Contains(prop.Key))
				{
					gbdKeys.Add(prop.Key);
				}

				// Finds the matching index in the master key list
				var index = gbdKeys.ToList().FindIndex(k => k == prop.Key);

				featureInfo.Properties.Add((uint)index);

				featureInfo.Properties.Add((uint)(featureInfo.Values.Count - 1));


			}

			return featureInfo;
		}




		private static Data.Types.Geometry RouteGeometryParsing(GeoJSONObjectType geometryType, JObject genericObj, Data gbData)
		{
			var dataGeom = new Data.Types.Geometry();

			try
			{
				switch (geometryType)
				{
					case GeoJSONObjectType.Point:


						var point = genericObj.ToObject<Point>();

						point = new Point(SanitizePosition(point.Coordinates as Position));

						SetPrecisionAndDimensionsFromPosition((Position)point.Coordinates, gbData);

						dataGeom = ConvertToGeobufPoint(point, gbData);

						break;
					case GeoJSONObjectType.MultiPoint:

						var multiPoint = genericObj.ToObject<MultiPoint>();

                        var cleanPoints = new List<Point>();

						foreach (var pt in multiPoint.Coordinates)
						{
                            var cleanPt = new Point(SanitizePosition(pt.Coordinates as Position));

                            cleanPoints.Add(cleanPt);

							SetPrecisionAndDimensionsFromPosition((Position)pt.Coordinates, gbData);
						}

                        multiPoint = new MultiPoint(cleanPoints);

						dataGeom = ConvertToGeobufMultiPoint(multiPoint, gbData);

						break;
					case GeoJSONObjectType.LineString:

						var lineString = genericObj.ToObject<LineString>();

						for (var index = 0; index < lineString.Coordinates.Count; index++)
						{
							lineString.Coordinates[index] = SanitizePosition(lineString.Coordinates[index] as Position);

							SetPrecisionAndDimensionsFromPosition((Position)lineString.Coordinates[index], gbData);
						}

						dataGeom = ConvertToGeobufLineString(lineString, gbData);

						break;
					case GeoJSONObjectType.MultiLineString:

						var multiLineString = genericObj.ToObject<MultiLineString>();

						foreach (var lineStr in multiLineString.Coordinates)
							for (var index = 0; index < lineStr.Coordinates.Count; index++)
							{
								lineStr.Coordinates[index] = SanitizePosition(lineStr.Coordinates[index] as Position);

								SetPrecisionAndDimensionsFromPosition((Position)lineStr.Coordinates[index], gbData);
							}

						dataGeom = ConvertToGeobufMultiLineString(multiLineString, gbData);


						break;
					case GeoJSONObjectType.Polygon:

						var polygon = genericObj.ToObject<Polygon>();


						foreach (var lineStr in polygon.Coordinates)
						{
							for (var index = 0; index < lineStr.Coordinates.Count; index++)
							{
								lineStr.Coordinates[index] = SanitizePosition(lineStr.Coordinates[index] as Position);

								SetPrecisionAndDimensionsFromPosition((Position)lineStr.Coordinates[index], gbData);
							}
						}

						dataGeom = ConvertToGeobufPolygon(polygon, gbData);

						break;
					case GeoJSONObjectType.MultiPolygon:

						var multiPolygon = genericObj.ToObject<MultiPolygon>();

						foreach (var poly in multiPolygon.Coordinates)
						{
							foreach (var lineStr in poly.Coordinates)
							{
								for (var index = 0; index < lineStr.Coordinates.Count; index++)
								{
									lineStr.Coordinates[index] = SanitizePosition(lineStr.Coordinates[index] as Position);

									SetPrecisionAndDimensionsFromPosition((Position)lineStr.Coordinates[index], gbData);
								}
							}
						}

						dataGeom = ConvertToGeobufMultiPolygon(multiPolygon, gbData);

						break;

					case GeoJSONObjectType.GeometryCollection:

						var geometryCollection = genericObj.ToObject<GeometryCollection>();

						foreach (var geometry in geometryCollection.Geometries)
						{
							var eachGeom = RouteGeometryParsing(geometry.Type, JObject.FromObject(geometry), gbData);

							dataGeom.Geometries.Add(eachGeom);

						}

						dataGeom.Type = Data.Types.Geometry.Types.Type.Geometrycollection;

						break;
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);

			}

			return dataGeom;
		}


		/// <summary>
		///     Standardizes coordinate values in GeoggraphicPosition.
		///     There is no need for lat/lon values to exceed six decimal places (meter accuracy)
		///     or for altitude to be more detailed than a whole number
		/// </summary>
		/// <param name="dirtyPosition"></param>
		/// <returns>GeoJSON.Net.Geometry.Position</returns>
		private static Position SanitizePosition(Position dirtyPosition)
		{
			var cleanLat = Math.Round(dirtyPosition.Latitude, DefaultPrecision, MidpointRounding.AwayFromZero);
			var cleanLon = Math.Round(dirtyPosition.Longitude, DefaultPrecision, MidpointRounding.AwayFromZero);

			if (dirtyPosition.Altitude == null) return new Position(cleanLat, cleanLon);

			// Round altitude to nearest whole number 
			var cleanAlt = Math.Round((double)dirtyPosition.Altitude, 0);

			return new Position(cleanLat, cleanLon, cleanAlt);
		}


		private static void SetPrecisionAndDimensionsFromPosition(Position position, Data gbData)
		{
			try
			{
				var latPrecision = (uint)GetMinPrecision((decimal)position.Latitude);

				var lonPrecision = (uint)GetMinPrecision((decimal)position.Longitude);

               // gbData.Precision = 6;// Math.Max(gbData.Precision, Math.Max(latPrecision, lonPrecision));

				if (position.Altitude != null)
					gbData.Dimensions = 3;


			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}
		}

		private static IEnumerable<long> ProcessPositions(IEnumerable<IPosition> inputPositions, Data gbData)
		{
			var exponentialFactor = Math.Pow(10, gbData.Precision);

			long lastLat = 0;
			long lastLon = 0;
			long lastAlt = 0;

			var dataPositions = new List<long>();

			foreach (var point in inputPositions)
			{
				var position = point as Position;

				var convertedLon = Convert.ToInt64(position.Longitude * exponentialFactor);
				var convertedLat = Convert.ToInt64(position.Latitude * exponentialFactor);

				dataPositions.Add(convertedLon - lastLon);
				dataPositions.Add(convertedLat - lastLat);

				lastLon = convertedLon;
				lastLat = convertedLat;


				if (position.Altitude != null)
				{
					var convertedAlt = Convert.ToInt64(position.Altitude * exponentialFactor);

					dataPositions.Add(convertedAlt - lastAlt);

					lastAlt = convertedAlt;
				}
				else if (gbData.Dimensions == 3)
				{
					dataPositions.Add(lastAlt * -1);

					lastAlt = 0;
				}
			}

			return dataPositions;
		}


		private static Data.Types.Geometry ConvertToGeobufPoint(Point inputPoint, Data gbData)
		{
			var exponentialFactor = Math.Pow(10, gbData.Precision);

			var dataGeom = new Data.Types.Geometry { Type = Data.Types.Geometry.Types.Type.Point };

			var position = inputPoint.Coordinates as Position;

			var convertedLat = Convert.ToInt64(position.Latitude * exponentialFactor);
			var convertedLon = Convert.ToInt64(position.Longitude * exponentialFactor);

			dataGeom.Coords.Add(convertedLon);
			dataGeom.Coords.Add(convertedLat);

			if (position.Altitude == null) return dataGeom;

			dataGeom.Coords.Add(Convert.ToInt64(position.Altitude * exponentialFactor));

			return dataGeom;
		}


		private static Data.Types.Geometry ConvertToGeobufMultiPoint(MultiPoint multiPoint, Data gbData)
		{
			// Gotta convert in this one case
			var positions = multiPoint.Coordinates.Select(point => point.Coordinates as Position)
				.Cast<IPosition>()
				.ToList();

			var processedPositions = ProcessPositions(positions, gbData);

			var dataGeom = new Data.Types.Geometry
			{
				Type = Data.Types.Geometry.Types.Type.Multipoint,
				Coords = { processedPositions }
			};

			return dataGeom;
		}


		private static Data.Types.Geometry ConvertToGeobufLineString(LineString lineString, Data gbData)
		{
			var processedPositions = ProcessPositions(lineString.Coordinates, gbData);

			var dataGeom = new Data.Types.Geometry
			{
				Type = Data.Types.Geometry.Types.Type.Linestring,
				Coords = { processedPositions }
			};

			return dataGeom;
		}


		private static Data.Types.Geometry ConvertToGeobufMultiLineString(MultiLineString multiLineString, Data gbData)
		{
			var lengths = new RepeatedField<uint>();

			var cumulativePositions = new List<long>();

			foreach (var lineString in multiLineString.Coordinates)
			{
				lengths.Add((uint)lineString.Coordinates.Count);

				var processedPositions = ProcessPositions(lineString.Coordinates, gbData);

				cumulativePositions.AddRange(processedPositions);
			}

			var dataGeom = new Data.Types.Geometry
			{
				Type = Data.Types.Geometry.Types.Type.Multilinestring,
				Lengths = { lengths },
				Coords = { cumulativePositions }
			};

			return dataGeom;
		}


		private static Data.Types.Geometry ConvertToGeobufPolygon(Polygon polygon, Data gbData)
		{
			var lengths = new RepeatedField<uint>();

			var cumulativePositions = new List<long>();

			foreach (var lineString in polygon.Coordinates)
			{
				// If multiple parts, we need lengths
				if (polygon.Coordinates.Count > 1)
				{
					lengths.Add((uint)lineString.Coordinates.Count - 1); // minus final coord since it duplicates first in a polygon
				}

				var polyCoords = lineString.Coordinates.Take(lineString.Coordinates.Count - 1);

				var processedPositions = ProcessPositions(polyCoords, gbData);

				cumulativePositions.AddRange(processedPositions);
			}

			var dataGeom = new Data.Types.Geometry
			{
				Type = Data.Types.Geometry.Types.Type.Polygon,
				Lengths = { lengths },
				Coords = { cumulativePositions }
			};

			return dataGeom;
		}


		private static Data.Types.Geometry ConvertToGeobufMultiPolygon(MultiPolygon multiPolygon, Data gbData)
		{
			var lengths = new RepeatedField<uint>();

			var cumulativePositions = new List<long>();

			// First, add a length for total polygons
			lengths.Add((uint)multiPolygon.Coordinates.Count);

			foreach (var polygon in multiPolygon.Coordinates)
			{
				// Add length for each polygon's linestring count
				lengths.Add((uint)polygon.Coordinates.Count);

				foreach (var lineString in polygon.Coordinates)
				{
					// If multiple parts, we need lengths
					//if (polygon.Coordinates.Count > 1)
					//{
					lengths.Add((uint)lineString.Coordinates.Count - 1); // minus final coord since it duplicates first in a polygon
																		 //}

					var polyCoords = lineString.Coordinates.Take(lineString.Coordinates.Count - 1);

					var processedPositions = ProcessPositions(polyCoords, gbData);

					cumulativePositions.AddRange(processedPositions);
				}
			}

			var dataGeom = new Data.Types.Geometry
			{
				Type = Data.Types.Geometry.Types.Type.Multipolygon,
				Lengths = { lengths },
				Coords = { cumulativePositions }
			};

			return dataGeom;
		}


		// Helper method
		public static int GetMinPrecision(this decimal input)
		{
			if (input < 0)
				input = -input;

			var count = 0;
			input -= decimal.Truncate(input);
			while (input != 0)
			{
				++count;
				input *= 10;
				input -= decimal.Truncate(input);
			}

			return count;
		}
	}
}
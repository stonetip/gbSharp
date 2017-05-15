using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeoJSON.Net;
using GeoJSON.Net.Feature;
using GeoJSON.Net.Geometry;
using Google.Protobuf.Collections;
using MapboxGeobuf;

namespace Stonetip.Geobuf
{
	public static class GeobufDecode
	{
		// constants
		private const int DefaultPrecision = 6; // no need to go beyond meter accuracy so max out at 6

		public static object ParseGeobufFile(FileStream file)
		{
			var geoJsonObject = new object();

			try
			{
				var gbd = Data.Parser.ParseFrom(file);

				// Refresh these either using values from GBData or defaults
				var dimensions = (int)(gbd.Dimensions != 0 ? gbd.Dimensions : 2);
				var precision = (int) (gbd.Precision != 0 ? gbd.Precision : DefaultPrecision);
			
				var keys = gbd.Keys;

				switch (gbd.DataTypeCase)
				{
					case Data.DataTypeOneofCase.FeatureCollection:
						geoJsonObject = ProcessFeatureCollection(gbd.FeatureCollection, keys, dimensions, precision);
						break;

					case Data.DataTypeOneofCase.Feature:
						geoJsonObject = ProcessFeature(gbd.Feature, keys, dimensions, precision);
						break;

					case Data.DataTypeOneofCase.Geometry:
						geoJsonObject = ProcessGeometry(gbd.Geometry, dimensions, precision);
						break;

					default:
						{
							throw new Exception("No usable feature/geometry type found.");
						}
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}

			return geoJsonObject;
		}


		public static FeatureCollection ProcessFeatureCollection(Data.Types.FeatureCollection featureCollection,
			RepeatedField<string> gbdKeys, int dimensions, int precision)
		{
			var processedFeatureCollection = new FeatureCollection();

			try
			{
				//Parallel.ForEach(featureCollection.Features, gbdFeature =>
				//{
				//	var processedFeature = ProcessFeature(gbdFeature, gbdKeys, dimensions, precision);

				//	processedFeatureCollection.Features.Add(processedFeature);
				//});

				//object locker = new object();

				//Parallel.ForEach(featureCollection.Features, gbdFeature =>
				//{
				//	lock (locker)
				//	{
				//		var processedFeature = ProcessFeature(gbdFeature, gbdKeys, dimensions, precision);

				//		processedFeatureCollection.Features.Add(processedFeature);
				//	}


				//});

				var processedFeatures = featureCollection.Features.AsParallel()
					.Select(gbdFeature =>ProcessFeature(gbdFeature, gbdKeys, dimensions, precision))
					.ToList();

				processedFeatureCollection.Features.AddRange(processedFeatures);

				//foreach (var gbdFeature in featureCollection.Features)
				//{
				//	var processedFeature = ProcessFeature(gbdFeature, gbdKeys, dimensions, precision);

				//	processedFeatureCollection.Features.Add(processedFeature);
				//}

			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}

			return processedFeatureCollection;
		}


		public static Feature ProcessFeature(Data.Types.Feature gbdFeature, RepeatedField<string> gbdKeys, int dimensions, int precision)
		{
			//var geoJsonFeature = new Feature
			//{
			//	Properties = new Dictionary<string, object>(),
			//	Type = GeoJSONObjectType.Feature // TODO: I think this gets set by init, can probably delete
			//};

			Feature geoJsonFeature = null;

			try
			{
				//ProcessProperties(gbdFeature.Properties, gbdFeature.Values, gbdKeys)
				//	.ToList()
				//	.ForEach(x => geoJsonFeature.Properties[x.Key] = x.Value);

				//geoJsonFeature.Geometry = ProcessGeometry(gbdFeature.Geometry, dimensions, precision);

				var props = ProcessProperties(gbdFeature.Properties, gbdFeature.Values, gbdKeys).ToList();

				var geom = ProcessGeometry(gbdFeature.Geometry, dimensions, precision);

				geoJsonFeature = new Feature(geom);
				
				props.ForEach(x => geoJsonFeature.Properties[x.Key] = x.Value);

				// Test
				//   var json = JsonConvert.SerializeObject(geoJsonFeature);
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}

			return geoJsonFeature;
		}


		public static Dictionary<string, object> ProcessProperties(RepeatedField<uint> properties, RepeatedField<Data.Types.Value> values,
			RepeatedField<string> gbdKeys)
		{
			var featureProperties = new Dictionary<string, object>();

			try
			{
				// Flat list of keys and values indices. Odds are keys, evens are values
				var propsList = properties.Select(Convert.ToInt32).ToList();

				var keysList = PbfListHelpers.GetOddsOrEvensFromList(propsList, true);
				var valuesPositionList = PbfListHelpers.GetOddsOrEvensFromList(propsList, false);

				var dict = keysList.ToDictionary(i => i, i => valuesPositionList[keysList.IndexOf(i)]);

				foreach (var keyValuePair in dict)
				{
					var propName = gbdKeys[keyValuePair.Key];
					var valueData = values[keyValuePair.Value];

					switch (valueData.ValueTypeCase)
					{
						case Data.Types.Value.ValueTypeOneofCase.None:
							break;
						case Data.Types.Value.ValueTypeOneofCase.StringValue:
							featureProperties.Add(propName, valueData.StringValue);
							break;
						case Data.Types.Value.ValueTypeOneofCase.DoubleValue:
							featureProperties.Add(propName, valueData.DoubleValue);
							break;
						case Data.Types.Value.ValueTypeOneofCase.PosIntValue:
							featureProperties.Add(propName, valueData.PosIntValue);
							break;
						case Data.Types.Value.ValueTypeOneofCase.NegIntValue:
							featureProperties.Add(propName, Convert.ToInt64(valueData.NegIntValue) * -1);
							break;
						case Data.Types.Value.ValueTypeOneofCase.BoolValue:
							featureProperties.Add(propName, valueData.BoolValue);
							break;
						case Data.Types.Value.ValueTypeOneofCase.JsonValue:
							featureProperties.Add(propName, valueData.JsonValue);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}

			return featureProperties;
		}


		public static IGeometryObject ProcessGeometry(Data.Types.Geometry inputGeometry, int dimensions, int precision)
		{
			// Process geometry
			var geoJsonFeature = new Feature(null);

			IGeometryObject geoJsonGeometry = null;

			try
			{
				var encodedCoordsSubList =
					PbfListHelpers.SubdivideList(inputGeometry.Coords.ToList(), dimensions);

				var lengths = inputGeometry.Lengths.Select(Convert.ToInt32).ToList(); // int32 - needed to use in loops

				switch (inputGeometry.Type)
				{
					case Data.Types.Geometry.Types.Type.Point:

						geoJsonGeometry = BuildPoint(encodedCoordsSubList[0], dimensions, precision);
						break;

					case Data.Types.Geometry.Types.Type.Linestring:

						geoJsonGeometry = BuildLineString(encodedCoordsSubList, false, dimensions, precision);
						break;

					case Data.Types.Geometry.Types.Type.Multipoint:

						geoJsonGeometry = BuildMultiPoint(encodedCoordsSubList, dimensions, precision);
						break;


					case Data.Types.Geometry.Types.Type.Multilinestring:

						geoJsonGeometry = BuildMultiLineString(encodedCoordsSubList, lengths, dimensions, precision);
						break;


					case Data.Types.Geometry.Types.Type.Polygon:

						geoJsonGeometry = BuildPolygon(encodedCoordsSubList, lengths, dimensions, precision);
						break;

					case Data.Types.Geometry.Types.Type.Multipolygon:

						geoJsonGeometry = BuildMultiPolygon(encodedCoordsSubList, lengths, dimensions, precision);
						break;


					case Data.Types.Geometry.Types.Type.Geometrycollection:

						var geometryCollection = new GeometryCollection();

						foreach (var geometry in inputGeometry.Geometries)
						{
							encodedCoordsSubList =
								PbfListHelpers.SubdivideList(geometry.Coords.ToList(), dimensions);

							lengths = geometry.Lengths.ToList().ConvertAll(x => (int)x);
							switch (geometry.Type)
							{
								case Data.Types.Geometry.Types.Type.Point:
									geometryCollection.Geometries.Add(BuildPoint(encodedCoordsSubList[0], dimensions, precision));
									break;
								case Data.Types.Geometry.Types.Type.Multipoint:
									geometryCollection.Geometries.Add(BuildMultiPoint(encodedCoordsSubList, dimensions, precision));
									break;
								case Data.Types.Geometry.Types.Type.Linestring:
									geometryCollection.Geometries.Add(BuildLineString(encodedCoordsSubList, false, dimensions, precision));
									break;
								case Data.Types.Geometry.Types.Type.Multilinestring:
									geometryCollection.Geometries.Add(BuildMultiLineString(encodedCoordsSubList,
										lengths, dimensions, precision));
									break;
								case Data.Types.Geometry.Types.Type.Polygon:
									geometryCollection.Geometries.Add(BuildPolygon(encodedCoordsSubList, lengths, dimensions, precision));
									break;
								case Data.Types.Geometry.Types.Type.Multipolygon:
									geometryCollection.Geometries.Add(BuildMultiPolygon(encodedCoordsSubList, lengths, dimensions, precision));
									break;
								default:
									throw new ArgumentOutOfRangeException();
							}
						}

						geoJsonGeometry = geometryCollection;

						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.Message);
			}

			return geoJsonGeometry;
		}


		// Returns Geographic position in converted (lat/lon/altitude) coordinates
		public static Position BuildPosition(List<long> encodedCoords, int dimensions, int precision)
		{
			var exponentialFactor = Math.Pow(10, precision);

			var x = encodedCoords[0] / exponentialFactor;

			var y = encodedCoords[1] / exponentialFactor;

			double z = 0;

			if (dimensions > 2)
				z = encodedCoords[2] / exponentialFactor;

			var position = new Position(y, x, z);

			return position;
		}


		public static Point BuildPoint(List<long> encodedCoords, int dimensions, int precision)
		{
			return new Point(BuildPosition(encodedCoords, dimensions, precision));
		}


		public static MultiPoint BuildMultiPoint(List<List<long>> encodedCoordsSubList, int dimensions, int precision)
		{
			var pointsList = new List<Point>();

			for (var i = 0; i < encodedCoordsSubList.Count; i++)
			{
				if (i > 0)
					for (var dim = 0; dim < dimensions; dim++)
						encodedCoordsSubList[i][dim] += encodedCoordsSubList[i - 1][dim];

				var point = BuildPoint(encodedCoordsSubList[i], dimensions, precision);

				pointsList.Add(point);
			}

			return new MultiPoint(pointsList);
		}


		public static LineString BuildLineString(List<List<long>> encodedCoordsList, bool closed, int dimensions, int precision)
		{
			try
			{
				var coordsList = new List<Position>();

				for (var currentCoordsetIndex = 0;
					currentCoordsetIndex < encodedCoordsList.Count;
					currentCoordsetIndex++)
				{
					var currentCoordsetList = encodedCoordsList[currentCoordsetIndex];

					if (currentCoordsetIndex > 0)
					{
						// Get previous coord list so deltas may be added.
						var prevCoordsetList = encodedCoordsList[currentCoordsetIndex - 1];

						// Add deltas to get full values
						currentCoordsetList[0] = currentCoordsetList[0] + prevCoordsetList[0];
						currentCoordsetList[1] = currentCoordsetList[1] + prevCoordsetList[1];

						// Check for altitude value
						if (dimensions > 2)
							currentCoordsetList[2] = currentCoordsetList[2] + prevCoordsetList[2];
					}

					var position = BuildPosition(currentCoordsetList, dimensions, precision);

					coordsList.Add(position);
				}

				// If linestring is part of polygon it must be closed (last pair of coords matches first)
				if (closed)
					coordsList.Add(coordsList[0]);

				return new LineString(coordsList);
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				return null;
			}
		}


		public static MultiLineString BuildMultiLineString(List<List<long>> encodedCoordsSubList, List<int> lengths, int dimensions, int precision)
		{
			var lineStringsList = new List<LineString>();

			var coordsListStart = 0;

			foreach (var lineVertexCount in lengths)
			{
				var coordsList = encodedCoordsSubList.GetRange(coordsListStart, lineVertexCount);

				var lineStr = BuildLineString(coordsList, false, dimensions, precision);

				lineStringsList.Add(lineStr);

				// update start to new position
				coordsListStart += lineVertexCount;
			}

			return new MultiLineString(lineStringsList);
		}


		public static Polygon BuildPolygon(List<List<long>> encodedCoordsSubList, List<int> lengths, int dimensions, int precision)
		{
			if (lengths.Count < 1)
				return new Polygon(new List<LineString> { BuildLineString(encodedCoordsSubList, true, dimensions, precision) });

			var polygonLineStringsList = new List<LineString>();

			var coordsListStart = 0;

			foreach (var ringVertexCount in lengths)
			{
				// ring processing
				var coordsList = encodedCoordsSubList.GetRange(coordsListStart, ringVertexCount);

				var lineStr = BuildLineString(coordsList, true, dimensions, precision);

				polygonLineStringsList.Add(lineStr);

				// update start to new position
				coordsListStart += ringVertexCount;
			}

			return new Polygon(polygonLineStringsList);
		}


		public static MultiPolygon BuildMultiPolygon(List<List<long>> encodedCoordsSubList, List<int> lengths, int dimensions, int precision)
		{
			var coordsListStart = 0;
			var lengthsCursor = 1;

			var multipolyList = new List<Polygon>();

			while (lengthsCursor < lengths.Count)
			{
				//  Debug.WriteLine("coordsListStart: {0}, lengthsCursor: {1}", coordsListStart, lengthsCursor);

				var ringsCount = lengths[lengthsCursor];

				lengthsCursor++;

				var polygonLineStringsList = new List<LineString>();

				for (var move = 0; move < ringsCount; move++)
				{
					var ringVertexCount = lengths[lengthsCursor];

					// ring processing
					var coordsList = encodedCoordsSubList.GetRange(coordsListStart, ringVertexCount);

					var lineStr = BuildLineString(coordsList, true, dimensions, precision);

					polygonLineStringsList.Add(lineStr);

					// update start to new position
					coordsListStart += ringVertexCount;

					lengthsCursor++;
				}

				multipolyList.Add(new Polygon(polygonLineStringsList));
			}

			return new MultiPolygon(multipolyList);
		}
	}
}
using System;
using System.Collections.Generic;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NetTopologySuite.IO.Converters
{
    /// <summary>
    /// Converts an array of <see cref="IGeometry"/>s to and from JSON
    /// </summary>
    public class GeometryArrayConverter : JsonConverter
    {
        private readonly IGeometryFactory _factory;

        /// <summary>
        /// Creates an instance of this class using <see cref="GeometryFactory.Default"/>
        /// </summary>
        public GeometryArrayConverter() : this(GeometryFactory.Default) { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="IGeometryFactory"/>
        /// </summary>
        /// <param name="factory">The factory</param>
        public GeometryArrayConverter(IGeometryFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Writes an array of <see cref="IGeometry"/>s to JSON
        /// </summary>
        /// <param name="writer">The writer</param>
        /// <param name="value">The geometry</param>
        /// <param name="serializer">The serializer</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WritePropertyName("geometries");
            WriteGeometries(writer, value as IList<IGeometry>, serializer);
        }

        private static void WriteGeometries(JsonWriter writer, IEnumerable<IGeometry> geometries, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (IGeometry geometry in geometries)
                serializer.Serialize(writer, geometry);
            writer.WriteEndArray();
        }

        /// <summary>
        /// Reads an array of <see cref="IGeometry"/>s from JSON
        /// </summary>
        /// <param name="reader">The reader</param>
        /// <param name="objectType">The object type</param>
        /// <param name="existingValue">The existing value</param>
        /// <param name="serializer">The serializer</param>
        /// <returns>The geometry array read</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            reader.Read();
            if (!(reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "geometries"))
                throw new Exception();
            reader.Read();
            if (reader.TokenType != JsonToken.StartArray)
                throw new Exception();

            reader.Read();
            List<IGeometry> geoms = new List<IGeometry>();
            while (reader.TokenType != JsonToken.EndArray)
            {
                JObject obj = (JObject)serializer.Deserialize(reader);
                GeoJsonObjectType geometryType = (GeoJsonObjectType)Enum.Parse(typeof(GeoJsonObjectType), obj.Value<string>("type"), true);

                switch (geometryType)
                {
                    case GeoJsonObjectType.Point:
                        geoms.Add(_factory.CreatePoint(ToCoordinate(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.LineString:
                        geoms.Add(_factory.CreateLineString(ToCoordinates(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.Polygon:
                        geoms.Add(CreatePolygon(ToListOfCoordinates(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.MultiPoint:
                        geoms.Add(_factory.CreateMultiPointFromCoords(ToCoordinates(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.MultiLineString:
                        geoms.Add(CreateMultiLineString(ToListOfCoordinates(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.MultiPolygon:
                        geoms.Add(CreateMultiPolygon(ToListOfListOfCoordinates(obj.Value<JArray>("coordinates"))));
                        break;
                    case GeoJsonObjectType.GeometryCollection:
                        throw new NotSupportedException();

                }
                reader.Read();
            }
            return geoms;
        }

        /// <summary>
        /// Predicate function to check if an instance of <paramref name="objectType"/> can be converted using this converter.
        /// </summary>
        /// <param name="objectType">The type of the object to convert</param>
        /// <returns><value>true</value> if the conversion is possible, otherwise <value>false</value></returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(IEnumerable<IGeometry>).IsAssignableFrom(objectType);
        }

        private IMultiLineString CreateMultiLineString(List<Coordinate[]> coordinates)
        {
            ILineString[] strings = new ILineString[coordinates.Count];
            for (int i = 0; i < coordinates.Count; i++)
                strings[i] = _factory.CreateLineString(coordinates[i]);
            return _factory.CreateMultiLineString(strings);
        }

        private IPolygon CreatePolygon(List<Coordinate[]> coordinates)
        {
            ILinearRing shell = _factory.CreateLinearRing(coordinates[0]);
            ILinearRing[] rings = new ILinearRing[coordinates.Count - 1];
            for (int i = 1; i < coordinates.Count; i++)
                rings[i - 1] = _factory.CreateLinearRing(coordinates[i]);
            return _factory.CreatePolygon(shell, rings);
        }

        private IMultiPolygon CreateMultiPolygon(List<List<Coordinate[]>> coordinates)
        {
            IPolygon[] polygons = new IPolygon[coordinates.Count];
            for (int i = 0; i < coordinates.Count; i++)
                polygons[i] = CreatePolygon(coordinates[i]);
            return _factory.CreateMultiPolygon(polygons);
        }

        private static Coordinate ToCoordinate(JArray array)
        {
            Coordinate c = new Coordinate { X = (Double)array[0], Y = (Double)array[1] };
            if (array.Count > 2)
                c.Z = (Double)array[2];
            return c;
        }

        private static Coordinate[] ToCoordinates(JArray array)
        {
            Coordinate[] c = new Coordinate[array.Count];
            for (int i = 0; i < array.Count; i++)
                c[i] = ToCoordinate((JArray)array[i]);
            return c;
        }
        private static List<Coordinate[]> ToListOfCoordinates(JArray array)
        {
            List<Coordinate[]> c = new List<Coordinate[]>();
            for (int i = 0; i < array.Count; i++)
                c.Add(ToCoordinates((JArray)array[i]));
            return c;
        }
        private static List<List<Coordinate[]>> ToListOfListOfCoordinates(JArray array)
        {
            List<List<Coordinate[]>> c = new List<List<Coordinate[]>>();
            for (int i = 0; i < array.Count; i++)
                c.Add(ToListOfCoordinates((JArray)array[i]));
            return c;
        }
    }
}

﻿// OsmSharp - OpenStreetMap tools & library.
// Copyright (C) 2012 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
//using OsmSharp.Osm;
using OsmSharp.Tools.Math.Geo;

namespace OsmSharp.Routing.Route
{
    /// <summary>
    /// Class representing a route generated by OsmSharp.
    /// </summary>
    public class OsmSharpRoute
    {
        /// <summary>
        /// Creates a new route.
        /// </summary>
        public OsmSharpRoute()
        { 
            this.TimeStamp = DateTime.Now;
        }

        /// <summary>
        /// The vehicle type this route was created for.
        /// </summary>
        public VehicleEnum Vehicle { get; set; }

        /// <summary>
        /// Tags for this route.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// Route metrics.
        /// </summary>
        public RouteMetric[] Metrics { get; set; }
        
        /// <summary>
        /// An ordered array of route entries reprenting the details of the route to the next
        /// route point.
        /// </summary>
        public RoutePointEntry[] Entries { get; set; }

        /// <summary>
        /// A timestamp for this route.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        #region Save / Load

#if !WINDOWS_PHONE
        #region Raw Route

        /// <summary>
        /// Saves a serialized version to a file.
        /// </summary>
        /// <param name="file"></param>
        public void Save(FileInfo file)
        {
            Stream stream = file.OpenWrite();
            this.Save(stream);
            stream.Flush();
            stream.Close();
            stream.Dispose();
        }

        /// <summary>
        /// Saves a serialized version to a stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(OsmSharpRoute));
            ser.Serialize(stream, this);
            ser = null;
        }

        /// <summary>
        /// Saves the route as a byte stream.
        /// </summary>
        /// <returns></returns>
        public byte[] SaveToByteArray()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(OsmSharpRoute));
            MemoryStream mem_stream = new MemoryStream();
            //GZipStream stream = new GZipStream(mem_stream, CompressionMode.Compress);
            Stream stream = mem_stream;
            serializer.Serialize(stream, this);
            stream.Flush();
            mem_stream.Flush();
            return mem_stream.ToArray();
        }

        /// <summary>
        /// Loads a route from file.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static OsmSharpRoute Load(FileInfo info)
        {
            return OsmSharpRoute.Load(info.OpenRead());
        }

        /// <summary>
        /// Parses a route from a data stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static OsmSharpRoute Load(Stream stream)
        {
            XmlSerializer ser = new XmlSerializer(typeof(OsmSharpRoute));
            OsmSharpRoute route = ser.Deserialize(stream) as OsmSharpRoute;
            ser = null;
            return route;
        }

        /// <summary>
        /// Parses a route from a byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static OsmSharpRoute Load(byte[] bytes)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(OsmSharpRoute));
            MemoryStream mem_stream = new MemoryStream(bytes);
            //GZipStream stream = new GZipStream(mem_stream, CompressionMode.Decompress);
            Stream stream = mem_stream;
            OsmSharpRoute route = (serializer.Deserialize(stream) as OsmSharpRoute);

            return route;
        }

        #endregion

        #region Gpx

        /// <summary>
        /// Save the route as GPX.
        /// </summary>
        /// <param name="file"></param>
        public void SaveAsGpx(FileInfo file)
        {
            OsmSharp.Routing.Route.Gpx.OsmSharpRouteGpx.Save(file, this);
        }

        #endregion

        #region Kml

        /// <summary>
        /// Saves the route as KML.
        /// </summary>
        /// <param name="file"></param>
        public void SaveAsKml(FileInfo file)
        {
            OsmSharp.Routing.Route.Kml.OsmSharpRouteKml.Save(file, this);
        }

        #endregion
#endif
        #endregion

        #region Create Routes

        /// <summary>
        /// Concatenates two routes.
        /// </summary>
        /// <param name="route1"></param>
        /// <param name="route2"></param>
        /// <returns></returns>
        public static OsmSharpRoute Concatenate(OsmSharpRoute route1, OsmSharpRoute route2)
        {
            return OsmSharpRoute.Concatenate(route1, route2, true);
        }

        /// <summary>
        /// Concatenates two routes.
        /// </summary>
        /// <param name="route1"></param>
        /// <param name="route2"></param>
        /// <param name="clone"></param>
        /// <returns></returns>
        public static OsmSharpRoute Concatenate(OsmSharpRoute route1, OsmSharpRoute route2, bool clone)
        {
            if (route1 == null) return route2;
            if (route2 == null) return route1;
            if (route1.Entries.Length == 0) return route2;
            if (route2.Entries.Length == 0) return route1;
            
            // get the end/start point.
            RoutePointEntry end = route1.Entries[route1.Entries.Length - 1];
            RoutePointEntry start = route2.Entries[0];

            // only do all this if the routes are 'concatenable'.
            if (end.Latitude == start.Latitude &&
                end.Longitude == start.Longitude)
            {
                // construct the new route.
                OsmSharpRoute route = new OsmSharpRoute();

                // concatenate points.
                List<RoutePointEntry> entries = new List<RoutePointEntry>();
                // add points for the first route except the last point.
                for (int idx = 0; idx < route1.Entries.Length - 1; idx++)
                {
                    if (clone)
                    {
                        entries.Add(route1.Entries[idx].Clone() as RoutePointEntry);
                    }
                    else
                    {
                        entries.Add(route1.Entries[idx]);
                    }
                }
                // add points of the next route.
                for (int idx = 0; idx < route2.Entries.Length; idx++)
                {
                    if (clone)
                    {
                        entries.Add(route2.Entries[idx].Clone() as RoutePointEntry);
                    }
                    else
                    {
                        entries.Add(route2.Entries[idx]);
                    }
                }
                route.Entries = entries.ToArray();

                // concatenate tags.
                List<RouteTags> tags = new List<RouteTags>();
                if (route1.Tags != null) { tags.AddRange(route1.Tags); }
                if (route2.Tags != null) { tags.AddRange(route2.Tags); }
                route.Tags = tags.ToArray();
                
                //// calculate metrics.
                //Routing.Core.Metrics.Time.TimeCalculator calculator = new OsmSharp.Routing.Metrics.Time.TimeCalculator();
                //Dictionary<string, double> metrics = calculator.Calculate(route);
                //route.TotalDistance = metrics[Routing.Core.Metrics.Time.TimeCalculator.DISTANCE_KEY];
                //route.TotalTime = metrics[Routing.Core.Metrics.Time.TimeCalculator.TIME_KEY];

                return route;
            }
            else
            {
                throw new ArgumentOutOfRangeException("Contatenation routes can only be done when the end point of the first route equals the start of the second.");
            }
        }

        #endregion

        #region Metrics and Calculations

        #region Total Distance

        /// <summary>
        /// The distance in meter.
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// The time in seconds.
        /// </summary>
        public double TotalTime { get; set; }

        #endregion

        #region Bounding Box

        /// <summary>
        /// Returns the bounding box around this route.
        /// </summary>
        /// <returns></returns>
        public GeoCoordinateBox GetBox()
        {
            return new GeoCoordinateBox(this.GetPoints().ToArray());
        }

        #endregion

        #region Points List

        /// <summary>
        /// Returns the points along the route for the entire route in the correct order.
        /// </summary>
        /// <returns></returns>
        public List<GeoCoordinate> GetPoints()
        {
            List<GeoCoordinate> coordinates = new List<GeoCoordinate>();
            for (int p = 0; p < this.Entries.Length; p++)
            {
                coordinates.Add(new GeoCoordinate(this.Entries[p].Latitude, this.Entries[p].Longitude));
            }
            return coordinates;
        }

        #endregion

        #endregion
    }

    /// <summary>
    /// Structure representing one point in a route that has been routed to.
    /// </summary>
    public class RoutePoint : ICloneable
    {
        /// <summary>
        /// The name of the point.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }
        
        /// <summary>
        /// Tags for this route point.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// Route metrics.
        /// </summary>
        public RouteMetric[] Metrics { get; set; }

        /// <summary>
        /// Distance in meter to reach this point.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Estimated time in seconds to reach this point.
        /// </summary>
        public double Time { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Clones this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RoutePoint clone = new RoutePoint();
            clone.Distance = this.Distance;
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Metrics != null)
            {
                clone.Metrics = new RouteMetric[this.Metrics.Length];
                for (int idx = 0; idx < this.Metrics.Length; idx++)
                {
                    clone.Metrics[idx] = this.Metrics[idx].Clone() as RouteMetric;
                }
            }
            clone.Name = this.Name;
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            clone.Time = this.Time;
            return clone;            
        }

        #endregion
    }

    /// <summary>
    /// Structure representing one point in a route.
    /// </summary>
    public class RoutePointEntry : ICloneable
    {
        /// <summary>
        /// The type of this entry.
        /// Start: Has no way from, distance from, angle or angles on poi's.
        /// Along: Has all data.
        /// Stop: Has all data but is the end point.
        /// </summary>
        public RoutePointEntryType Type { get; set; }

        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }

        /// <summary>
        /// Tags of this entry.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        /// <summary>
        /// Route metrics.
        /// </summary>
        public RouteMetric[] Metrics { get; set; }

        /// <summary>
        /// Distance in meter to reach this part of the route.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Estimated time in seconds to reach this part of the route.
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// The points this route travels along.
        /// 
        /// Between each two points there exists a route represented by the entries array.
        /// </summary>
        public RoutePoint[] Points { get; set; }

        #region Ways

        /// <summary>
        /// The name of the way the route comes from.
        /// </summary>
        public string WayFromName { get; set; }

        /// <summary>
        /// All the names of the ways indexed according to the alpha-2 code of ISO 639-1.
        /// </summary>
        public RouteTags[] WayFromNames { get; set; }

        #endregion

        /// <summary>
        /// The side streets entries.
        /// </summary>
        public RoutePointEntrySideStreet[] SideStreets { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Clones this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RoutePointEntry clone = new RoutePointEntry();
            clone.Distance = this.Distance;
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Metrics != null)
            {
                clone.Metrics = new RouteMetric[this.Metrics.Length];
                for (int idx = 0; idx < this.Metrics.Length; idx++)
                {
                    clone.Metrics[idx] = this.Metrics[idx].Clone() as RouteMetric;
                }
            }
            if (this.Points != null)
            {
                clone.Points = new RoutePoint[this.Points.Length];
                for (int idx = 0; idx < this.Points.Length; idx++)
                {
                    clone.Points[idx] = this.Points[idx].Clone() as RoutePoint;
                }
            }
            if (this.SideStreets != null)
            {
                clone.SideStreets = new RoutePointEntrySideStreet[this.SideStreets.Length];
                for (int idx = 0; idx < this.SideStreets.Length; idx++)
                {
                    clone.SideStreets[idx] = this.SideStreets[idx].Clone() as RoutePointEntrySideStreet;
                }
            }
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            clone.Time = this.Time;
            clone.Type = this.Type;
            clone.WayFromName = this.WayFromName;
            if (this.WayFromNames != null)
            {
                clone.WayFromNames = new RouteTags[this.WayFromNames.Length];
                for (int idx = 0; idx < this.WayFromNames.Length; idx++)
                {
                    clone.WayFromNames[idx] = this.WayFromNames[idx].Clone() as RouteTags;
                }
            }
            return clone;
        }

        #endregion
    }

    /// <summary>
    /// Represents a type of point entry.
    /// </summary>
    public enum RoutePointEntryType
    {
        /// <summary>
        /// Start type.
        /// </summary>
        Start,
        /// <summary>
        /// Along type.
        /// </summary>
        Along,
        /// <summary>
        /// Stop type.
        /// </summary>
        Stop
    }

    /// <summary>
    /// Route point entry.
    /// </summary>
    public class RoutePointEntrySideStreet : ICloneable
    {
        /// <summary>
        /// The latitude of the entry.
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// The longitude of the entry.
        /// </summary>
        public float Longitude { get; set; }

        /// <summary>
        /// Tags of this entry.
        /// </summary>
        public RouteTags[] Tags { get; set; }

        #region Ways

        /// <summary>
        /// The name of the way the route comes from.
        /// </summary>
        public string WayName { get; set; }

        /// <summary>
        /// All the names of the ways indexed according to the alpha-2 code of ISO 639-1.
        /// </summary>
        public RouteTags[] WayNames { get; set; }

        #endregion

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RoutePointEntrySideStreet clone = new RoutePointEntrySideStreet();
            clone.Latitude = this.Latitude;
            clone.Longitude = this.Longitude;
            if (this.Tags != null)
            {
                clone.Tags = new RouteTags[this.Tags.Length];
                for (int idx = 0; idx < this.Tags.Length; idx++)
                {
                    clone.Tags[idx] = this.Tags[idx].Clone() as RouteTags;
                }
            }
            clone.WayName = this.WayName;
            if (this.WayNames != null)
            {
                clone.WayNames = new RouteTags[this.WayNames.Length];
                for (int idx = 0; idx < this.WayNames.Length; idx++)
                {
                    clone.WayNames[idx] = this.WayNames[idx].Clone() as RouteTags;
                }
            }
            return clone;
        }

        #endregion
    }

    /// <summary>
    /// Represents a key value pair.
    /// </summary>
    public class RouteTags : ICloneable
    {
        /// <summary>
        /// The key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The value.
        /// </summary>
        public string Value { get; set; }

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RouteTags clone = new RouteTags();
            clone.Key = this.Key;
            clone.Value = this.Value;
            return clone;
        }

        #endregion
    }

    /// <summary>
    /// Contains extensions for route tags.
    /// </summary>
    public static class RouteTagsExtensions
    {
        /// <summary>
        /// Converts a dictionary of tags to a RouteTags array.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteTags[] ConvertFrom(this IDictionary<string, string> tags)
        {
            List<RouteTags> tags_list = new List<RouteTags>();
            foreach (KeyValuePair<string, string> pair in tags)
            {
                RouteTags tag = new RouteTags();
                tag.Key = pair.Key;
                tag.Value = pair.Value;
                tags_list.Add(tag);
            }
            return tags_list.ToArray();
        }

        /// <summary>
        /// Converts a list of KeyValuePairs to a RouteTags array.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteTags[] ConvertFrom(this List<KeyValuePair<string, string>> tags)
        {
            List<RouteTags> tags_list = new List<RouteTags>();
            if (tags != null)
            {
                foreach (KeyValuePair<string, string> pair in tags)
                {
                    RouteTags tag = new RouteTags();
                    tag.Key = pair.Key;
                    tag.Value = pair.Value;
                    tags_list.Add(tag);
                }
            }
            return tags_list.ToArray();
        }

        /// <summary>
        /// Converts a RouteTags array to a list of KeyValuePairs.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static List<KeyValuePair<string, string>> ConvertTo(this RouteTags[] tags)
        {
            List<KeyValuePair<string, string>> tags_list = new List<KeyValuePair<string, string>>();
            if (tags != null)
            {
                foreach (RouteTags pair in tags)
                {
                    tags_list.Add(new KeyValuePair<string, string>(pair.Key, pair.Value));
                }
            }
            return tags_list;
        }

        /// <summary>
        /// Returns the value of the first tag with the key given.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetValueFirst(this RouteTags[] tags, string key)
        {
            string first_value = null;
            if (tags != null)
            {
                foreach (RouteTags tag in tags)
                {
                    if (tag.Key == key)
                    {
                        first_value = tag.Value;
                        break;
                    }
                }
            }
            return first_value;
        }

        /// <summary>
        /// Returns all values for a given key.
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static List<string> GetValues(this RouteTags[] tags, string key)
        {
            List<string> values = new List<string>();
            if (tags != null)
            {
                foreach (RouteTags tag in tags)
                {
                    if (tag.Key == key)
                    {
                        values.Add(tag.Value);
                    }
                }
            }
            return values;
        }
    }

    /// <summary>
    /// Represents a key value pair.
    /// </summary>
    public class RouteMetric : ICloneable
    {
        /// <summary>
        /// The key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The value.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Convert from a regular tag dictionary.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static RouteMetric[] ConvertFrom(IDictionary<string, double> tags)
        {
            List<RouteMetric> tags_list = new List<RouteMetric>();
            foreach (KeyValuePair<string, double> pair in tags)
            {
                RouteMetric tag = new RouteMetric();
                tag.Key = pair.Key;
                tag.Value = pair.Value;
                tags_list.Add(tag);
            }
            return tags_list.ToArray();
        }

        /// <summary>
        /// Converts to regular tags list.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static List<KeyValuePair<string, double>> ConvertTo(RouteMetric[] tags)
        {
            List<KeyValuePair<string, double>> tags_list = new List<KeyValuePair<string, double>>();
            if (tags != null)
            {
                foreach (RouteMetric pair in tags)
                {
                    tags_list.Add(new KeyValuePair<string, double>(pair.Key, pair.Value));
                }
            }
            return tags_list;
        }

        #region ICloneable Members

        /// <summary>
        /// Returns a clone of this object.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            RouteMetric clone = new RouteMetric();
            clone.Key = this.Key;
            clone.Value = this.Value;
            return clone;
        }

        #endregion
    }
}

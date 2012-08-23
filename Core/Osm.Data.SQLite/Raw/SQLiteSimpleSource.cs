﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Osm.Core;
using Osm.Core.Factory;
using Osm.Core.Filters;
using Tools.Math.Geo;

namespace Osm.Data.SQLite.Raw
{
	class CachedNode
	{
		public CachedNode(Node node)
		{
			Node = node;
		}

		public Node Node { get; set; }
		public List<Way> Ways { get; set; }
	}

	public class SQLiteSimpleSource : IDataSourceReadOnly, IDisposable
	{
		private readonly string _connection_string;
		private Guid _id;
		private const int max_cache_nodes = 1000;
		private readonly Dictionary<long, CachedNode> cache_nodes = new Dictionary<long, CachedNode>(max_cache_nodes);

		public SQLiteSimpleSource(string connection_string)
		{
			_connection_string = connection_string;
			_id = Guid.NewGuid();
		}

		private SQLiteConnection _connection;

		private SQLiteConnection CreateConnection()
		{
			if (_connection == null)
			{
				_connection = new SQLiteConnection(_connection_string);
				_connection.Open();
			}
			return _connection;
		}

		#region IDataSourceReadOnly Members

		public GeoCoordinateBox BoundingBox
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public string Name
		{
			get
			{
				return "SQLite API Data Source";
			}
		}

		public Guid Id
		{
			get
			{
				return _id;
			}
		}

		public bool HasBoundinBox
		{
			get
			{
				return false;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public Node GetNode(long id)
		{
			IList<Node> nodes = this.GetNodes(new List<long>(new long[] { id }));
			if (nodes.Count > 0)
			{
				return nodes[0];
			}
			return null;
		}

		public IList<Node> GetNodes(IList<long> ids)
		{
			IList<Node> return_list = new List<Node>();
			if (ids.Count > 0)
			{
				// initialize connection.
				SQLiteConnection con = CreateConnection();
				// STEP 1: query nodes table.
				//id	latitude	longitude	changeset_id	visible	timestamp	tile	version

				Dictionary<long, Node> nodes = GetCachedNodes(ids);
				if (nodes.Count > 0)
					if (nodes.Count < ids.Count)
						ids = new List<long>(ids.Where(x => !cache_nodes.ContainsKey(x)));
					else
						return nodes.Values.ToList();

				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, ids.Count);
					string ids_string = ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						string sql = "SELECT node.id, node.latitude, node.longitude, node.changeset_id, node.timestamp, node.version, node_tags.key, node_tags.value " +
												 "FROM node " +
												 "LEFT JOIN node_tags ON node_tags.node_id = node.id " +
												 "WHERE (node.id IN ({0})) ";
						sql = string.Format(sql, ids_string);

						using (SQLiteCommand com = new SQLiteCommand(sql))
						{
							com.Connection = con;
							using (SQLiteDataReader reader = ExecuteReader(com))
							{
								while (reader.Read())
								{
									// load/parse data.
									long returned_id = reader.GetInt64(0);

									Node node;
									if (!nodes.TryGetValue(returned_id, out node))
									{
										// create node.
										node = OsmBaseFactory.CreateNode(returned_id);
										int latitude_int = reader.GetInt32(1);
										int longitude_int = reader.GetInt32(2);
										node.ChangeSetId = reader.GetInt64(3);
										node.TimeStamp = reader.GetDateTime(4);
										node.Version = reader.GetInt64(5);
										node.Coordinate = new GeoCoordinate(latitude_int / 10000000.0, longitude_int / 10000000.0);
										nodes.Add(node.Id, node);

										AddCachedNode(node);
									}

									//Tags
									if (!reader.IsDBNull(6))
									{
										string key = reader.GetString(6);
										if (!node.Tags.ContainsKey(key))
											node.Tags.Add(key, reader.IsDBNull(7) ? string.Empty : reader.GetString(7));
									}

								}
								reader.Close();
							}
						}
					}
				}

				// STEP2: Load all node tags.
				//this.LoadNodeTags(nodes);

				return_list = nodes.Values.ToList();
			}
			return return_list;
		}

		private CachedNode AddCachedNode(Node node)
		{
			CachedNode cachedNode;
			if (cache_nodes.TryGetValue(node.Id, out cachedNode))
				return cachedNode; //exists

			if (cache_nodes.Count > max_cache_nodes)
				cache_nodes.Remove(cache_nodes.First().Key);

			cachedNode = new CachedNode(node);
			cache_nodes.Add(node.Id, cachedNode);
			return cachedNode;
		}

		private void AddCachedWay(Node node, Way way)
		{
			CachedNode cached;
			if (!cache_nodes.TryGetValue(node.Id, out cached))
			{
				AddCachedNode(node);
				return;
			}
			if (cached.Ways == null)
				cached.Ways = new List<Way>();
			cached.Ways.Add(way);
		}

		private Dictionary<long, Node> GetCachedNodes(IList<long> ids)
		{
			return cache_nodes.Where(x => ids.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value.Node);
		}


		private Dictionary<long, Way> GetCachedWays(Dictionary<long, Node> nodes)
		{
			return cache_nodes.Where(n => nodes.ContainsKey(n.Key) && n.Value.Ways != null && n.Value.Ways.Count > 0).SelectMany(cn => cn.Value.Ways).Distinct().ToDictionary(way => way.Id, way => way);
		}

		public Relation GetRelation(long id)
		{
			// TODO: implement this
			return null;
		}

		public IList<Relation> GetRelations(IList<long> ids)
		{
			// TODO: implement this
			return new List<Relation>();
		}

		public IList<Relation> GetRelationsFor(Osm.Core.OsmBase obj)
		{
			// TODO: implement this
			return new List<Relation>();
		}

		public Way GetWay(long id)
		{
			IList<Way> ways = this.GetWays(new List<long>(new long[] { id }));
			if (ways.Count > 0)
			{
				return ways[0];
			}
			return null;
		}

		public IList<Way> GetWays(IList<long> ids)
		{
			return this.GetWays(ids, null);
		}

		private IList<Way> GetWays(IList<long> ids, Dictionary<long, Node> nodes)
		{
			if (ids.Count > 0)
			{
				SQLiteConnection con = this.CreateConnection();

				// STEP2: Load ways.
				Dictionary<long, Way> ways = new Dictionary<long, Way>();
				string sql;
				SQLiteCommand com;
				SQLiteDataReader reader;
				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, ids.Count);

					sql = "SELECT * FROM way WHERE (id IN ({0})) ";
					string ids_string = this.ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						sql = string.Format(sql, ids_string);
						com = new SQLiteCommand(sql);
						com.Connection = con;
						reader = ExecuteReader(com);
						Way way;
						while (reader.Read())
						{
							long id = reader.GetInt64(0);
							long changeset_id = reader.GetInt64(1);
							bool visible = reader.GetInt64(2) == 1;
							DateTime timestamp = reader.GetDateTime(3);
							long version = reader.GetInt64(4);

							// create way.
							way = OsmBaseFactory.CreateWay(id);
							way.Version = version;
							//node.UserId = user_id;
							way.TimeStamp = timestamp;
							way.ChangeSetId = changeset_id;

							ways.Add(way.Id, way);
						}
						reader.Close();
					}
				}

				//STEP3: Load all node-way relations
				List<long> missing_node_ids = new List<long>();
				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, ids.Count);

					sql = "SELECT * FROM way_nodes WHERE (way_id IN ({0})) ORDER BY sequence_id";
					string ids_string = this.ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						sql = string.Format(sql, ids_string);
						com = new SQLiteCommand(sql);
						com.Connection = con;
						reader = ExecuteReader(com);
						while (reader.Read())
						{
							long id = reader.GetInt64(0);
							long node_id = reader.GetInt64(1);
							long sequence_id = reader.GetInt64(2);

							if (nodes == null || !nodes.ContainsKey(node_id))
							{
								missing_node_ids.Add(node_id);
							}
						}
						reader.Close();
					}
				}

				//STEP4: Load all missing nodes.
				IList<Node> missing_nodes = this.GetNodes(missing_node_ids);
				Dictionary<long, Node> way_nodes = new Dictionary<long, Node>();
				if (nodes != null)
				{
					way_nodes = new Dictionary<long, Node>(nodes);
				}
				foreach (Node node in missing_nodes)
				{
					way_nodes.Add(node.Id, node);
				}

				//STEP5: assign nodes to way.
				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, ids.Count);

					sql = "SELECT * FROM way_nodes WHERE (way_id IN ({0})) ORDER BY sequence_id";
					string ids_string = this.ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						sql = string.Format(sql, ids_string);
						com = new SQLiteCommand(sql);
						com.Connection = con;
						reader = ExecuteReader(com);
						while (reader.Read())
						{
							long id = reader.GetInt64(0);
							long node_id = reader.GetInt64(1);
							long sequence_id = reader.GetInt64(2);

							Node way_node;
							if (way_nodes.TryGetValue(node_id, out way_node))
							{
								Way way;
								if (ways.TryGetValue(id, out way))
								{
									way.Nodes.Add(way_node);
								}
							}
						}
						reader.Close();
					}
				}


				//STEP4: Load all tags.
				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, ids.Count);

					sql = "SELECT * FROM way_tags WHERE (way_id IN ({0})) ";
					string ids_string = this.ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						sql = string.Format(sql, ids_string);
						com = new SQLiteCommand(sql);
						com.Connection = con;
						reader = ExecuteReader(com);
						while (reader.Read())
						{
							long id = reader.GetInt64(0);
							string key = reader.GetString(1);
							object value_object = reader[2];
							string value = string.Empty;
							if (value_object != null && value_object != DBNull.Value)
							{
								value = (string)value_object;
							}

							Way way;
							if (ways.TryGetValue(id, out way))
							{
								way.Tags.Add(key, value);
							}
						}
						reader.Close();
					}
				}

				return ways.Values.ToList<Way>();
			}
			return new List<Way>();
		}

		public IList<Way> GetWaysFor(Node node)
		{
			Dictionary<long, Node> nodes = new Dictionary<long, Node>();
			nodes.Add(node.Id, node);
			return this.GetWaysForNodes(nodes);
		}

		public IList<Way> GetWaysForNodes(Dictionary<long, Node> nodes)
		{

			if (nodes.Count > 0)
			{
				SQLiteConnection con = CreateConnection();
				Dictionary<long, Way> ways = GetCachedWays(nodes);

				List<long> ids = nodes.Keys.ToList();
				if (ways.Count > 0)
				{
					ids = ids.Where(id => !cache_nodes.ContainsKey(id) || cache_nodes[id].Ways == null).ToList();
					if (ids.Count == 0)
						return ways.Values.ToList();
				}

				IList<long> way_ids = new List<long>();
				for (int idx_1000 = 0; idx_1000 <= ids.Count / 1000; idx_1000++)
				{
					// STEP1 & STEP2 & STEP5: Load ways that exist for the given nodes & tags for ways 
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, nodes.Count);

					string ids_string = ConstructIdList(ids, start_idx, stop_idx);
					if (ids_string.Length <= 0)
						continue;

					string sql = "SELECT way.id, way.changeset_id, way.timestamp, way.version, way_tags.key, way_tags.value, way_nodes.node_id " +
											 "FROM way_nodes " +
											 "INNER JOIN way ON way.Id = way_nodes.way_id " +
											 "INNER JOIN way_tags ON way_tags.way_id = way_nodes.way_id " +
											 "WHERE node_id IN ({0})";
					sql = string.Format(sql, ids_string);
					using (SQLiteCommand com = new SQLiteCommand(sql, con))
					{
						using (SQLiteDataReader reader = ExecuteReader(com))
						{
							while (reader.Read())
							{
								long id = reader.GetInt64(0);
								Way way;
								//Create ay if not exists
								if (!ways.TryGetValue(id, out way))
								{
									// create way.
									way = OsmBaseFactory.CreateWay(id);
									way.Version = reader.GetInt64(3);
									way.TimeStamp = reader.GetDateTime(2);
									way.ChangeSetId = reader.GetInt64(1);
									ways.Add(id, way);
									way_ids.Add(id);
								}

								//Tags
								if (!reader.IsDBNull(4))
								{
									string key = reader.GetString(4);
									if (!way.Tags.ContainsKey(key))
									{
										//TODO: es necesario guardar el string.empty?
										string value = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
										way.Tags.Add(key, value);
									}
								}

								AddCachedWay(nodes[reader.GetInt64(6)], way);

								//if (cache_ways.Count > max_cache_ways)
								//cache_ways.Remove(cache_ways.First().Key);
								//cache_ways.Add(id, way);
							}
							reader.Close();
						}
					}
				}

				Dictionary<long, Node> way_nodes = new Dictionary<long, Node>();

				//STEP3 & STEP4: Load all node-way relations & assign nodes
				for (int idx_1000 = 0; idx_1000 <= way_ids.Count / 1000; idx_1000++)
				{
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, way_ids.Count);

					string ids_string = ConstructIdList(way_ids, start_idx, stop_idx);
					if (ids_string.Length > 0)
					{
						string sql = "SELECT way_nodes.way_id, way_nodes.node_id, node.latitude, node.longitude, node.changeset_id, node.timestamp, node.version " +
												 "FROM way_nodes " +
												 "INNER JOIN node ON node.Id = way_nodes.node_id " +
												 "WHERE (way_nodes.way_id IN ({0})) ORDER BY way_nodes.sequence_id";
						sql = string.Format(sql, ids_string);
						using (SQLiteCommand com = new SQLiteCommand(sql, con))
						{
							using (SQLiteDataReader reader = ExecuteReader(com))
							{
								while (reader.Read())
								{
									long node_id = reader.GetInt64(1);
									Node node;
									if (!way_nodes.TryGetValue(node_id, out node))
									{
										// load/parse data.
										int latitude_int = reader.GetInt32(2);
										int longitude_int = reader.GetInt32(3);

										// create node.
										node = OsmBaseFactory.CreateNode(node_id);
										node.Version = reader.GetInt64(6);
										//node.UserId = user_id;
										node.TimeStamp = reader.GetDateTime(5);
										node.ChangeSetId = reader.GetInt64(4);
										node.Coordinate = new GeoCoordinate(latitude_int / 10000000.0, longitude_int / 10000000.0);
									}
									Way way;
									if (ways.TryGetValue(reader.GetInt64(0), out way))
										way.Nodes.Add(node);
									AddCachedNode(node);
								}
								reader.Close();
							}
						}
					}
				}
				return ways.Values.ToList();
			}

			return new List<Way>();
		}

		private static SQLiteDataReader ExecuteReader(SQLiteCommand com)
		{
			return com.ExecuteReader();
			Stopwatch timer = new Stopwatch();
			//timer.Start();
			var re = com.ExecuteReader();
			//timer.Stop();
			//Debug.WriteLine(timer.Elapsed);
			//Debug.WriteLine(com.CommandText);
			return re;
		}

		#region Tile Calculations

		uint xy2tile(uint x, uint y)
		{
			uint tile = 0;
			int i;

			for (i = 15; i >= 0; i--)
			{
				tile = (tile << 1) | ((x >> i) & 1);
				tile = (tile << 1) | ((y >> i) & 1);
			}

			return tile;
		}

		uint lon2x(double lon)
		{
			return (uint)Math.Floor(((lon + 180.0) * 65536.0 / 360.0));
		}

		uint lat2y(double lat)
		{
			return (uint)Math.Floor(((lat + 90.0) * 65536.0 / 180.0));
		}

		#endregion

		public IList<OsmBase> Get(GeoCoordinateBox box, Filter filter)
		{
			// initialize connection.
			SQLiteConnection con = this.CreateConnection();
			List<OsmBase> base_list = new List<OsmBase>();

			// calculate bounding box parameters to query db.
			long latitude_min = (long)(box.MinLat * 10000000.0);
			long longitude_min = (long)(box.MinLon * 10000000.0);
			long latitude_max = (long)(box.MaxLat * 10000000.0);
			long longitude_max = (long)(box.MaxLon * 10000000.0);

			// TODO: improve this to allow loading of bigger bb's.
			uint x_min = lon2x(box.MinLon);
			uint x_max = lon2x(box.MaxLon);
			uint y_min = lat2y(box.MinLat);
			uint y_max = lat2y(box.MaxLat);

			IList<long> boxes = new List<long>();

			for (uint x = x_min; x <= x_max; x++)
			{
				for (uint y = y_min; y <= y_max; y++)
				{
					boxes.Add(this.xy2tile(x, y));
				}
			}

			// STEP 1: query nodes table.
			//id	latitude	longitude	changeset_id	visible	timestamp	tile	version
			string sql
					= "SELECT * FROM node WHERE  (tile IN ({4})) AND (visible = 1) AND (latitude BETWEEN {0} AND {1} AND longitude BETWEEN {2} AND {3})";
			sql = string.Format(sql,
							latitude_min.ToString(),
							latitude_max.ToString(),
							longitude_min.ToString(),
							longitude_max.ToString(),
							this.ConstructIdList(boxes));

			// TODO: parameters.
			SQLiteCommand com = new SQLiteCommand(sql);
			com.Connection = con;
			SQLiteDataReader reader = ExecuteReader(com);
			Node node = null;
			Dictionary<long, Node> nodes = new Dictionary<long, Node>();
			while (reader.Read())
			{
				// load/parse data.
				long returned_id = reader.GetInt64(0);
				int latitude_int = reader.GetInt32(1);
				int longitude_int = reader.GetInt32(2);
				long changeset_id = reader.GetInt64(3);
				//bool visible = reader.GetInt64(4) == 1;
				DateTime timestamp = reader.GetDateTime(5);
				//long tile = reader.GetInt64(6);
				long version = reader.GetInt64(7);

				// create node.
				node = OsmBaseFactory.CreateNode(returned_id);
				node.Version = version;
				//node.UserId = user_id;
				node.TimeStamp = timestamp;
				node.ChangeSetId = changeset_id;
				node.Coordinate = new GeoCoordinate(latitude_int / 10000000.0, longitude_int / 10000000.0);

				nodes.Add(node.Id, node);
			}
			reader.Close();

			// STEP2: Load all node tags.
			this.LoadNodeTags(nodes);

			// STEP3: Load all ways for the given nodes.
			IList<Way> ways = this.GetWaysForNodes(nodes);



			// Add all objects to the base list.
			foreach (Node node_result in nodes.Values.ToList<Node>())
			{
				base_list.Add(node_result);
			}
			foreach (Way way in ways)
			{
				base_list.Add(way);
			}
			return base_list;
		}

		private string ConstructIdList(IList<long> ids)
		{
			return this.ConstructIdList(ids, 0, ids.Count);
		}

		private string ConstructIdList(IList<long> ids, int start_idx, int end_idx)
		{
			string return_string = string.Empty;
			if (ids.Count > 0 && ids.Count > start_idx)
			{
				return_string = return_string + ids[start_idx].ToString();
				for (int i = start_idx + 1; i < end_idx; i++)
				{
					return_string = return_string + "," + ids[i].ToString();
				}
			}
			return return_string;
		}

		private void LoadNodeTags(Dictionary<long, Node> nodes)
		{
			if (nodes.Count > 0)
			{
				for (int idx_1000 = 0; idx_1000 <= nodes.Count / 1000; idx_1000++)
				{
					string sql = "SELECT * FROM node_tags WHERE (node_id IN ({0})) ";
					int start_idx = idx_1000 * 1000;
					int stop_idx = Math.Min((idx_1000 + 1) * 1000, nodes.Count);
					string ids = this.ConstructIdList(nodes.Keys.ToList<long>(), start_idx, stop_idx);
					if (ids.Length > 0)
					{
						sql = string.Format(sql, ids);
						SQLiteConnection con = this.CreateConnection();
						SQLiteCommand com = new SQLiteCommand(sql);
						com.Connection = con;
						SQLiteDataReader reader = ExecuteReader(com);
						while (reader.Read())
						{
							long returned_id = reader.GetInt64(0);
							string key = reader.GetString(1);
							object val = reader.GetValue(2);
							string value = string.Empty;
							if (val is string)
							{
								value = val as string;
							}

							nodes[returned_id].Tags.Add(key, value);

						}
						reader.Close();
					}
				}
			}
		}

		#endregion

		public void Close()
		{
			if (_connection != null)
			{
				_connection.Close();
				_connection = null;
			}
		}

		#region IDisposable Members

		public void Dispose()
		{
			_connection.Close();
			_connection.Dispose();
			_connection = null;
		}

		#endregion
	}
}

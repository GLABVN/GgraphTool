using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;
using Rhino.Geometry;

namespace Glab.C_Graph
{
    static class ToNeo4j
    {
        private static IDriver _driver;

        public static void Initialize(string uri, string user, string password)
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }

        public static async Task<bool> IsServerUp()
        {
            if (_driver == null)
                return false;

            try
            {
                // Create a test session and run a simple query
                using var session = _driver.AsyncSession();
                var result = await session.RunAsync("RETURN 1 as n");
                var record = await result.SingleAsync();

                // If we get here, the server is up
                return record != null && record["n"].As<int>() == 1;
            }
            catch (Exception)
            {
                // Any exception means the server is not reachable or login failed
                return false;
            }
        }

        public static async Task PushGraphToNeo4j(Graph graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            using var session = _driver.AsyncSession();

            try
            {
                // Create nodes
                foreach (var node in graph.QuickGraphObj.Vertices)
                {
                    // Ensure type is never null
                    string nodeType = node.Type ?? "DefaultNode";

                    // Create a parameters dictionary with basic node properties
                    var parameters = new Dictionary<string, object>
                        {
                            { "id", node.Id },
                            { "type", nodeType }
                        };

                    // Add node's geometric properties
                    if (node.Point != Point3d.Unset)
                    {
                        parameters["x"] = node.Point.X;
                        parameters["y"] = node.Point.Y;
                        parameters["z"] = node.Point.Z;
                    }

                    // Add GUID as string
                    parameters["guid"] = node.GGUID.ToString();

                    // Add valence
                    parameters["valence"] = node.Valence;

                    // Add IsNaked
                    parameters["is_naked"] = node.IsNaked;

                    // Add all attributes that can be stored directly in Neo4j
                    foreach (var attr in node.Attributes)
                    {
                        if (CanStoreInNeo4j(attr.Value))
                        {
                            // Convert key to neo4j-friendly format (lowercase with underscores)
                            string key = ConvertToCypherPropertyName(attr.Key);

                            // Skip if we already added this property or it would conflict with reserved properties
                            if (!parameters.ContainsKey(key) && key != "id" && key != "type")
                            {
                                parameters[key] = attr.Value;
                            }
                        }
                    }

                    // Build the SET clause dynamically
                    var setClauses = new List<string>();
                    foreach (var param in parameters)
                    {
                        if (param.Key != "id")  // Skip id as it's used in MERGE
                        {
                            setClauses.Add($"n.{param.Key} = ${param.Key}");
                        }
                    }

                    string setClause = string.Join(", ", setClauses);

                    var createNodeQuery = $@"
                            MERGE (n:GNode {{ id: $id }})
                            SET {setClause}";

                    await session.RunAsync(createNodeQuery, parameters);
                }

                // Create relationships
                foreach (var edge in graph.QuickGraphObj.Edges)
                {
                    // Ensure type is never null - Neo4j won't accept null values for MERGE properties
                    string edgeType = "DefaultEdge";
                    if (!string.IsNullOrEmpty(edge.Type))
                    {
                        edgeType = edge.Type;
                    }

                    // Basic edge properties
                    var parameters = new Dictionary<string, object>
                        {
                            { "sourceId", edge.Source.Id },
                            { "targetId", edge.Target.Id },
                            { "type", edgeType }
                        };

                    // Add edge ID if available
                    if (!string.IsNullOrEmpty(edge.Id))
                    {
                        parameters["id"] = edge.Id;
                    }

                    // Add GUID as string
                    parameters["guid"] = edge.GGUID.ToString();

                    // Add Length
                    parameters["length"] = edge.Length;

                    // Add valence
                    parameters["valence"] = edge.Valence;

                    // Add IsNaked
                    parameters["is_naked"] = edge.IsNaked;

                    // Add all attributes that can be stored directly in Neo4j
                    foreach (var attr in edge.Attributes)
                    {
                        if (CanStoreInNeo4j(attr.Value))
                        {
                            // Convert key to neo4j-friendly format (lowercase with underscores)
                            string key = ConvertToCypherPropertyName(attr.Key);

                            // Skip if we already added this property
                            if (!parameters.ContainsKey(key) && key != "sourceId" && key != "targetId" && key != "type")
                            {
                                parameters[key] = attr.Value;
                            }
                        }
                    }

                    // Build the SET clause dynamically
                    var setClauses = new List<string>();
                    foreach (var param in parameters)
                    {
                        if (param.Key != "sourceId" && param.Key != "targetId" && param.Key != "type")
                        {
                            setClauses.Add($"r.{param.Key} = ${param.Key}");
                        }
                    }

                    string setClause = setClauses.Count > 0 ? "SET " + string.Join(", ", setClauses) : "";

                    var createEdgeQuery = $@"
                            MATCH (a:GNode {{ id: $sourceId }})
                            MATCH (b:GNode {{ id: $targetId }})
                            MERGE (a)-[r:GEdge {{ type: $type }}]->(b)
                            {setClause}";

                    await session.RunAsync(createEdgeQuery, parameters);
                }
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Determines if a value can be stored directly in Neo4j
        /// </summary>
        private static bool CanStoreInNeo4j(object value)
        {
            if (value == null)
                return false;

            Type type = value.GetType();

            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(bool) ||
                   type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(double) ||
                   type == typeof(float) ||
                   type == typeof(DateTime) ||
                   type == typeof(decimal);
        }

        /// <summary>
        /// Converts a property name to a Neo4j-friendly format
        /// </summary>
        private static string ConvertToCypherPropertyName(string propertyName)
        {
            // Convert camelCase or PascalCase to snake_case for Neo4j properties
            if (string.IsNullOrEmpty(propertyName))
                return propertyName;

            StringBuilder result = new StringBuilder();
            result.Append(char.ToLower(propertyName[0]));

            for (int i = 1; i < propertyName.Length; i++)
            {
                if (char.IsUpper(propertyName[i]))
                {
                    result.Append('_');
                    result.Append(char.ToLower(propertyName[i]));
                }
                else
                {
                    result.Append(propertyName[i]);
                }
            }

            return result.ToString();
        }

        public static async Task<bool> CleanAllGraphData()
        {
            if (_driver == null)
                throw new InvalidOperationException("Neo4j driver not initialized. Call Initialize method first.");

            using var session = _driver.AsyncSession();
            try
            {
                // Delete all relationships first
                var deleteRelationshipsQuery = "MATCH ()-[r:GEdge]->() DELETE r";
                await session.RunAsync(deleteRelationshipsQuery);

                // Delete all nodes
                var deleteNodesQuery = "MATCH (n:GNode) DELETE n";
                await session.RunAsync(deleteNodesQuery);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public static async Task Close()
        {
            if (_driver != null)
            {
                await _driver.DisposeAsync();
                _driver = null;
            }
        }
    }
}

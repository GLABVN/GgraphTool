using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino;
using Glab.Utilities;
using QuikGraph;
using QuikGraph.Algorithms.ConnectedComponents;
using Newtonsoft.Json;
using QuikGraph.Algorithms.ShortestPath;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Observers;

namespace Glab.C_Graph
{
    public static class GraphUtils
    {
        // Static method to create graphs from nodes and edges
        public static List<Graph> CreateGraphsFromNodesAndEdges(
            IEnumerable<GNode> nodes,
            IEnumerable<GEdge> edges,
            out List<GNode> isolatedNodes,
            out List<GEdge> isolatedEdges,
            bool divideEdge = true)
        {
            // Create initial graph and get its isolated elements
            var initialGraph = CreateGraphFromNodesAndEdges(nodes, edges, out isolatedNodes, out isolatedEdges, divideEdge);

            // If the graph is fully connected, return it as a single item list
            if (initialGraph.IsGraphFullyConnected)
            {
                return new List<Graph> { initialGraph };
            }

            // If not fully connected, return the subgraphs
            return SplitGraphIntoSubgraphs(initialGraph);
        }

        // Static method to create a graph from nodes and edges
        public static Graph CreateGraphFromNodesAndEdges(
            IEnumerable<GNode> nodes,
            IEnumerable<GEdge> edges,
            out List<GNode> isolatedNodes,
            out List<GEdge> isolatedEdges,
            bool divideEdge = true)
        {
            var graph = new Graph();
            isolatedNodes = new List<GNode>();
            isolatedEdges = new List<GEdge>();

            // Handle the case where divideEdge is true
            if (divideEdge)
            {
                // Extract curves and points from nodes and edges
                List<Curve> edgeCurves = edges.Select(edge => edge.EdgeCurve).ToList();
                List<Point3d> points = nodes?.Where(node => node != null).Select(node => node.Point).ToList() ?? new List<Point3d>();

                // Shatter curves at intersections and points
                var (shatteredCurves, originalEdgeIndices) =
                    CurveUtils.ShatterLineLikeCurvesAtIntersectionsAndPoints(edgeCurves, points);

                // Create a dictionary to map points to deep copied nodes with explicit deep copy of attributes
                var nodeDict = new Dictionary<Point3d, GNode>();

                // For divideEdge=true case
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        if (node == null) continue;

                        var point = node.Point;
                        if (!nodeDict.Values.Any(n => PointUtils.ArePointsEqual(n.Point, point)))
                        {
                            // Replace manual copy with DeepCopy
                            nodeDict[point] = node.DeepCopy();
                        }
                    }
                }

                // Initialize a list to store valid edges
                var validEdges = new List<GEdge>();

                // Initialize a set to track nodes that are part of any edge
                var nodesInEdges = new HashSet<Point3d>();

                // Iterate through shattered curves and construct edges
                for (int i = 0; i < shatteredCurves.Count; i++)
                {
                    var shatteredCurve = shatteredCurves[i];
                    int originalEdgeIndex = originalEdgeIndices[i];
                    var originalEdge = edges.ElementAt(originalEdgeIndex);

                    Point3d startPoint = shatteredCurve.PointAtStart;
                    Point3d endPoint = shatteredCurve.PointAtEnd;

                    // Replace the edge's source and target nodes with the new nodes if found
                    GNode newSourceNode = nodeDict.ContainsKey(startPoint) ? nodeDict[startPoint] : new GNode(startPoint);
                    GNode newTargetNode = nodeDict.ContainsKey(endPoint) ? nodeDict[endPoint] : new GNode(endPoint);

                    // Add the new nodes to the node dictionary if they are not already present
                    if (!nodeDict.Values.Any(n => PointUtils.ArePointsEqual(n.Point, startPoint)))
                    {
                        nodeDict[startPoint] = newSourceNode;
                    }
                    if (!nodeDict.Values.Any(n => PointUtils.ArePointsEqual(n.Point, endPoint)))
                    {
                        nodeDict[endPoint] = newTargetNode;
                    }

                    // Copy attributes from the original nodes to the new nodes
                    if (PointUtils.ArePointsEqual(startPoint, originalEdge.Source.Point))
                    {
                        newSourceNode.Attributes = new Dictionary<string, object>(originalEdge.Source.Attributes.Select(kvp =>
                            new KeyValuePair<string, object>(
                                kvp.Key,
                                kvp.Value is ICloneable cloneable ? cloneable.Clone() : kvp.Value
                            )
                        ));
                        // Copy the node's type
                        newSourceNode.Type = originalEdge.Source.Type;
                    }
                    if (PointUtils.ArePointsEqual(endPoint, originalEdge.Target.Point))
                    {
                        newTargetNode.Attributes = new Dictionary<string, object>(originalEdge.Target.Attributes.Select(kvp =>
                            new KeyValuePair<string, object>(
                                kvp.Key,
                                kvp.Value is ICloneable cloneable ? cloneable.Clone() : kvp.Value
                            )
                        ));
                        // Copy the node's type
                        newTargetNode.Type = originalEdge.Target.Type;
                    }

                    // Create a new edge by deep copying the original and updating relevant properties
                    GEdge newEdge = originalEdge.DeepCopy();
                    // Update source and target nodes
                    newEdge.Source = newSourceNode;
                    newEdge.Target = newTargetNode;
                    // Update the curve to the shattered one
                    newEdge.EdgeCurve = shatteredCurve;

                    validEdges.Add(newEdge);
                    nodesInEdges.Add(newSourceNode.Point);
                    nodesInEdges.Add(newTargetNode.Point);
                }

                // Add nodes to the graph
                foreach (var node in nodeDict.Values)
                {
                    graph.AddNode(node);
                }

                // Add edges to the graph
                foreach (var edge in validEdges)
                {
                    graph.AddEdge(edge);
                }
            }
            // Handle the case where divideEdge is false
            else
            {
                // Deep copy edges and nodes
                var deepCopiedEdges = edges.Select(edge => edge.DeepCopy()).ToList();
                var nodeDict = new Dictionary<Point3d, GNode>();

                // Create nodes dictionary if nodes are provided
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    { 
                        if (node == null)
                            continue;
                        var nodePoint = node.Point;
                        nodeDict[nodePoint] = node.DeepCopy();
                    }
                }

                // Add nodes from edges if they don't exist in nodeDict
                foreach (var edge in deepCopiedEdges)
                {
                    if (edge == null)
                        continue;
                    var sourcePoint = edge.Source.Point;
                    var targetPoint = edge.Target.Point;

                    if (!nodeDict.ContainsKey(sourcePoint))
                    {
                        // Create a deep copy of the source node from the edge
                        var sourceNodeCopy = edge.Source.DeepCopy();
                        // Update the point in case it was rounded/changed
                        sourceNodeCopy.Point = sourcePoint;
                        nodeDict[sourcePoint] = sourceNodeCopy;
                    }

                    if (!nodeDict.ContainsKey(targetPoint))
                    {
                        // Create a deep copy of the target node from the edge
                        var targetNodeCopy = edge.Target.DeepCopy();
                        // Update the point in case it was rounded/changed
                        targetNodeCopy.Point = targetPoint;
                        nodeDict[targetPoint] = targetNodeCopy;
                    }

                    // Update edge to use the nodes from nodeDict
                    edge.Source = nodeDict[sourcePoint];
                    edge.Target = nodeDict[targetPoint];
                }

                // Add all nodes to the graph
                foreach (var node in nodeDict.Values)
                {
                    graph.AddNode(node);
                }

                // Add all edges to the graph
                foreach (var edge in deepCopiedEdges)
                {
                    graph.AddEdge(edge);
                }
            }

            // Extract nodes and edges from the graph
            var allNodes = graph.QuickGraphObj.Vertices.ToList();
            var allEdges = graph.QuickGraphObj.Edges.ToList();

            // Find and handle initially isolated nodes
            foreach (var node in allNodes)
            {
                if (node.Valence == 0)
                {
                    isolatedNodes.Add(node);
                    graph.RemoveNode(node);
                }
            }

            // Find and handle isolated edges
            foreach (var edge in allEdges)
            {
                if (edge.Valence == 0)
                {
                    // Add edge to isolated list
                    isolatedEdges.Add(edge);

                    // Get nodes before removing edge
                    var sourceNode = edge.Source;
                    var targetNode = edge.Target;

                    // Remove the edge
                    graph.RemoveEdge(edge);

                    // Check if source node becomes isolated after edge removal
                    if (graph.QuickGraphObj.AdjacentEdges(sourceNode).Count() == 0)
                    {
                        //isolatedNodes.Add(sourceNode);
                        graph.RemoveNode(sourceNode);
                    }

                    // Check if target node becomes isolated after edge removal
                    if (graph.QuickGraphObj.AdjacentEdges(targetNode).Count() == 0)
                    {
                        //isolatedNodes.Add(targetNode);
                        graph.RemoveNode(targetNode);
                    }
                }
            }

            // Set node IDs
            graph.SetNodeIds();

            return graph;
        }


        public static Graph CombineGraphs(
            List<Graph> graphs,
            out List<GNode> isolatedNodes,
            out List<GEdge> isolatedEdges,
            IEnumerable<GNode> additionalNodes = null,
            IEnumerable<GEdge> additionalEdges = null)
        {
            if (graphs == null || !graphs.Any())
            {
                throw new ArgumentNullException(nameof(graphs), "CombineGraphs: The provided list of graphs is null or empty.");
            }

            var nodeDict = new Dictionary<Point3d, GNode>(new Point3dComparer());
            var edgeDict = new Dictionary<(Point3d, Point3d), GEdge>(new EdgeTupleComparer());

            // Process graphs
            foreach (var graph in graphs)
            {
                // Create deep copy of the graph first
                var graphCopy = graph.DeepCopy();

                foreach (var node in graphCopy.QuickGraphObj.Vertices)
                {
                    Point3d roundedPoint = PointUtils.RoundPoint(node.Point);
                    if (nodeDict.TryGetValue(roundedPoint, out GNode existingNode))
                    {
                        // Deep merge attributes
                        foreach (var kvp in node.Attributes)
                        {
                            existingNode.Attributes[kvp.Key] = kvp.Value is ICloneable cloneable ?
                                cloneable.Clone() : kvp.Value;
                        }

                        // Merge types
                        if (existingNode.Type == null)
                        {
                            existingNode.Type = node.Type;
                        }
                        else if (node.Type != null && existingNode.Type != node.Type)
                        {
                            existingNode.Type = $"{existingNode.Type}-{node.Type}";
                        }
                    }
                    else
                    {
                        // Create a deep copy of the node
                        var nodeCopy = node.DeepCopy();
                        // Set the point to the rounded point in case it was adjusted
                        nodeCopy.Point = roundedPoint;
                        nodeDict[roundedPoint] = nodeCopy;
                    }
                }

                // Handle edges with deep copy of attributes
                foreach (var edge in graphCopy.QuickGraphObj.Edges)
                {
                    Point3d sourcePoint = PointUtils.RoundPoint(edge.Source.Point);
                    Point3d targetPoint = PointUtils.RoundPoint(edge.Target.Point);

                    var edgeKey = (sourcePoint, targetPoint);
                    if (edgeDict.TryGetValue(edgeKey, out GEdge existingEdge))
                    {
                        // Deep merge attributes
                        foreach (var kvp in edge.Attributes)
                        {
                            existingEdge.Attributes[kvp.Key] = kvp.Value is ICloneable cloneable ?
                                cloneable.Clone() : kvp.Value;
                        }

                        // Merge types
                        if (existingEdge.Type == null)
                        {
                            existingEdge.Type = edge.Type;
                        }
                        else if (edge.Type != null && existingEdge.Type != edge.Type)
                        {
                            existingEdge.Type = $"{existingEdge.Type}-{edge.Type}";
                        }
                    }
                    else
                    {
                        // Create a deep copy of the edge
                        var newEdge = edge.DeepCopy();
                        // Update the source and target nodes to use the ones from nodeDict
                        newEdge.Source = nodeDict[sourcePoint];
                        newEdge.Target = nodeDict[targetPoint];
                        // Add to the edge dictionary
                        edgeDict[edgeKey] = newEdge;
                    }
                }
            }

            // Add additional nodes directly to the node dictionary
            if (additionalNodes != null)
            {
                foreach (var node in additionalNodes)
                {
                    if (node == null) continue;
                    Point3d roundedPoint = PointUtils.RoundPoint(node.Point);
                    if (!nodeDict.ContainsKey(roundedPoint))
                    {
                        nodeDict[roundedPoint] = node.DeepCopy();
                    }
                }
            }

            // Add additional edges directly to the edge dictionary
            if (additionalEdges != null)
            {
                foreach (var edge in additionalEdges)
                {
                    Point3d sourcePoint = PointUtils.RoundPoint(edge.Source.Point);
                    Point3d targetPoint = PointUtils.RoundPoint(edge.Target.Point);

                    var edgeKey = (sourcePoint, targetPoint);
                    if (!edgeDict.ContainsKey(edgeKey))
                    {
                        var newEdge = edge.DeepCopy();
                        newEdge.Source = nodeDict[sourcePoint];
                        newEdge.Target = nodeDict[targetPoint];
                        edgeDict[edgeKey] = newEdge;
                    }
                }
            }

            // Create the combined graph
            var combinedGraph = CreateGraphFromNodesAndEdges(nodeDict.Values, edgeDict.Values, out isolatedNodes, out isolatedEdges, false);

            return combinedGraph;
        }

        public static Graph PruneGraphByType(Graph graph, string type = "zzz", string JsonString = null, int valence = 1, bool pruneTypeUnsetOnly = true, bool pruneOnce = false)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var workingGraph = graph.DeepCopy();

            // Parse JsonString into dictionary if provided and convert values to string
            Dictionary<string, string> jsonAttributes = null;
            if (!string.IsNullOrEmpty(JsonString))
            {
                try
                {
                    // First deserialize to object dictionary
                    var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonString);
                    // Then convert all values to strings
                    jsonAttributes = rawJson.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty
                    );
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException("Invalid JSON string provided", nameof(JsonString), ex);
                }
            }

            bool nodeRemoved;
            do
            {
                nodeRemoved = false;

                // Find nodes to remove based on conditions
                var nodesToRemove = new List<GNode>();
                foreach (var node in workingGraph.QuickGraphObj.Vertices)
                {
                    // Base condition: check valence
                    bool matchesValence = node.Valence == valence;
                    bool shouldRemove = false;

                    // If pruneTypeNull is true, skip nodes with type != null
                    if (pruneTypeUnsetOnly && node.Type != "unset")
                    {
                        continue;
                    }

                    // If no additional conditions are provided, use only valence
                    if (type == "unset" && jsonAttributes == null)
                    {
                        shouldRemove = matchesValence;
                    }
                    // Case 1: Both type and JsonString provided (OR gate for exclusion)
                    else if (type != "unset" && jsonAttributes != null)
                    {
                        bool matchesType = node.Type == type;
                        bool matchesAttributes = true;

                        foreach (var attr in jsonAttributes)
                        {
                            if (!node.Attributes.ContainsKey(attr.Key) ||
                                node.Attributes[attr.Key]?.ToString() != attr.Value)
                            {
                                matchesAttributes = false;
                                break;
                            }
                        }

                        shouldRemove = matchesValence && (!matchesType || !matchesAttributes);
                    }
                    // Case 2: Only type provided
                    else if (type != "unset")
                    {
                        shouldRemove = matchesValence && node.Type != type;
                    }
                    // Case 3: Only JsonString provided
                    else if (jsonAttributes != null)
                    {
                        bool matchesAttributes = true;
                        foreach (var attr in jsonAttributes)
                        {
                            if (!node.Attributes.ContainsKey(attr.Key) ||
                                node.Attributes[attr.Key]?.ToString() != attr.Value)
                            {
                                matchesAttributes = false;
                                break;
                            }
                        }

                        shouldRemove = matchesValence && !matchesAttributes;
                    }

                    if (shouldRemove)
                    {
                        nodesToRemove.Add(node);
                    }
                }

                // Remove nodes and their connected edges
                foreach (var node in nodesToRemove)
                {
                    var edgesToRemove = workingGraph.QuickGraphObj.AdjacentEdges(node).ToList();
                    foreach (var edge in edgesToRemove)
                    {
                        workingGraph.QuickGraphObj.RemoveEdge(edge);
                    }
                    workingGraph.QuickGraphObj.RemoveVertex(node);
                    nodeRemoved = true;
                }

                // If pruneOnce is true, break the loop after the first iteration
                if (pruneOnce)
                {
                    break;
                }

            } while (nodeRemoved && workingGraph.QuickGraphObj.VertexCount > 3 && workingGraph.QuickGraphObj.Vertices.Any(n => n.Valence == 1));

            return CreateGraphFromNodesAndEdges(
                workingGraph.QuickGraphObj.Vertices,
                workingGraph.QuickGraphObj.Edges,
                out _, out _,
                false
            );
        }


        public static List<Graph> SplitGraphAtPoints(Graph graph, List<Point3d> points)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (points == null || !points.Any())
                throw new ArgumentNullException(nameof(points));

            // Create a copy of the graph to work on
            var workingGraph = graph.DeepCopy();
            if (workingGraph.QuickGraphObj == null)
                throw new InvalidOperationException("QuickGraphObj is null");

            // Find nodes at split points
            var splitNodes = new List<GNode>();
            foreach (var point in points)
            {
                var node = FindEditNode(workingGraph, point);
                if (node != null)
                    splitNodes.Add(node);
            }

            // Temporarily remove edges connected to split nodes
            var edgesToRemove = new List<GEdge>();
            foreach (var node in splitNodes)
            {
                edgesToRemove.AddRange(workingGraph.QuickGraphObj.AdjacentEdges(node));
            }
            foreach (var edge in edgesToRemove)
            {
                workingGraph.RemoveEdge(edge);
            }

            // Convert to bidirectional for component analysis
            var bidirectionalGraph = ConvertToBidirectionalGraphIfUndirected(workingGraph);

            // Find connected components
            var connectedComponents = new WeaklyConnectedComponentsAlgorithm<GNode, GEdge>(bidirectionalGraph);
            connectedComponents.Compute();

            // Create subgraphs from components
            var componentNodes = new Dictionary<int, List<GNode>>();
            foreach (var kvp in connectedComponents.Components)
            {
                if (!componentNodes.ContainsKey(kvp.Value))
                    componentNodes[kvp.Value] = new List<GNode>();
                componentNodes[kvp.Value].Add(kvp.Key);
            }

            var subgraphs = new List<Graph>();
            foreach (var nodesInComponent in componentNodes.Values)
            {
                // Skip components with only one node
                if (nodesInComponent.Count == 1)
                    continue;

                var nodes = new List<GNode>();
                var edges = new List<GEdge>();
                var processedNodes = new Dictionary<Point3d, GNode>(new Point3dComparer());

                // Process component nodes first
                foreach (var node in nodesInComponent)
                {
                    // Use DeepCopy for cleaner, more robust node copying
                    var newNode = node.DeepCopy();
                    nodes.Add(newNode);
                    processedNodes[node.Point] = newNode;
                }


                // Process edges and create split nodes as needed
                foreach (var node in nodesInComponent)
                {
                    var processedEdgeKeys = new HashSet<(Point3d, Point3d)>(new EdgeTupleComparer());
                    var edgeComparer = new EdgeComparer();

                    foreach (var edge in workingGraph.QuickGraphObj.AdjacentEdges(node))
                    {
                        var sourcePoint = edge.Source.Point;
                        var targetPoint = edge.Target.Point;
                        var edgeKey = (sourcePoint, targetPoint);

                        // Skip if we've already processed this edge or its reverse
                        if (processedEdgeKeys.Contains(edgeKey))
                            continue;

                        var sourceNode = edge.Source;
                        var targetNode = edge.Target;
                        GNode newSource, newTarget;

                        // Handle source node
                        if (splitNodes.Contains(sourceNode))
                        {
                            if (!processedNodes.TryGetValue(sourceNode.Point, out newSource))
                            {
                                newSource = sourceNode.DeepCopy();
                                nodes.Add(newSource);
                                processedNodes[sourceNode.Point] = newSource;
                            }
                        }
                        else
                        {
                            newSource = processedNodes[sourceNode.Point];
                        }

                        // Handle target node
                        if (splitNodes.Contains(targetNode))
                        {
                            if (!processedNodes.TryGetValue(targetNode.Point, out newTarget))
                            {
                                newTarget = targetNode.DeepCopy();
                                nodes.Add(newTarget);
                                processedNodes[targetNode.Point] = newTarget;
                            }
                        }
                        else
                        {
                            newTarget = processedNodes[targetNode.Point];
                        }

                        // Create new edge
                        if (newSource != null && newTarget != null)
                        {
                            // Use DeepCopy instead of manual creation
                            var newEdge = edge.DeepCopy();
                            // Update source and target nodes
                            newEdge.Source = newSource;
                            newEdge.Target = newTarget;

                            // Check for duplicate edges before adding using EdgeComparer
                            if (!edges.Any(e => edgeComparer.Equals(e, newEdge)))
                            {
                                edges.Add(newEdge);
                            }

                            // Mark this edge as processed
                            processedEdgeKeys.Add(edgeKey);
                            processedEdgeKeys.Add((targetPoint, sourcePoint)); // Also mark reverse edge
                        }
                    }
                }

                // Ensure split nodes are included in each subgraph
                foreach (var splitNode in splitNodes)
                {
                    if (!processedNodes.ContainsKey(splitNode.Point))
                    {
                        var newSplitNode = splitNode.DeepCopy();
                        nodes.Add(newSplitNode);
                        processedNodes[splitNode.Point] = newSplitNode;
                    }
                }

                // Reconnect split nodes in each subgraph
                foreach (var edge in edgesToRemove)
                {
                    if (processedNodes.TryGetValue(edge.Source.Point, out var newSource) &&
                        processedNodes.TryGetValue(edge.Target.Point, out var newTarget))
                    {
                        // Use DeepCopy instead of manual creation
                        var newEdge = edge.DeepCopy();
                        // Update source and target nodes
                        newEdge.Source = newSource;
                        newEdge.Target = newTarget;

                        edges.Add(newEdge);
                    }
                }

                // Create subgraph
                var subgraph = CreateGraphFromNodesAndEdges(nodes, edges, out _, out _, false);

                // Convert subgraph back to undirected
                var undirectedGraph = new UndirectedGraph<GNode, GEdge>();
                foreach (var vertex in subgraph.QuickGraphObj.Vertices)
                    undirectedGraph.AddVertex(vertex);

                foreach (var edge in subgraph.QuickGraphObj.Edges)
                {
                    undirectedGraph.AddEdge(edge);
                }

                subgraph.QuickGraphObj = undirectedGraph;
                subgraphs.Add(subgraph);
            }

            return subgraphs;
        }


        // Static method to find a node by point and optionally update its type and attributes
        public static GNode FindEditNode(Graph graph, Point3d point, string type = null, Dictionary<string, object> attributes = null)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            // Convert vertices to Point3d list
            var points = graph.QuickGraphObj.Vertices.Select(n => n.Point).ToList();

            // Use PointUtils.FindClosestPoint to find the closest point within the maxDistance
            var result = PointUtils.FindClosestPoint(points, point);

            if (result != null)
            {
                // Get the node with the found index
                var node = graph.QuickGraphObj.Vertices.ElementAt(result.Index);

                // Update type and attributes if provided
                if (type != null)
                {
                    node.Type = type;
                }
                if (attributes != null)
                {
                    foreach (var kvp in attributes)
                    {
                        node.Attributes[kvp.Key] = kvp.Value;
                    }
                }
                return node;
            }

            return null;
        }

        // Static method to find an edge by line and optionally update its type and attributes
        public static GEdge FindEditEdge(Graph graph, Line line, string type = null, Dictionary<string, object> attributes = null)
        {
            foreach (GEdge edge in graph.QuickGraphObj.Edges)
            {
                if ((edge.Source.Point.EpsilonEquals(line.From, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) && edge.Target.Point.EpsilonEquals(line.To, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)) ||
                    (edge.Source.Point.EpsilonEquals(line.To, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) && edge.Target.Point.EpsilonEquals(line.From, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)))
                {
                    if (type != null)
                    {
                        edge.Type = type;
                    }
                    if (attributes != null)
                    {
                        foreach (var kvp in attributes)
                        {
                            edge.Attributes[kvp.Key] = kvp.Value;
                        }
                    }
                    return edge;
                }
            }
            return null;
        }

        // Custom comparer for Point3d to use as dictionary keys
        public class Point3dComparer : IEqualityComparer<Point3d>
        {
            public bool Equals(Point3d p1, Point3d p2)
            {
                return p1.EpsilonEquals(p2, RhinoMath.ZeroTolerance);
            }

            public int GetHashCode(Point3d p)
            {
                return p.GetHashCode();
            }
        }

        // Custom comparer for edges to use as dictionary keys
        public class EdgeComparer : IEqualityComparer<GEdge>
        {
            public bool Equals(GEdge e1, GEdge e2)
            {
                return (e1.Source.Point.EpsilonEquals(e2.Source.Point, RhinoMath.ZeroTolerance) && e1.Target.Point.EpsilonEquals(e2.Target.Point, RhinoMath.ZeroTolerance)) ||
                       (e1.Source.Point.EpsilonEquals(e2.Target.Point, RhinoMath.ZeroTolerance) && e1.Target.Point.EpsilonEquals(e2.Source.Point, RhinoMath.ZeroTolerance));
            }

            public int GetHashCode(GEdge edge)
            {
                return edge.Source.Point.GetHashCode() ^ edge.Target.Point.GetHashCode();
            }
        }

        // Custom comparer for edge tuples to use as dictionary keys
        public class EdgeTupleComparer : IEqualityComparer<(Point3d, Point3d)>
        {
            public bool Equals((Point3d, Point3d) e1, (Point3d, Point3d) e2)
            {
                return (e1.Item1.EpsilonEquals(e2.Item1, RhinoMath.ZeroTolerance) && e1.Item2.EpsilonEquals(e2.Item2, RhinoMath.ZeroTolerance)) ||
                       (e1.Item1.EpsilonEquals(e2.Item2, RhinoMath.ZeroTolerance) && e1.Item2.EpsilonEquals(e2.Item1, RhinoMath.ZeroTolerance));
            }

            public int GetHashCode((Point3d, Point3d) edge)
            {
                return edge.Item1.GetHashCode() ^ edge.Item2.GetHashCode();
            }
        }

        public static BidirectionalGraph<GNode, GEdge> ConvertToBidirectionalGraphIfUndirected(Graph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph), "ConvertToBidirectionalGraphIfUndirected: The provided graph is null.");
            }

            var bidirectionalGraph = new BidirectionalGraph<GNode, GEdge>();

            // Add vertices to the bidirectional graph
            foreach (var vertex in graph.QuickGraphObj.Vertices)
            {
                bidirectionalGraph.AddVertex(vertex);
            }

            // Add edges to the bidirectional graph
            foreach (var edge in graph.QuickGraphObj.Edges)
            {
                bidirectionalGraph.AddEdge(edge);

                // Add the reverse edge if it doesn't already exist
                var reverseEdge = edge.DeepCopy();
                // Swap source and target nodes
                reverseEdge.Source = edge.Target;
                reverseEdge.Target = edge.Source;
                // Keep the same GUID as the original edge for bidirectional pairing
                reverseEdge.GGUID = edge.GGUID;

                if (!bidirectionalGraph.ContainsEdge(reverseEdge))
                {
                    bidirectionalGraph.AddEdge(reverseEdge);
                }
            }

            return bidirectionalGraph;
        }
        public static List<Graph> SplitGraphIntoSubgraphs(Graph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph), "SplitGraphIntoSubgraphs: The provided graph is null.");
            }

            // Convert to bidirectional graph if the input graph is undirected
            var convertedGraph = ConvertToBidirectionalGraphIfUndirected(graph);

            var weaklyConnectedComponents = new WeaklyConnectedComponentsAlgorithm<GNode, GEdge>(convertedGraph);
            weaklyConnectedComponents.Compute();

            // Create subgraphs from components
            var componentNodes = new Dictionary<int, List<GNode>>();
            foreach (var kvp in weaklyConnectedComponents.Components)
            {
                if (!componentNodes.ContainsKey(kvp.Value))
                    componentNodes[kvp.Value] = new List<GNode>();
                componentNodes[kvp.Value].Add(kvp.Key);
            }

            var subgraphs = new List<Graph>();
            foreach (var nodesInComponent in componentNodes.Values)
            {
                var nodes = new List<GNode>();
                var edges = new List<GEdge>();
                var processedNodes = new Dictionary<Point3d, GNode>(new Point3dComparer());

                // Process component nodes first
                foreach (var node in nodesInComponent)
                {
                    // Replace with DeepCopy
                    var newNode = node.DeepCopy();
                    nodes.Add(newNode);
                    processedNodes[node.Point] = newNode;
                }

                // Process edges
                foreach (var node in nodesInComponent)
                {
                    foreach (var edge in graph.QuickGraphObj.AdjacentEdges(node))
                    {
                        var sourceNode = edge.Source;
                        var targetNode = edge.Target;
                        GNode newSource, newTarget;

                        // Handle source node
                        if (!processedNodes.TryGetValue(sourceNode.Point, out newSource))
                        {
                            newSource = sourceNode.DeepCopy();
                            nodes.Add(newSource);
                            processedNodes[sourceNode.Point] = newSource;
                        }

                        // Handle target node
                        if (!processedNodes.TryGetValue(targetNode.Point, out newTarget))
                        {
                            newTarget = targetNode.DeepCopy();
                            nodes.Add(newTarget);
                            processedNodes[targetNode.Point] = newTarget;
                        }


                        // Create new edge
                        if (newSource != null && newTarget != null)
                        {
                            // Use DeepCopy instead of manual creation
                            var newEdge = edge.DeepCopy();
                            // Update source and target nodes
                            newEdge.Source = newSource;
                            newEdge.Target = newTarget;

                            edges.Add(newEdge);
                        }
                    }
                }

                // Create subgraph
                var subgraph = CreateGraphFromNodesAndEdges(nodes, edges, out _, out _, false);

                // Convert subgraph back to undirected using our new method
                subgraph.QuickGraphObj = ConvertToUndirectedGraph(subgraph.QuickGraphObj);
                subgraphs.Add(subgraph);
            }

            return subgraphs;
        }


        public static UndirectedGraph<GNode, GEdge> ConvertToUndirectedGraph(IEdgeListGraph<GNode, GEdge> graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph), "ConvertToUndirectedGraph: The provided graph is null.");
            }

            var undirectedGraph = new UndirectedGraph<GNode, GEdge>();
            var processedEdges = new HashSet<(Point3d, Point3d)>(new EdgeTupleComparer());

            // Add vertices to the undirected graph
            foreach (var vertex in graph.Vertices)
            {
                undirectedGraph.AddVertex(vertex);
            }

            // Add edges to the undirected graph, avoiding duplicates
            foreach (var edge in graph.Edges)
            {
                var sourcePoint = edge.Source.Point;
                var targetPoint = edge.Target.Point;
                var edgeKey = (sourcePoint, targetPoint);

                // Only add the edge if we haven't processed it or its reverse yet
                if (!processedEdges.Contains(edgeKey))
                {
                    undirectedGraph.AddEdge(edge);
                    processedEdges.Add(edgeKey);
                }
            }

            return undirectedGraph;
        }

        public static void SimplifyGraphByAngle(Graph graph, double minAngle = 160, double maxAngle = 180)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            // Create a deep copy of the graph to work on
            Graph workingGraph = graph.DeepCopy();

            bool nodeRemoved;
            do
            {
                nodeRemoved = false;
                var nodesToRemove = new List<GNode>();
                var edgesToAdd = new List<GEdge>();

                foreach (var node in workingGraph.QuickGraphObj.Vertices.ToList())
                {
                    if (node.Valence == 2 && node.Angle.HasValue && node.Angle.Value >= minAngle && node.Angle.Value <= maxAngle)
                    {
                        var edges = workingGraph.QuickGraphObj.AdjacentEdges(node).ToList();
                        if (edges.Count == 2)
                        {
                            var edge1 = edges[0];
                            var edge2 = edges[1];

                            var sourceNode = edge1.Source == node ? edge1.Target : edge1.Source;
                            var targetNode = edge2.Source == node ? edge2.Target : edge2.Source;

                            // Create a new edge connecting the source and target nodes
                            var newEdge = new GEdge(sourceNode, targetNode)
                            {
                                EdgeCurve = new LineCurve(sourceNode.Point, targetNode.Point),
                                Type = edge1.Type ?? edge2.Type,
                                Attributes = new Dictionary<string, object>(edge1.Attributes)
                            };

                            // Merge attributes from edge2 into newEdge
                            foreach (var kvp in edge2.Attributes)
                            {
                                if (!newEdge.Attributes.ContainsKey(kvp.Key))
                                {
                                    newEdge.Attributes[kvp.Key] = kvp.Value;
                                }
                                else if (newEdge.Attributes[kvp.Key] is ICloneable cloneable)
                                {
                                    newEdge.Attributes[kvp.Key] = cloneable.Clone();
                                }
                            }

                            edgesToAdd.Add(newEdge);
                            nodesToRemove.Add(node);
                            nodeRemoved = true;
                            break; // Exit the loop to process the changes immediately
                        }
                    }
                }

                // Remove nodes and their connected edges
                foreach (var node in nodesToRemove)
                {
                    var edgesToRemove = workingGraph.QuickGraphObj.AdjacentEdges(node).ToList();
                    foreach (var edge in edgesToRemove)
                    {
                        workingGraph.RemoveEdge(edge);
                    }
                    workingGraph.RemoveNode(node);
                }

                // Add new edges to the graph
                foreach (var edge in edgesToAdd)
                {
                    workingGraph.AddEdge(edge);
                    if (edge.Source.ParentGraph == null)
                    {
                        edge.Source.ParentGraph = workingGraph;
                    }
                    if (edge.Target.ParentGraph == null)
                    {
                        edge.Target.ParentGraph = workingGraph;
                    }
                }

            } while (nodeRemoved);

            // Replace the original graph with the modified graph
            graph.QuickGraphObj = workingGraph.QuickGraphObj;
        }

        public static Graph ConnectNodesToGraph(Graph graph, List<GNode> inputNodes)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (inputNodes == null || !inputNodes.Any())
                throw new ArgumentNullException(nameof(inputNodes));

            // Deep copy the graph
            Graph workingGraph = graph.DeepCopy();

            List<GEdge> allEdge = new();

            allEdge.AddRange(workingGraph.QuickGraphObj.Edges);

            List<GNode> projectedNodes = new();

            // Iterate over the nodes to form the dict to create pointOnNode
            foreach (var inputNode in inputNodes)
            {
                double minDistance = double.MaxValue;
                GEdge closestEdge = null;
                Point3d closestPoint = Point3d.Unset;

                // Check each edge in the graph to find the closest one
                foreach (var edge in workingGraph.QuickGraphObj.Edges)
                {
                    // Get closest point on the edge curve to the input node
                    double t;
                    if (edge.EdgeCurve.ClosestPoint(inputNode.Point, out t))
                    {
                        Point3d pointOnCurve = edge.EdgeCurve.PointAt(t);
                        double distance = inputNode.Point.DistanceTo(pointOnCurve);

                        // Update if this is the closest edge found so far
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestEdge = edge;
                            closestPoint = pointOnCurve;
                        }
                    }
                }

                if (closestEdge != null)
                {
                    // Create a new node at the closest point
                    var nodeOnEdge = new GNode(closestPoint, inputNode.Type);

                    // Find the nearest connected neighbor node to copy attributes
                    GNode nearestNeighbor = FindNearestConnectedNeighbor(workingGraph, nodeOnEdge);
                    if (nearestNeighbor != null)
                    {
                        nodeOnEdge.Attributes = new Dictionary<string, object>(nearestNeighbor.Attributes.Select(kvp =>
                            new KeyValuePair<string, object>(
                                kvp.Key,
                                kvp.Value is ICloneable cloneable ? cloneable.Clone() : kvp.Value
                            )
                        ));
                    }

                    projectedNodes.Add(nodeOnEdge);

                    // Create bridging edge
                    var bridgeEdge = new GEdge(nodeOnEdge, inputNode)
                    {
                        EdgeCurve = new LineCurve(nodeOnEdge.Point, inputNode.Point)
                    };
                    allEdge.Add(bridgeEdge);
                }
            }
            
            var newGraph = CreateGraphFromNodesAndEdges(null, allEdge, out _, out _);

            return newGraph;
        }

        public static GNode FindNearestConnectedNeighbor(Graph graph, GNode node)
        {
            double minDistance = double.MaxValue;
            GNode nearestNeighbor = null;

            foreach (var edge in graph.QuickGraphObj.Edges)
            {
                if (edge.Source != node && edge.Target != node)
                {
                    double distanceToSource = node.Point.DistanceTo(edge.Source.Point);
                    double distanceToTarget = node.Point.DistanceTo(edge.Target.Point);

                    if (distanceToSource < minDistance)
                    {
                        minDistance = distanceToSource;
                        nearestNeighbor = edge.Source;
                    }

                    if (distanceToTarget < minDistance)
                    {
                        minDistance = distanceToTarget;
                        nearestNeighbor = edge.Target;
                    }
                }
            }

            return nearestNeighbor;
        }

        public static GNode FindOrMergeNode(Graph graph, GNode node)
        {
            foreach (var existingNode in graph.QuickGraphObj.Vertices)
            {
                if (PointUtils.ArePointsEqual(existingNode.Point, node.Point))
                {
                    // Merge attributes
                    foreach (var kvp in node.Attributes)
                    {
                        existingNode.Attributes[kvp.Key] = kvp.Value is ICloneable cloneable ? cloneable.Clone() : kvp.Value;
                    }

                    // Merge types
                    if (existingNode.Type == null)
                    {
                        existingNode.Type = node.Type;
                    }
                    else if (node.Type != null && existingNode.Type != node.Type)
                    {
                        existingNode.Type = $"{existingNode.Type}-{node.Type}";
                    }

                    return existingNode;
                }
            }

            // If no overlapping node is found, add the new node
            graph.AddNode(node);
            return node;
        }


        public static Dictionary<Point3d, Graph> FindClosestGraph(List<Graph> graphs, List<Point3d> testPoints, out List<double> minDistances)
        {
            if (graphs == null || !graphs.Any())
            {
                throw new ArgumentNullException(nameof(graphs), "FindClosestGraphs: The provided list of graphs is null or empty.");
            }
            if (testPoints == null || !testPoints.Any())
            {
                throw new ArgumentNullException(nameof(testPoints), "FindClosestGraphs: The provided list of test points is null or empty.");
            }

            // Initialize output lists
            var closestGraphs = new Dictionary<Point3d, Graph>();
            minDistances = new List<double>();

            // Iterate through each test point
            foreach (var testPoint in testPoints)
            {
                double minDistance = double.MaxValue;
                Graph closestGraph = null;

                // Iterate through each graph
                foreach (var graph in graphs)
                {
                    // Iterate through each edge in the graph
                    foreach (var edge in graph.QuickGraphObj.Edges)
                    {
                        // Calculate the distance from the test point to the closest point on the edge
                        double t;
                        edge.EdgeCurve.ClosestPoint(testPoint, out t);
                        Point3d closestPoint = edge.EdgeCurve.PointAt(t);
                        double distance = testPoint.DistanceTo(closestPoint);

                        // Update the minimum distance and closest graph if necessary
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestGraph = graph;
                        }
                    }
                }

                // Add the closest graph and minimum distance to the output lists
                closestGraphs[testPoint] = closestGraph;
                minDistances.Add(minDistance);
            }

            return closestGraphs;
        }

        // Method to find connected edges by input node and map them to their respective graphs
        public static Dictionary<Graph, List<GEdge>> FindConnectedEdges(GNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (node.ParentGraph == null)
                throw new InvalidOperationException("The node does not belong to any graph.");

            var connectedEdgesDict = new Dictionary<Graph, List<GEdge>>();
            var connectedEdges = new List<GEdge>();

            foreach (var edge in node.ParentGraph.QuickGraphObj.AdjacentEdges(node))
            {
                if (edge.Source == node || edge.Target == node)
                {
                    connectedEdges.Add(edge);
                }
            }

            if (connectedEdges.Any())
            {
                connectedEdgesDict[node.ParentGraph] = connectedEdges;
            }

            return connectedEdgesDict;
        }



        public static void FindShortestPath(Graph graph, GNode startNode, GNode endNode, out List<GNode> pathNodes, out List<GEdge> pathEdges, out double pathLength)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (startNode == null)
                throw new ArgumentNullException(nameof(startNode));
            if (endNode == null)
                throw new ArgumentNullException(nameof(endNode));

            // Convert the graph to a bidirectional graph if it is undirected
            var bidirectionalGraph = ConvertToBidirectionalGraphIfUndirected(graph);

            // Create the Dijkstra algorithm
            var dijkstra = new DijkstraShortestPathAlgorithm<GNode, GEdge>(bidirectionalGraph, edge => edge.EdgeCurve.GetLength());

            // Create a dictionary to store the predecessors
            var predecessors = new VertexPredecessorRecorderObserver<GNode, GEdge>();

            // Attach the predecessors recorder to the algorithm
            using (predecessors.Attach(dijkstra))
            {
                // Compute the shortest path
                dijkstra.Compute(startNode);
            }

            // Initialize the output lists
            pathNodes = new List<GNode>();
            pathEdges = new List<GEdge>();
            pathLength = 0.0;

            // Reconstruct the shortest path
            if (!predecessors.TryGetPath(endNode, out var edges))
            {
                return; // No path found
            }

            // Add the nodes and edges to the path
            pathNodes.Add(startNode);
            foreach (var edge in edges)
            {
                pathNodes.Add(edge.Target);
                pathEdges.Add(edge);
                pathLength += edge.EdgeCurve.GetLength();
            }
        }

        public static Graph CollapseShortEdges(Graph graph, double minLength)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (minLength <= 0)
                throw new ArgumentException("minLength must be greater than zero.", nameof(minLength));

            // Create a deep copy of the graph to work on
            Graph workingGraph = graph.DeepCopy();

            int loopCount = 0;
            const int maxLoopCount = 50;

            do
            {
                GEdge shortestEdge = null;
                double shortestLength = double.MaxValue;

                // Find the shortest edge that meets the condition
                foreach (var edge in workingGraph.QuickGraphObj.Edges)
                {
                    double edgeLength = edge.EdgeCurve.GetLength();
                    if (edgeLength < minLength && edgeLength < shortestLength && edge.Source.Valence == 2 && edge.Target.Valence == 2)
                    {
                        shortestEdge = edge;
                        shortestLength = edgeLength;
                    }
                }

                if (shortestEdge != null)
                {
                    var sourceNode = shortestEdge.Source;
                    var targetNode = shortestEdge.Target;

                    var sourceEdges = workingGraph.QuickGraphObj.AdjacentEdges(sourceNode).ToList();
                    var targetEdges = workingGraph.QuickGraphObj.AdjacentEdges(targetNode).ToList();

                    // Find the other edges connected to the source and target nodes
                    var sourceOtherEdge = sourceEdges.First(e => e != shortestEdge);
                    var targetOtherEdge = targetEdges.First(e => e != shortestEdge);

                    var newSourceNode = sourceOtherEdge.Source == sourceNode ? sourceOtherEdge.Target : sourceOtherEdge.Source;
                    var newTargetNode = targetOtherEdge.Source == targetNode ? targetOtherEdge.Target : targetOtherEdge.Source;

                    // Create a new edge connecting the new source and target nodes
                    var newEdge = new GEdge(newSourceNode, newTargetNode)
                    {
                        EdgeCurve = new PolylineCurve(new[] { newSourceNode.Point, sourceNode.Point, targetNode.Point, newTargetNode.Point }),
                        Type = shortestEdge.Type,
                        Attributes = new Dictionary<string, object>(shortestEdge.Attributes)
                    };

                    // Remove the shortest edge and the other edges connected to the source and target nodes
                    workingGraph.RemoveEdge(shortestEdge);
                    workingGraph.RemoveEdge(sourceOtherEdge);
                    workingGraph.RemoveEdge(targetOtherEdge);
                    workingGraph.RemoveNode(sourceNode);
                    workingGraph.RemoveNode(targetNode);

                    // Add the new edge to the graph
                    workingGraph.AddEdge(newEdge);
                    if (newEdge.Source.ParentGraph == null)
                    {
                        newEdge.Source.ParentGraph = workingGraph;
                    }
                    if (newEdge.Target.ParentGraph == null)
                    {
                        newEdge.Target.ParentGraph = workingGraph;
                    }
                }

                loopCount++;

            } while (loopCount < maxLoopCount && workingGraph.QuickGraphObj.Edges.Any(e => e.EdgeCurve.GetLength() < minLength && e.Source.Valence == 2 && e.Target.Valence == 2));

            return workingGraph;
        }

        public static Graph ConnectGraphs(Graph graph1, Graph graph2, List<GNode> nodes1, List<GNode> nodes2, Dictionary<string, object> edgeAttributes = null)
        {
            if (graph1 == null)
                throw new ArgumentNullException(nameof(graph1));
            if (graph2 == null)
                throw new ArgumentNullException(nameof(graph2));
            if (nodes1 == null)
                throw new ArgumentNullException(nameof(nodes1));
            if (nodes2 == null)
                throw new ArgumentNullException(nameof(nodes2));
            if (nodes1.Count != nodes2.Count)
                throw new ArgumentException("The lists of nodes must have the same number of items.");

            // Create a deep copy of both graphs
            var workingGraph1 = graph1.DeepCopy();
            var workingGraph2 = graph2.DeepCopy();

            // Combine the nodes and edges from both graphs
            var combinedNodes = workingGraph1.QuickGraphObj.Vertices.Concat(workingGraph2.QuickGraphObj.Vertices).ToList();
            var combinedEdges = workingGraph1.QuickGraphObj.Edges.Concat(workingGraph2.QuickGraphObj.Edges).ToList();

            // Create new edges connecting the specified nodes
            for (int i = 0; i < nodes1.Count; i++)
            {
                var connectingEdge = new GEdge(nodes1[i], nodes2[i])
                {
                    EdgeCurve = new LineCurve(nodes1[i].Point, nodes2[i].Point),
                    Attributes = edgeAttributes ?? new Dictionary<string, object>()
                };

                // Add the connecting edge to the combined edges list
                combinedEdges.Add(connectingEdge);
            }

            // Create the combined graph with the deep copied elements and the connecting edges
            return CreateGraphFromNodesAndEdges(combinedNodes, combinedEdges, out _, out _, false);
        }

        // Method to find neighboring nodes within a specified distance
        public static Dictionary<Graph, List<GNode>> FindNeighborNodes(GNode node, double maxDistance = 0.0)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (node.ParentGraph == null)
                throw new InvalidOperationException("The node does not belong to any graph.");

            var neighborNodesDict = new Dictionary<Graph, List<GNode>>();
            var neighborNodes = new List<GNode>();

            // Get all nodes in the parent graph
            var allNodes = node.ParentGraph.QuickGraphObj.Vertices;

            foreach (var potentialNeighbor in allNodes)
            {
                // Skip the input node itself
                if (potentialNeighbor == node)
                    continue;

                // Calculate distance between nodes
                double distance = node.Point.DistanceTo(potentialNeighbor.Point);

                // If maxDistance is 0 or not specified, only find directly connected neighbors
                if (maxDistance == 0)
                {
                    // Check if nodes are directly connected by an edge
                    if (node.ParentGraph.QuickGraphObj.AdjacentEdges(node)
                            .Any(edge => edge.Source == potentialNeighbor || edge.Target == potentialNeighbor))
                    {
                        neighborNodes.Add(potentialNeighbor);
                    }
                }
                // Otherwise, find all nodes within the specified distance
                else if (distance <= maxDistance)
                {
                    neighborNodes.Add(potentialNeighbor);
                }
            }

            // Add neighbors to dictionary if any were found
            if (neighborNodes.Any())
            {
                neighborNodesDict[node.ParentGraph] = neighborNodes;
            }

            return neighborNodesDict;
        }

        // Method to extend all naked edges' curves by a specified distance
        public static Graph ExtendNakedEdges(Graph graph, double distance)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            // Create a deep copy of the graph to work on
            Graph workingGraph = graph.DeepCopy();

            foreach (var edge in workingGraph.QuickGraphObj.Edges.Where(e => e.IsNaked))
            {
                if (edge.EdgeCurve is LineCurve lineCurve)
                {
                    Line line = edge.NakedDirectedLine.Line;
                    Vector3d direction = -line.UnitTangent;
                    Point3d newEndPoint = line.From + direction * distance;
                    line.From = newEndPoint;
                    edge.EdgeCurve = new LineCurve(line);

                    // Update the target node's position
                    if (edge.Target.IsNaked)
                    {
                        edge.Target.Point = newEndPoint;
                    }
                    else if (edge.Source.IsNaked)
                    {
                        edge.Source.Point = newEndPoint;
                    }
                }
            }

            return workingGraph;
        }

        public static string ExportGraphsToJSON(List<Graph> graphs, bool includeGeometry = true)
        {
            if (graphs == null || !graphs.Any())
                throw new ArgumentNullException(nameof(graphs), "ExportGraphsToJSON: The provided list of graphs is null or empty.");

            // Create a serializable model for the graphs
            var graphsModel = graphs.Select(graph => new
            {
                Graph = new
                {
                    Properties = graph.PropJSON, // Include graph properties
                    Attributes = graph.Attributes,

                    // Nodes data
                    Nodes = graph.QuickGraphObj.Vertices.Select(node => new
                    {
                        Properties = node.PropJSON, // Include node properties
                        Attributes = node.Attributes,
                        // Include geometry data conditionally
                        Point = includeGeometry ? new
                        {
                            X = node.Point.X,
                            Y = node.Point.Y,
                            Z = node.Point.Z
                        } : null,
                    }).ToList(),

                    // Edges data
                    Edges = graph.QuickGraphObj.Edges.Select(edge => new
                    {
                        Properties = edge.PropJSON, // Include edge properties
                        Attributes = edge.Attributes,
                        // Store source and target node IDs for connectivity
                        SourceNodeID = edge.Source.Id,
                        TargetNodeID = edge.Target.Id,
                        // Include geometry data conditionally
                        Curve = includeGeometry ? SerializeCurve(edge.EdgeCurve) : null,
                    }).ToList()
                }
            }).ToList();

            // Serialize to JSON with formatting
            return JsonConvert.SerializeObject(graphsModel, Formatting.Indented);
        }

        // Method to draw visualization of the graph edges
        public static Arc CreateArcFromEdge(GEdge edge)
        {
            if (edge == null || edge.EdgeCurve == null)
                throw new ArgumentNullException(nameof(edge), "The provided edge or its curve is null.");

            // Get the start and end points of the edge
            Point3d startPoint = edge.Source.Point;
            Point3d endPoint = edge.Target.Point;

            // Calculate the midpoint of the edge
            Point3d midpoint = new Point3d(
                (startPoint.X + endPoint.X) / 2,
                (startPoint.Y + endPoint.Y) / 2,
                (startPoint.Z + endPoint.Z) / 2
            );

            // Calculate the direction vector of the edge
            Vector3d edgeDirection = endPoint - startPoint;

            // Calculate a perpendicular vector to the edge direction
            Vector3d perpendicularDirection = Vector3d.CrossProduct(edgeDirection, Vector3d.ZAxis);
            if (perpendicularDirection.IsZero)
            {
                // If the edge is parallel to the Z-axis, use the X-axis for the perpendicular direction
                perpendicularDirection = Vector3d.CrossProduct(edgeDirection, Vector3d.XAxis);
            }
            perpendicularDirection.Unitize();

            // Calculate the distance to move the midpoint
            double moveDistance = edge.Length / 8.0;

            // Move the midpoint in the perpendicular direction
            Point3d controlPoint = midpoint + (perpendicularDirection * moveDistance);

            // Create the arc using the three points
            Arc arc = new Arc(startPoint, controlPoint, endPoint);

            return arc;
        }



        // Helper method to serialize curve data - only storing lines
        private static object SerializeCurve(Curve curve)
        {
            if (curve == null)
                return null;

            // For any curve type, just extract start and end points to create a line
            Point3d startPoint = curve.PointAtStart;
            Point3d endPoint = curve.PointAtEnd;

            return new
            {
                Type = "LineCurve",
                StartPoint = new { X = startPoint.X, Y = startPoint.Y, Z = startPoint.Z },
                EndPoint = new { X = endPoint.X, Y = endPoint.Y, Z = endPoint.Z }
            };
        }
    }

    // KDTree implementation
    public class KDTree<T>
    {
        private readonly int _dimensions;
        private KDNode _root;

        public KDTree(int dimensions)
        {
            _dimensions = dimensions;
        }

        public void Add(Point3d point, T value)
        {
            _root = Add(_root, point, value, 0);
        }

        private KDNode Add(KDNode node, Point3d point, T value, int depth)
        {
            if (node == null)
                return new KDNode(point, value);

            int axis = depth % _dimensions;
            if (point[axis] < node.Point[axis])
                node.Left = Add(node.Left, point, value, depth + 1);
            else
                node.Right = Add(node.Right, point, value, depth + 1);

            return node;
        }

        public T Nearest(Point3d target, out double minDistance)
        {
            minDistance = double.MaxValue;
            T closestValue = default;
            Nearest(_root, target, 0, ref minDistance, ref closestValue);
            return closestValue;
        }

        private void Nearest(KDNode node, Point3d target, int depth, ref double minDistance, ref T closestValue)
        {
            if (node == null)
                return;

            double distance = target.DistanceTo(node.Point);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestValue = node.Value;
            }

            int axis = depth % _dimensions;
            KDNode nextNode = target[axis] < node.Point[axis] ? node.Left : node.Right;
            KDNode otherNode = target[axis] < node.Point[axis] ? node.Right : node.Left;

            Nearest(nextNode, target, depth + 1, ref minDistance, ref closestValue);

            if (Math.Abs(target[axis] - node.Point[axis]) < minDistance)
                Nearest(otherNode, target, depth + 1, ref minDistance, ref closestValue);
        }

        private class KDNode
        {
            public Point3d Point { get; }
            public T Value { get; }
            public KDNode Left { get; set; }
            public KDNode Right { get; set; }

            public KDNode(Point3d point, T value)
            {
                Point = point;
                Value = value;
            }
        }
    }

}

using Grasshopper.Kernel.Geometry.Delaunay;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using QuikGraph;
using Glab.Utilities;
using Newtonsoft.Json;
using QuikGraph.Algorithms.ConnectedComponents;

namespace Glab.C_Graph
{
    public class Graph : IConnectable
    {
        public UndirectedGraph<GNode, GEdge> QuickGraphObj { get; set; } = new UndirectedGraph<GNode, GEdge>();

        private Dictionary<string, object> _Attributes = new();
        public Dictionary<string, object> Attributes
        {
            get
            {
                return _Attributes;
            }
            set
            {
                foreach (var kvp in value)
                {
                    _Attributes[kvp.Key] = kvp.Value;
                }
            }
        }

        // Auto-generated GUID for the graph
        public Guid GGUID { get; private set; }
        public string Type { get; set; }
        public bool IsGraphFullyConnected => CheckIfGraphFullyConnected(this);

        // Default constructor
        public Graph()
        {
            GGUID = Guid.NewGuid();
            Type = null;
        }

        // Method to set node IDs based on the number of nodes in the graph
        public void SetNodeIds()
        {
            int nodeId = 0;
            foreach (GNode node in QuickGraphObj.Vertices)
            {
                node.Id = $"N{nodeId++}";
            }
        }

        // Method to convert graph properties to attributes
        public virtual void ConvertPropertiesToAttributes()
        {
            // Add basic properties to the Attributes dictionary
            Attributes["NodeCount"] = QuickGraphObj.VertexCount;
            Attributes["EdgeCount"] = QuickGraphObj.EdgeCount;
            Attributes["Type"] = Type;
            Attributes["FullyConnected"] = IsGraphFullyConnected;
        }

        // Deep copy method
        public virtual Graph DeepCopy()
        {
            // Create a new graph using the default constructor
            var copy = new Graph()
            {
                Type = this.Type,
                Attributes = new Dictionary<string, object>(this.Attributes)
            };

            // Create a mapping from original nodes to their copies
            var nodeMapping = new Dictionary<GNode, GNode>();

            // Copy nodes manually and add them to the graph
            foreach (var node in QuickGraphObj.Vertices)
            {
                var nodeCopy = new GNode(node.Point, node.Type)
                {
                    Id = node.Id,
                    Attributes = new Dictionary<string, object>(node.Attributes),
                    GGUID = Guid.NewGuid(),
                    LinkedObjects = new List<object>(node.LinkedObjects) // Copy the linked objects
                };
                nodeMapping[node] = nodeCopy;
                copy.QuickGraphObj.AddVertex(nodeCopy);

                // Add the reference to the new graph
                nodeCopy.ParentGraph = copy;
            }

            // Copy edges manually and add them to the graph
            foreach (var edge in QuickGraphObj.Edges)
            {
                if (nodeMapping.TryGetValue(edge.Source, out GNode sourceCopy) &&
                    nodeMapping.TryGetValue(edge.Target, out GNode targetCopy))
                {
                    var edgeCopy = new GEdge(sourceCopy, targetCopy, edge.Type)
                    {
                        EdgeCurve = edge.EdgeCurve?.DuplicateCurve(),
                        Attributes = new Dictionary<string, object>(edge.Attributes),
                        GGUID = Guid.NewGuid(),
                        QuickGraphEdge = edge.QuickGraphEdge,
                        LinkedObjects = new List<object>(edge.LinkedObjects) // Copy the linked objects
                    };
                    copy.QuickGraphObj.AddEdge(edgeCopy);

                    // Add the reference to the new graph for the edge
                    edgeCopy.ParentGraph = copy;
                }
            }
            return copy;
        }

        // Method to add an edge 
        public void AddEdge(GEdge edge)
        {
            GNode sourceNode = GraphUtils.FindOrMergeNode(this, edge.Source);
            GNode targetNode = GraphUtils.FindOrMergeNode(this, edge.Target);

            var newEdge = new GEdge(sourceNode, targetNode)
            {
                EdgeCurve = new LineCurve(sourceNode.Point, targetNode.Point),
                Type = edge.Type,
                Attributes = new Dictionary<string, object>(edge.Attributes),
                GGUID = edge.GGUID,
                LinkedObjects = new List<object>(edge.LinkedObjects) // Copy the linked objects
            };

            QuickGraphObj.AddEdge(newEdge);
            newEdge.ParentGraph = this;
        }

        // Method to remove an edge
        public void RemoveEdge(GEdge edge)
        {
            QuickGraphObj.RemoveEdge(edge);
            edge.ParentGraph = null;
        }

        // Method to add a node
        public void AddNode(GNode node)
        {
            var newNode = new GNode(node.Point, node.Type)
            {
                Id = node.Id,
                Attributes = new Dictionary<string, object>(node.Attributes),
                GGUID = node.GGUID,
                LinkedObjects = new List<object>(node.LinkedObjects) // Copy the linked objects
            };

            QuickGraphObj.AddVertex(newNode);
            newNode.ParentGraph = this;
        }

        // Method to remove a node and its connected edges
        public void RemoveNode(GNode node)
        {
            QuickGraphObj.RemoveVertex(node);
            node.ParentGraph = null;
        }

        public bool CheckIfGraphFullyConnected(Graph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph), "CheckIfGraphFullyConnected: The provided graph is null.");
            }

            // Convert to bidirectional graph if the input graph is undirected
            var convertedGraph = GraphUtils.ConvertToBidirectionalGraphIfUndirected(graph);

            var weaklyConnectedComponents = new WeaklyConnectedComponentsAlgorithm<GNode, GEdge>(convertedGraph);
            weaklyConnectedComponents.Compute();

            // Check if there is only one connected component
            return weaklyConnectedComponents.ComponentCount == 1;
        }

    }

}

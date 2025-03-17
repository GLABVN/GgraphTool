using Grasshopper.Kernel.Geometry.Delaunay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Glab.Utilities;
using Rhino;
using System.Windows.Media;

namespace Glab.C_Graph
{
    public class GNode : IConnectable
    {
        public string Id { get; set; }
        public Point3d Point { get; set; }
        public string Type { get; set; } = "DefaultNodeType";

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

        // Auto-generated GUID for the node
        public Guid GGUID { get; set; }

        public bool IsNaked
        {
            get
            {
                if (IsInGraph && Valence == 1)
                {
                    return true;
                }
                return false;
            }
        }

        // Dynamic property for valence
        public int Valence
        {
            get
            {
                if (IsInGraph)
                {
                    return ParentGraph.QuickGraphObj.AdjacentEdges(this).Count();
                }
                return -1;
            }
        }

        // Dynamic property for angle
        public double? Angle
        {
            get
            {
                if (Valence == 2)
                {
                    var edges = ParentGraph.QuickGraphObj.AdjacentEdges(this).ToList();
                    if (edges.Count == 2)
                    {
                        var edge1 = edges[0];
                        var edge2 = edges[1];

                        var vector1 = edge1.Source == this ? edge1.Target.Point - Point : edge1.Source.Point - Point;
                        var vector2 = edge2.Source == this ? edge2.Target.Point - Point : edge2.Source.Point - Point;

                        return Vector3d.VectorAngle(vector1, vector2) * (180.0 / Math.PI); // Convert to degrees
                    }
                }
                return null;
            }
        }

        // Reference to the graph this node belongs to
        internal Graph ParentGraph { get; set; }

        // Property to check if the node belongs to any graph
        public bool IsInGraph => ParentGraph != null;

        // New property to store linked objects
        public List<object> LinkedObjects { get; set; } = new List<object>();

        public GNode(Point3d point, string type = null, List<object> linkedObjects = null)
        {
            Point = point;
            Type = type;
            GGUID = Guid.NewGuid();
            LinkedObjects = linkedObjects ?? new List<object>();
        }

        // Method to convert text and numeric properties to attributes
        public void ConvertPropertiesToAttributes()
        {
            Attributes["Id"] = Id;
            Attributes["Type"] = Type;
            Attributes["Valence"] = Valence;
            Attributes["IsInGraph"] = IsInGraph;
            if (Angle.HasValue)
            {
                Attributes["Angle"] = Angle.Value;
            }

            // Convert GUID to string
            Attributes["GGUID"] = GGUID.ToString();

            // Convert null values to string
            foreach (var key in Attributes.Keys.ToList())
            {
                if (Attributes[key] == null)
                {
                    Attributes[key] = "null";
                }
            }
        }

        // Deep copy method
        public GNode DeepCopy()
        {
            var nodeCopy = new GNode(Point, Type, new List<object>(LinkedObjects))
            {
                Id = this.Id,
                Attributes = new Dictionary<string, object>(this.Attributes),
                GGUID = Guid.NewGuid()
            };

            // Associate the copied node with the same graph as the original node
            nodeCopy.ParentGraph = this.ParentGraph;

            return nodeCopy;
        }
    }
}

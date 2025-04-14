using QuikGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Rhino.Geometry;
using Rhino;
using Glab.Utilities;

namespace Glab.C_Graph
{
    public class GEdge : IEdge<GNode>, IConnectable
    {
        public string Id => $"{Source.Id}-{Target.Id}"; // Dynamic Id property
        public double Length => EdgeCurve?.GetLength() ?? 0.0; // Dynamic Length property
        public string Type { get; set; } = "DefaultEdgeType"; // Default type property
        public Curve EdgeCurve { get; set; }
        public List<Curve> EdgeOffsetedCurves { get; private set; } = new List<Curve>();

        public Dictionary<string, object> PropJSON
        {
            get
            {
                return new Dictionary<string, object>
            {
                { "Id", Id },
                { "Length", Length },
                { "Type", Type },
                { "Valence", Valence },
                { "IsInGraph", IsInGraph },
                { "GGUID", GGUID.ToString() },
                { "SourceId", Source?.Id },
                { "TargetId", Target?.Id }
            };
            }
        }

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

        public Guid GGUID { get; set; }
        public bool IsNaked => Source.Valence == 1 || Target.Valence == 1;
        public LineCurve NakedDirectedLine
        {
            get
            {
                if (IsNaked)
                {
                    if (Source.Valence == 1)
                    {
                        return new LineCurve(Source.Point, Target.Point);
                    }
                    else if (Target.Valence == 1)
                    {
                        return new LineCurve(Target.Point, Source.Point);
                    }
                }
                return null;
            }
        }
        public int Valence
        {
            get
            {
                return Math.Max((Source.Valence - 1) + (Target.Valence - 1), 0);
            }
        }

        // Reference to the graphs this edge belongs to
        internal Graph ParentGraph { get; set; }

        // Property to check if the edge belongs to any graph
        public bool IsInGraph => ParentGraph != null;

        // Property to hold the QuickGraph edge information
        public Edge<GNode> QuickGraphEdge { get; set; }

        public GNode Source { get; set; }
        public GNode Target { get; set; }

        // New property to store linked objects
        public List<object> LinkedObjects { get; set; } = new List<object>();

        public GEdge(GNode source, GNode target, string type = null, List<object> linkedObjects = null)
        {
            Source = source;
            Target = target;
            EdgeCurve = new LineCurve(source.Point, target.Point);
            Type = type;
            GGUID = Guid.NewGuid();
            LinkedObjects = linkedObjects ?? new List<object>();
        }

        // Overloaded constructor to accept a Curve object
        public GEdge(Curve curve, string type = null, List<object> linkedObjects = null)
        {
            Source = new GNode(curve.PointAtStart);
            Target = new GNode(curve.PointAtEnd);
            EdgeCurve = new LineCurve(curve.PointAtStart, curve.PointAtEnd);

            Type = type;
            GGUID = Guid.NewGuid();
            LinkedObjects = linkedObjects ?? new List<object>();
        }

        // Deep copy method
        public GEdge DeepCopy()
        {
            var sourceCopy = Source.DeepCopy();
            var targetCopy = Target.DeepCopy();

            var edgeCopy = new GEdge(sourceCopy, targetCopy, Type, new List<object>(LinkedObjects))
            {
                EdgeCurve = EdgeCurve?.DuplicateCurve(),
                Attributes = new Dictionary<string, object>(Attributes),
                GGUID = Guid.NewGuid(),
                QuickGraphEdge = this.QuickGraphEdge // Copy the QuickGraph edge property
            };

            // Associate the copied edge with the same graphs as the original edge
            edgeCopy.ParentGraph = this.ParentGraph;

            return edgeCopy;
        }

        // Method to add an offset curve
        public bool AddOffsetCurve(Curve offsetCurve)
        {
            if (EdgeOffsetedCurves.Count >= 2)
            {
                return false; // Prevent adding more than 2 curves
            }

            // Check if the offsetCurve is in the same direction as the EdgeCurve
            if (EdgeCurve is LineCurve edgeLineCurve && offsetCurve is LineCurve offsetLineCurve)
            {
                Vector3d edgeDirection = edgeLineCurve.Line.Direction;
                Vector3d offsetDirection = offsetLineCurve.Line.Direction;

                if (edgeDirection.IsParallelTo(offsetDirection, RhinoMath.DefaultAngleTolerance) == -1)
                {
                    // Flip the offsetCurve if it is not in the same direction
                    Line flippedLine = CurveUtils.FlipLine(offsetLineCurve.Line);
                    offsetCurve = new LineCurve(flippedLine);
                }
            }

            EdgeOffsetedCurves.Add(offsetCurve);
            return true;
        }
    }
}

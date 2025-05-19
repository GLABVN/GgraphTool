using Glab.C_Graph;
using Neo4j.Driver;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Rhino.UI.Theme;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Glab.Utilities
{
    public static partial class CurveUtils
    {
        public class CurveWithContainmentCount
        {
            public Curve Curve { get; set; }
            public int ContainmentCount { get; set; }

            public CurveWithContainmentCount(Curve curve, int containmentCount)
            {
                Curve = curve;
                ContainmentCount = containmentCount;
            }
        }
        public class CurveGroup
        {
            public Curve OuterCurve { get; set; }
            public List<Curve> Holes { get; set; }

            public CurveGroup(Curve outerCurve)
            {
                OuterCurve = outerCurve;
                Holes = new List<Curve>();
            }
        }
        public class PolylineNode
        {
            public Point3d Point { get; set; }
            public int Index { get; set; }
            public List<PolylineSegment> Segments { get; set; }
            public int SegmentCount => Segments?.Count ?? 0;
            public double? Angle
            {
                get
                {
                    if (Segments == null || Segments.Count != 2)
                    {
                        return null;
                    }

                    Vector3d direction1 = Segments[0].Segment.Direction;
                    Vector3d direction2 = Segments[1].Segment.Direction;
                    double angle = Vector3d.VectorAngle(direction1, direction2) * (180.0 / Math.PI); // Convert to degrees
                    return angle;
                }
            }

            public PolylineNode(Point3d point, int index, List<PolylineSegment> segments)
            {
                if (segments == null || segments.Count > 2)
                {
                    throw new ArgumentException("PolylineNode: The Segment property must not contain more than 2 PolylineSegment objects.", nameof(segments));
                }

                Index = index;
                Point = point;
                Segments = segments;
            }
        }
        public class PolylineSegment
        {
            public Line Segment { get; set; }
            public Line OffsetedSegment { get; set; }
            public int Index { get; set; }
            public PolylineSegment(Line segment, int index)
            {
                Segment = segment;
                Index = index;
            }
        }
        private static List<CurveWithContainmentCount> CreateCurveWithContainment(List<Curve> curves)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "CreateCurveWithContainment: The provided list of curves is null or empty.");
            }

            List<CurveWithContainmentCount> result = new List<CurveWithContainmentCount>();

            // Check if the curves are closed and planar
            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];
                if (curve == null)
                {
                    throw new ArgumentNullException(nameof(curve), "CreateCurveWithContainment: One of the provided curves is null.");
                }
                if (!curve.IsClosed || !curve.IsPlanar())
                {
                    continue;
                }

                int containmentCount = 0;

                // Check how many times the curve is inside another curve
                for (int j = 0; j < curves.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    Curve otherCurve = curves[j];
                    if (otherCurve == null)
                    {
                        throw new ArgumentNullException(nameof(otherCurve), "CreateCurveWithContainment: One of the provided curves is null.");
                    }
                    if (!otherCurve.IsClosed || !otherCurve.IsPlanar())
                    {
                        continue;
                    }
                    if (Curve.PlanarClosedCurveRelationship(curve, otherCurve, Plane.WorldXY, 0.001) == RegionContainment.AInsideB)
                    {
                        containmentCount++;
                    }
                }

                result.Add(new CurveWithContainmentCount(curve, containmentCount));
            }

            return result;
        }
        private static List<CurveGroup> GroupCurvesByContainment(List<Curve> curves)
        {
            var curvesWithContainmentCount = CreateCurveWithContainment(curves);

            if (curvesWithContainmentCount == null || !curvesWithContainmentCount.Any())
            {
                throw new ArgumentNullException(nameof(curvesWithContainmentCount), "GroupCurvesByContainment: The provided list of curves with containment count is null or empty.");
            }

            List<CurveGroup> curveGroups = new List<CurveGroup>();

            // Filter out the outer curves (even containment count or 0)
            var outerCurves = curvesWithContainmentCount
                .Where(c => c.ContainmentCount % 2 == 0)
                .OrderBy(c => c.ContainmentCount)
                .ToList();

            foreach (var outerCurve in outerCurves)
            {
                var group = new CurveGroup(outerCurve.Curve);

                // Find holes for the current outer curve
                var holes = curvesWithContainmentCount
                    .Where(c => c.ContainmentCount == outerCurve.ContainmentCount + 1)
                    .Where(hole => Curve.PlanarClosedCurveRelationship(hole.Curve, outerCurve.Curve, Plane.WorldXY, 0.001) == RegionContainment.AInsideB)
                    .Select(h => h.Curve)
                    .ToList();

                group.Holes.AddRange(holes);
                curveGroups.Add(group);
            }

            return curveGroups;
        }
        private static List<PolylineNode> CreatePolylineNodes(Polyline polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "CreatePolylineNodes: The provided polyline is null.");
            }

            var segments = polyline.GetSegments();
            var polylineNodes = new List<PolylineNode>();
            var segmentDict = new Dictionary<Point3d, List<PolylineSegment>>();

            // Create a dictionary to store segments connected to each point
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = new PolylineSegment(segments[i], i);

                if (!segmentDict.ContainsKey(segments[i].From))
                {
                    segmentDict[segments[i].From] = new List<PolylineSegment>();
                }
                segmentDict[segments[i].From].Add(segment);

                if (!segmentDict.ContainsKey(segments[i].To))
                {
                    segmentDict[segments[i].To] = new List<PolylineSegment>();
                }
                segmentDict[segments[i].To].Add(segment);
            }

            // Create PolylineNode objects for each point with connected segments
            foreach (var kvp in segmentDict)
            {
                var point = kvp.Key;
                var connectedSegments = kvp.Value;

                // Ensure the node contains at most 2 segments
                if (connectedSegments.Count > 2)
                {
                    throw new InvalidOperationException("CreatePolylineNodes: A point is connected to more than 2 segments.");
                }

                polylineNodes.Add(new PolylineNode(point, polylineNodes.Count, connectedSegments));
            }

            return polylineNodes;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Geometry;
using Glab.C_Graph;

namespace Glab.Utilities
{
    public static class VoronoiSkeleton
    {
        public static void ExtractVoronoiSkeleton(
            List<Polyline> boundaryPolylines,
            out List<Polyline> skeleton,
            out List<Graph> skeletonGraphs,
            out List<Curve> uniqueLines,
            double divisionLength = 4.0)
        {
            skeleton = new List<Polyline>();
            skeletonGraphs = new List<Graph>();
            uniqueLines = new List<Curve>();

            if (boundaryPolylines == null || boundaryPolylines.Count == 0)
                throw new ArgumentException("Input polylines must not be null or empty.");

            // Convert polylines to curves
            var curves = boundaryPolylines
                .Where(pl => pl != null && pl.IsClosed)
                .Select(pl => (Curve)pl.ToNurbsCurve())
                .ToList();

            // Use improved containment logic to group curves into outer boundaries and holes
            var curveGroups = CurveUtils.FilterCurvesByContainment(curves);

            List<Curve> allUniqueLines = new List<Curve>();

            foreach (var group in curveGroups)
            {
                // Outer boundary
                Polyline outerPolyline;
                if (!group.outer.TryGetPolyline(out outerPolyline))
                    continue;

                // Holes: combine even and odd as needed, or use one set depending on your logic
                var holes = new List<Curve>();
                if (group.even != null) holes.AddRange(group.even);
                if (group.odd != null) holes.AddRange(group.odd);

                // Step 1: Divide the polyline into points with appropriate spacing
                HashSet<Point3d> uniquePoints = new HashSet<Point3d>();
                List<Point3d> points = new List<Point3d>();
                // Divide outer boundary
                for (int i = 0; i < outerPolyline.SegmentCount; i++)
                {
                    Line segment = outerPolyline.SegmentAt(i);
                    double length = segment.Length;
                    int divisions = Math.Max((int)(length / divisionLength), 1);

                    NurbsCurve nurbsCurve = segment.ToNurbsCurve();

                    if (uniquePoints.Add(segment.From))
                        points.Add(segment.From);

                    Point3d[] divisionPoints;
                    nurbsCurve.DivideByCount(divisions, true, out divisionPoints);
                    foreach (var point in divisionPoints)
                    {
                        if (uniquePoints.Add(point))
                            points.Add(point);
                    }
                }
                // Divide holes as well
                foreach (var hole in holes)
                {
                    Polyline holePolyline;
                    if (hole.TryGetPolyline(out holePolyline))
                    {
                        for (int i = 0; i < holePolyline.SegmentCount; i++)
                        {
                            Line segment = holePolyline.SegmentAt(i);
                            double length = segment.Length;
                            int divisions = Math.Max((int)(length / divisionLength), 1);

                            NurbsCurve nurbsCurve = segment.ToNurbsCurve();

                            if (uniquePoints.Add(segment.From))
                                points.Add(segment.From);

                            Point3d[] divisionPoints;
                            nurbsCurve.DivideByCount(divisions, true, out divisionPoints);
                            foreach (var point in divisionPoints)
                            {
                                if (uniquePoints.Add(point))
                                    points.Add(point);
                            }
                        }
                    }
                }

                // Step 2: Create bounding box for outline
                BoundingBox bb = new BoundingBox(points);
                Vector3d diagonal = bb.Diagonal;
                double inflationFactor = diagonal.Length / 15;
                bb.Inflate(inflationFactor, inflationFactor, inflationFactor);
                Point3d[] bbCorners = bb.GetCorners();

                // Step 3: Convert points to Node2 format
                Node2List nodes = new Node2List();
                foreach (Point3d p in points)
                    nodes.Append(new Node2(p.X, p.Y));

                Node2List outline = new Node2List();
                foreach (Point3d p in bbCorners)
                    outline.Append(new Node2(p.X, p.Y));

                // Step 4: Generate Delaunay triangulation
                var delaunay = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(nodes, 0.01, false);

                // Step 5: Generate Voronoi diagram
                var voronoi = Grasshopper.Kernel.Geometry.Voronoi.Solver.Solve_Connectivity(nodes, delaunay, outline);

                // Step 6: Explode Voronoi cells into individual line segments
                List<Curve> allLines = new List<Curve>();
                foreach (var cell in voronoi)
                {
                    Polyline pl = cell.ToPolyline();
                    foreach (var segment in pl.GetSegments())
                        allLines.Add(segment.ToNurbsCurve());
                }

                // Step 7: Remove duplicate lines
                List<Curve> groupUniqueLines = CurveUtils.RemoveDuplicateLineCurves(allLines);
                allUniqueLines.AddRange(groupUniqueLines);

                // Step 8: Remove lines that intersect with the outer curve or any hole, then trim lines to the boundary curve and keep only the segments inside
                List<Curve> trimmedLines = new List<Curve>();
                foreach (var line in groupUniqueLines)
                {
                    // Check intersection with outer curve
                    var outerIntersections = Intersection.CurveCurve(line, group.outer, 0.001, 0.001);
                    if (outerIntersections.Count > 0)
                        continue; // Skip lines that intersect the outer curve

                    // Check intersection with any hole
                    bool intersectsHole = false;
                    foreach (var hole in holes)
                    {
                        var holeIntersections = Intersection.CurveCurve(line, hole, 0.001, 0.001);
                        if (holeIntersections.Count > 0)
                        {
                            intersectsHole = true;
                            break;
                        }
                    }
                    if (intersectsHole)
                        continue; // Skip lines that intersect any hole

                    // If the line does not intersect the outer curve or any hole, check if it's inside the boundary
                    if (group.outer.Contains(line.PointAt(line.Domain.Mid), Rhino.Geometry.Plane.WorldXY, 0.001) == PointContainment.Inside)
                    {
                        bool inHole = holes.Any(hole => hole.Contains(line.PointAt(line.Domain.Mid), Rhino.Geometry.Plane.WorldXY, 0.001) == PointContainment.Inside);
                        if (!inHole)
                            trimmedLines.Add(line);
                    }
                }

                // Step 9: Reconstruct polylines from trimmed lines
                Rhino.Geometry.Plane boundaryPlane;
                if (!group.outer.TryGetPlane(out boundaryPlane))
                    throw new InvalidOperationException("Failed to get the plane of the boundary polyline.");

                foreach (var line in trimmedLines)
                {
                    Polyline pl;
                    if (line.TryGetPolyline(out pl))
                    {
                        for (int i = 0; i < pl.Count; i++)
                            pl[i] = boundaryPlane.ClosestPoint(pl[i]);
                        skeleton.Add(pl);
                    }
                }
            }

            uniqueLines = allUniqueLines;

            // Step 10: Create graphs from skeleton
            List<GEdge> graphEdges = new List<GEdge>();
            foreach (var polyline in skeleton)
            {
                var segments = polyline.GetSegments();
                foreach (var segment in segments)
                {
                    var edge = new GEdge(new GNode(segment.From), new GNode(segment.To))
                    {
                        EdgeCurve = segment.ToNurbsCurve()
                    };
                    graphEdges.Add(edge);
                }
            }

            List<GNode> isolatedNodes;
            List<GEdge> isolatedEdges;
            skeletonGraphs = GraphUtils.CreateGraphsFromNodesAndEdges(null, graphEdges, out isolatedNodes, out isolatedEdges, divideEdge: false);

            // Optionally prune and check connectivity for each graph
            for (int i = 0; i < skeletonGraphs.Count; i++)
            {
                var graph = skeletonGraphs[i];
                if (!graph.IsGraphFullyConnected)
                    throw new InvalidOperationException("A Voronoi graph is out of tolerance and not fully connected.");
                skeletonGraphs[i] = GraphUtils.PruneGraphByType(graph, pruneOnce: true);
            }
        }
    }
}

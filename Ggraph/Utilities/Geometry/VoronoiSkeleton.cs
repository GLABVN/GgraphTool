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
        public static void ExtractVoronoiSkeleton(Polyline boundaryPolyline, out List<Polyline> skeleton, out Graph skeletonGraph, double divisionLength = 4.0)
        {
            skeleton = new List<Polyline>();
            skeletonGraph = new Graph();

            // Validate polyline
            if (boundaryPolyline == null)
                return;

            // Check if polyline is closed
            if (!boundaryPolyline.IsClosed)
                throw new ArgumentException("ExtractVoronoiSkeleton: Input polyline must be closed.");

            // Check if polyline is planar
            var boundaryCurve = boundaryPolyline.ToNurbsCurve();
            if (!boundaryCurve.IsPlanar())
                throw new ArgumentException("ExtractVoronoiSkeleton: Input polyline must be planar.");

            // Step 1: Divide the polyline into points with appropriate spacing
            HashSet<Point3d> uniquePoints = new HashSet<Point3d>();
            List<Point3d> points = new List<Point3d>();
            for (int i = 0; i < boundaryPolyline.SegmentCount; i++)
            {
                Line segment = boundaryPolyline.SegmentAt(i);
                double length = segment.Length;
                int divisions = Math.Max((int)(length / divisionLength), 1);

                // Convert segment to NurbsCurve
                NurbsCurve nurbsCurve = segment.ToNurbsCurve();

                // Add segment start point
                if (uniquePoints.Add(segment.From))
                {
                    points.Add(segment.From);
                }

                // Add intermediate points using DivideByCount
                Point3d[] divisionPoints;
                nurbsCurve.DivideByCount(divisions, true, out divisionPoints);
                foreach (var point in divisionPoints)
                {
                    if (uniquePoints.Add(point))
                    {
                        points.Add(point);
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
            {
                nodes.Append(new Node2(p.X, p.Y));
            }

            // Create outline nodes from bounding box
            Node2List outline = new Node2List();
            foreach (Point3d p in bbCorners)
            {
                outline.Append(new Node2(p.X, p.Y));
            }

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
                {
                    allLines.Add(segment.ToNurbsCurve());
                }
            }

            // Step 7: Remove duplicate lines
            List<Curve> uniqueLines = CurveUtils.RemoveDuplicateLineCurves(allLines);

            // Step 8: Trim lines to the boundary curve and keep only the segments inside
            List<Curve> trimmedLines = new List<Curve>();
            foreach (var line in uniqueLines)
            {
                var intersections = Intersection.CurveCurve(line, boundaryCurve, 0.001, 0.001);
                if (intersections.Count > 0)
                {
                    foreach (var intersection in intersections)
                    {
                        if (intersection.IsPoint)
                        {
                            var trimmedLine = line.Trim(intersection.ParameterA, line.Domain.Max);
                            if (trimmedLine != null && boundaryCurve.Contains(trimmedLine.PointAt(trimmedLine.Domain.Mid), Rhino.Geometry.Plane.WorldXY, 0.001) == PointContainment.Inside)
                            {
                                trimmedLines.Add(trimmedLine);
                            }
                        }
                    }
                }
                else
                {
                    if (boundaryCurve.Contains(line.PointAt(line.Domain.Mid), Rhino.Geometry.Plane.WorldXY, 0.001) == PointContainment.Inside)
                    {
                        trimmedLines.Add(line);
                    }
                }
            }

            // Step 9: Reconstruct polylines from trimmed lines
            Rhino.Geometry.Plane boundaryPlane;
            if (!boundaryCurve.TryGetPlane(out boundaryPlane))
            {
                throw new InvalidOperationException("Failed to get the plane of the boundary polyline.");
            }

            foreach (var line in trimmedLines)
            {
                Polyline pl;
                if (line.TryGetPolyline(out pl))
                {
                    // Project each point of the polyline back to the plane of the input boundaryPolyline
                    for (int i = 0; i < pl.Count; i++)
                    {
                        pl[i] = boundaryPlane.ClosestPoint(pl[i]);
                    }
                    skeleton.Add(pl);
                }
            }

            // Step 10: Create graph from skeleton
            List<GEdge> graphEdges = new List<GEdge>();

            foreach (var polyline in skeleton)
            {
                // Create edge for each segment in the polyline
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
            skeletonGraph = GraphUtils.CreateGraphFromNodesAndEdges(null, graphEdges, out isolatedNodes, out isolatedEdges, divideEdge: false);

            // Check if the graph is fully connected
            if (!skeletonGraph.IsGraphFullyConnected)
            {
                throw new InvalidOperationException("The Voronoi graph is out of tolerance and not fully connected.");
            }

            // Step 11: Prune graph once
            skeletonGraph = GraphUtils.PruneGraphByType(skeletonGraph, pruneOnce: true);
        }
    }

}

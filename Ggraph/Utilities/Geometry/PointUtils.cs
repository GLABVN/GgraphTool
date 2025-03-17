using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Glab.Utilities
{
    public static class PointUtils
    {
        public static Point3d RoundPoint(Point3d point, int decimalPlace = 2)
        {
            return new Point3d(
                Math.Round(point.X, decimalPlace),
                Math.Round(point.Y, decimalPlace),
                Math.Round(point.Z, decimalPlace)
            );
        }

        public static void SortPointsByZ(
            List<GH_Point> ghPoints,
            double tolerance,
            GH_Path inputPointBranch,
            GH_Structure<GH_Point> groupedPointsTree,
            GH_Structure<GH_Integer> groupedIndicesTree)
        {
            var zGroups = ghPoints.GroupBy(
                p => Math.Round(p.Value.Z / tolerance) * tolerance
            ).OrderBy(g => g.Key)
             .ToDictionary(g => g.Key, g => g.ToList());

            int zIndex = 0;
            foreach (var zGroup in zGroups)
            {
                GH_Path outputPath = new GH_Path(inputPointBranch).AppendElement(zIndex);

                List<GH_Point> groupedPoint = zGroup.Value;
                groupedPointsTree.AppendRange(groupedPoint, outputPath);

                List<GH_Integer> indices = groupedPoint
                    .Select(p => new GH_Integer(ghPoints.IndexOf(p)))
                    .ToList();
                groupedIndicesTree.AppendRange(indices, outputPath);

                zIndex++;
            }
        }

        public static void SortPointsByXYThenZ(
            List<GH_Point> ghPoints,
            double xyTolerance,
            double zTolerance,
            GH_Path inputPointBranch,
            GH_Structure<GH_Point> groupedPointsTree,
            GH_Structure<GH_Integer> groupedIndicesTree)
        {
            var xyGroups = ghPoints.GroupBy(
                p => (
                    Math.Round(p.Value.X / xyTolerance) * xyTolerance,
                    Math.Round(p.Value.Y / xyTolerance) * xyTolerance
                )
            ).OrderBy(g => g.Key)
             .ToDictionary(g => g.Key, g => g.ToList());

            int xyGroupIndex = 0;
            foreach (var xyGroup in xyGroups)
            {
                var zGroups = xyGroup.Value.GroupBy(
                    p => Math.Round(p.Value.Z / zTolerance) * zTolerance
                ).OrderBy(g => g.Key)
                 .ToDictionary(g => g.Key, g => g.ToList());

                int zGroupIndex = 0;
                foreach (var zGroup in zGroups)
                {
                    GH_Path outputPath = new GH_Path(inputPointBranch).AppendElement(xyGroupIndex).AppendElement(zGroupIndex);

                    List<GH_Point> groupedPoints = zGroup.Value;
                    groupedPointsTree.AppendRange(groupedPoints, outputPath);

                    List<GH_Integer> groupedIndices = groupedPoints
                        .Select(p => new GH_Integer(ghPoints.IndexOf(p)))
                        .ToList();
                    groupedIndicesTree.AppendRange(groupedIndices, outputPath);

                    zGroupIndex++;
                }
                xyGroupIndex++;
            }
        }

        public static List<IndexedPoint> FindClosestPoints(List<Point3d> points, List<Point3d> searchPoints)
        {
            if (points == null || points.Count == 0 || searchPoints == null || searchPoints.Count == 0)
                return new List<IndexedPoint>();

            // Create RTree for spatial indexing - create once for all search points
            var rTree = new RTree();
            var indexedPoints = new List<IndexedPoint>();

            // Populate RTree and create indexed points - done once
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                indexedPoints.Add(new IndexedPoint(point, i));

                // Create a tiny bounding box around the point
                var bbox = new BoundingBox(point, point);
                rTree.Insert(bbox, i);
            }

            var closestPoints = new List<IndexedPoint>();

            // Search for closest points using the same RTree
            foreach (var searchPoint in searchPoints)
            {
                var searchBbox = new BoundingBox(searchPoint, searchPoint);
                var nearestPoints = new List<int>();

                // Start with a small search radius and increase if needed
                double searchRadius = 0.1;
                while (nearestPoints.Count == 0 && searchRadius < 1e6)
                {
                    var expandedBox = searchBbox;
                    expandedBox.Inflate(searchRadius);
                    rTree.Search(expandedBox, (sender, args) => nearestPoints.Add(args.Id));
                    searchRadius *= 2;
                }

                if (nearestPoints.Count > 0)
                {
                    // Find the actual closest point among candidates
                    var closestPoint = nearestPoints
                        .Select(i => indexedPoints[i])
                        .OrderBy(ip => ip.Point.DistanceTo(searchPoint))
                        .First();

                    closestPoints.Add(closestPoint);
                }
            }

            return closestPoints;
        }

        // Keep the original method for backward compatibility
        public static IndexedPoint FindClosestPoint(List<Point3d> points, Point3d searchPoint)
        {
            return FindClosestPoints(points, new List<Point3d> { searchPoint }).FirstOrDefault();
        }

        public static bool ArePointsEqual(Point3d point1, Point3d point2, int decimalPlace = 2)
        {
            var roundedPoint1 = RoundPoint(point1, decimalPlace);
            var roundedPoint2 = RoundPoint(point2, decimalPlace);

            return roundedPoint1.Equals(roundedPoint2);
        }

    }

    public class IndexedPoint
    {
        public Point3d Point { get; }
        public int Index { get; }

        public IndexedPoint(Point3d point, int index)
        {
            Point = point;
            Index = index;
        }
    }
}

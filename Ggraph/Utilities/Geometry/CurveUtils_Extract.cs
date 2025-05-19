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
        public static List<Plane> GetLineSegmentPatternPlane(Line baseLine, List<double> lengths, out double finalSumLength, bool flipStart = false, bool extendLine = false, bool reachEnd = false, double shiftDistance = 0.00)
        {
            // TODO fix flip
            if (flipStart)
            {
                baseLine = FlipLine(baseLine);
            }

            List<Plane> planes = new List<Plane>();
            double totalLength = lengths.Sum();
            double lineLength = baseLine.Length;

            // Extend the line if the total length is greater than the line length
            if (extendLine && totalLength > lineLength)
            {
                Vector3d direction = baseLine.Direction;
                direction.Unitize();
                baseLine.To += direction * (totalLength - lineLength);
            }

            double currentLength = 0;
            Point3d currentPoint = baseLine.From;

            for (int i = 0; i < lengths.Count; i++)
            {
                double length = lengths[i];
                currentLength += length; // Add the length
                if (currentLength <= lineLength)
                {
                    Point3d nextPoint = baseLine.PointAtLength(currentLength);
                    Line segment = new Line(currentPoint, nextPoint);

                    // Calculate the direction of the segment
                    Vector3d xAxis = segment.Direction;
                    xAxis.Unitize();

                    //if (flipStart)
                    //{
                    //    // Flip the xAxis vector
                    //    xAxis = -xAxis;
                    //}

                    Point3d midpoint = segment.PointAt(0.5);
                    Vector3d zAxis = new Vector3d(0, 0, 1);
                    Vector3d yAxis = Vector3d.CrossProduct(zAxis, xAxis);
                    Plane plane = new Plane(midpoint, xAxis, yAxis);

                    // Apply the shift distance if not reaching the end
                    if (!reachEnd)
                    {
                        Vector3d moveVector = baseLine.Direction;
                        moveVector.Unitize();
                        plane.Origin += moveVector * shiftDistance;
                    }

                    planes.Add(plane); // Add the plane to the list
                    currentPoint = nextPoint;
                }
                else
                {
                    break;
                }
            }

            // reach end if required
            if (reachEnd && totalLength < lineLength)
            {
                double redundantLength = lineLength - totalLength;
                Vector3d moveVector = baseLine.Direction;
                moveVector.Unitize();
                moveVector *= (redundantLength - shiftDistance); // Move to the end and then back by shiftDistance

                for (int i = 0; i < planes.Count; i++)
                {
                    var plane = planes[i];
                    plane.Origin += moveVector;
                    planes[i] = plane;
                }
            }

            finalSumLength = currentLength;
            return planes;
        }
        public static double GetCurveElevation(List<Curve> curves)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "GetCurveElevation: The provided list of curves is null or empty.");
            }

            double minElevation = double.MaxValue;

            foreach (var curve in curves)
            {
                if (curve == null)
                {
                    throw new ArgumentNullException(nameof(curve), "GetCurveElevation: The provided curve is null.");
                }

                BoundingBox bbox = curve.GetBoundingBox(false);
                if (bbox.Min.Z < minElevation)
                {
                    minElevation = bbox.Min.Z;
                }
            }

            return Math.Round(minElevation, 2);
        }
        public static Rectangle GetBoundingRectangleOfMultipleCurves(List<Curve> curves)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "GetBoundingRectangleOfMultipleCurves: The provided list of curves is null or empty.");
            }

            // Initialize with the bounding box of the first curve
            BoundingBox boundingBox = curves[0].GetBoundingBox(true);

            // Expand the bounding box to include all curves
            foreach (var curve in curves.Skip(1))
            {
                boundingBox.Union(curve.GetBoundingBox(true));
            }

            // Create rectangle with padding
            return new Rectangle(
                (int)(boundingBox.Min.X),
                (int)(boundingBox.Min.Y),
                (int)(boundingBox.Max.X - boundingBox.Min.X),
                (int)(boundingBox.Max.Y - boundingBox.Min.Y)
            );
        }
        public static Plane GetCurveXYBottomCenter(List<Curve> curve)
        {

            // Step 1: Get the bounding box of the curve

            Rectangle rect = GetBoundingRectangleOfMultipleCurves(curve);
            BoundingBox boundingBox = new BoundingBox(
                new Point3d(rect.Left, rect.Top, 0),
                new Point3d(rect.Right, rect.Bottom, 0)
            );
            if (!boundingBox.IsValid)
            {
                throw new InvalidOperationException("GetCurveXYBottomCenter: The bounding box of the curve is invalid.");
            }

            // Step 2: Calculate the bottom center point of the bounding rectangle
            double width = boundingBox.Max.X - boundingBox.Min.X;
            double height = boundingBox.Max.Y - boundingBox.Min.Y;
            Point3d bottomCenter = new Point3d(boundingBox.Min.X + (width / 2), boundingBox.Min.Y, boundingBox.Min.Z);

            // Step 3: Create a plane using the bottom center point
            Plane plane = new Plane(bottomCenter, Vector3d.XAxis, Vector3d.YAxis);

            return plane;
        }
        public static List<(Curve outer, List<Curve> even, List<Curve> odd)> FilterCurvesByContainment(List<Curve> curves)
        {
            if (curves == null || !curves.Any())
                throw new ArgumentNullException(nameof(curves), "FilterCurvesByContainment: The provided list of curves is null or empty.");

            // Use the grouping logic from CurveUtils.cs
            var groups = GroupCurvesByContainment(curves);

            var result = new List<(Curve outer, List<Curve> even, List<Curve> odd)>();

            foreach (var group in groups)
            {
                var evenCurves = new List<Curve>();
                var oddCurves = new List<Curve>();

                foreach (var hole in group.Holes)
                {
                    // Determine containment count for each hole
                    int containmentCount = 0;
                    foreach (var other in curves)
                    {
                        if (ReferenceEquals(hole, other)) continue;
                        if (Curve.PlanarClosedCurveRelationship(hole, other, Plane.WorldXY, 0.001) == RegionContainment.AInsideB)
                        {
                            containmentCount++;
                        }
                    }
                    if (containmentCount % 2 == 0)
                        evenCurves.Add(hole);
                    else
                        oddCurves.Add(hole);
                }

                result.Add((group.OuterCurve, evenCurves, oddCurves));
            }

            return result;
        }
        public static (List<Line> sortedLines, List<int> sortedIndices) SortLinesByAngle(List<Line> lines)
        {
            if (lines == null || !lines.Any())
            {
                throw new ArgumentNullException(nameof(lines), "SortLinesByAngle: The provided list of lines is null or empty.");
            }

            // If there's only one line, return it as is
            if (lines.Count == 1)
            {
                return (lines, new List<int> { 0 });
            }

            // Get the direction vector of the first line
            Vector3d referenceDirection = lines[0].Direction;
            referenceDirection.Unitize();

            // Create a list of tuples containing the line, its angle with the reference direction, and its original index
            var lineAngles = new List<(Line line, double angle, int index)>();
            lineAngles.Add((lines[0], 0, 0)); // First line has angle 0

            // Calculate angles for remaining lines
            for (int i = 1; i < lines.Count; i++)
            {
                Vector3d currentDirection = lines[i].Direction;
                currentDirection.Unitize();

                // Calculate angle between vectors (in radians)
                double angle = Vector3d.VectorAngle(referenceDirection, currentDirection);

                // Get the sign of the angle using cross product
                Vector3d cross = Vector3d.CrossProduct(referenceDirection, currentDirection);
                if (cross.Z < 0)
                {
                    angle = 2 * Math.PI - angle;
                }

                lineAngles.Add((lines[i], angle, i));
            }

            // Sort lines by angle
            var sortedLineAngles = lineAngles.OrderBy(x => x.angle).ToList();

            // Extract sorted lines and their original indices
            var sortedLines = sortedLineAngles.Select(x => x.line).ToList();
            var sortedIndices = sortedLineAngles.Select(x => x.index).ToList();

            return (sortedLines, sortedIndices);
        }
        public static void GetProjectedCurveRegion(List<Curve> inputCurves, out List<Curve> outerRegions, out List<Curve> holeRegions, bool includeHoles = false, bool gradually = false)
        {
            outerRegions = new List<Curve>();
            holeRegions = new List<Curve>();

            if (inputCurves == null || !inputCurves.Any())
            {
                throw new ArgumentNullException(nameof(inputCurves), "The provided list of curves is null or empty.");
            }

            // Ensure all input curves are closed
            foreach (var curve in inputCurves)
            {
                if (curve == null)
                {
                    throw new ArgumentNullException(nameof(curve), "One of the provided curves is null.");
                }

                if (!curve.IsClosed)
                {
                    throw new ArgumentException("All input curves must be closed.", nameof(inputCurves));
                }
            }

            // Project curves to the XY plane
            List<Curve> projectedCurves = new List<Curve>();
            foreach (var curve in inputCurves)
            {
                Curve projectedCurve = curve.DuplicateCurve();
                projectedCurve.Transform(Transform.PlanarProjection(Plane.WorldXY));
                projectedCurves.Add(projectedCurve);
            }

            CurveBooleanRegions combinedRegions = null;

            // Gradually combine regions to get one final combined region
            if (gradually)
            {
                foreach (var curve in projectedCurves)
                {
                    if (combinedRegions == null)
                    {
                        combinedRegions = Curve.CreateBooleanRegions(new List<Curve> { curve }, Plane.WorldXY, true, 0.001);
                    }
                    else
                    {
                        var newRegions = Curve.CreateBooleanRegions(new List<Curve> { curve }, Plane.WorldXY, true, 0.001);
                        if (newRegions != null && newRegions.RegionCount > 0)
                        {
                            List<Curve> combinedCurves = new List<Curve>();
                            for (int i = 0; i < combinedRegions.RegionCount; i++)
                            {
                                combinedCurves.AddRange(combinedRegions.RegionCurves(i));
                            }
                            for (int i = 0; i < newRegions.RegionCount; i++)
                            {
                                combinedCurves.AddRange(newRegions.RegionCurves(i));
                            }
                            combinedRegions = Curve.CreateBooleanRegions(combinedCurves, Plane.WorldXY, true, 0.001);
                        }
                    }
                }
            }
            else
            {
                combinedRegions = Curve.CreateBooleanRegions(projectedCurves, Plane.WorldXY, true, 0.001);
            }

            if (combinedRegions == null || combinedRegions.RegionCount == 0)
            {
                throw new InvalidOperationException("Failed to compute the combined regions of the curves.");
            }

            // Add the combined regions to the output list
            for (int i = 0; i < combinedRegions.RegionCount; i++)
            {
                var regionCurves = combinedRegions.RegionCurves(i);
                if (regionCurves != null && regionCurves.Length > 0)
                {
                    foreach (var regionCurve in regionCurves)
                    {
                        //if (!regionCurve.IsClosed)
                        //{
                        //    throw new InvalidOperationException("One or more region curves are not closed.");
                        //}

                        outerRegions.Add(regionCurve);
                    }
                }
            }

            // Simplify the final combined regions
            for (int i = 0; i < outerRegions.Count; i++)
            {
                Polyline polyline;
                if (outerRegions[i].TryGetPolyline(out polyline))
                {
                    outerRegions[i] = SimplifyPolylineByAngle(polyline, new Interval(2, 178)).ToNurbsCurve();
                }
            }

            // TODO process if space has hole, now it only works for hole that is not inclusion in any input curves

            if (includeHoles)
            {

                // Perform a boolean region operation to split the input curves into regions
                CurveBooleanRegions regions = Curve.CreateBooleanRegions(projectedCurves, Plane.WorldXY, false, 0.001);
                if (regions == null || regions.RegionCount == 0)
                {
                    throw new InvalidOperationException("Failed to compute the regions of the curves.");
                }

                // Generate a point inside each region and classify the regions
                for (int i = 0; i < regions.RegionCount; i++)
                {
                    var regionCurves = regions.RegionCurves(i);
                    if (regionCurves == null || regionCurves.Length == 0 || !regionCurves.All(rc => rc.IsClosed))
                    {
                        continue;
                    }
                    // Generate a point inside the region (Generate random point have better performance)
                    Point3d pointInsideRegion = IdentifyPointInClosedPolyline(regionCurves[0]);

                    // Check if the point is inside at least one of the input curves
                    bool isInside = inputCurves.Any(curve => curve.Contains(pointInsideRegion, Plane.WorldXY, 0.001) == PointContainment.Inside);

                    if (!isInside)
                    {
                        holeRegions.AddRange(regionCurves);
                    }
                }

                // Union all hole regions using regions
                if (holeRegions.Any())
                {
                    CurveBooleanRegions holeRegionsResult = Curve.CreateBooleanRegions(holeRegions, Plane.WorldXY, true, 0.001);
                    if (holeRegionsResult != null && holeRegionsResult.RegionCount > 0)
                    {
                        holeRegions.Clear();
                        for (int i = 0; i < holeRegionsResult.RegionCount; i++)
                        {
                            var regionCurves = holeRegionsResult.RegionCurves(i);
                            if (regionCurves != null && regionCurves.Length > 0)
                            {
                                holeRegions.AddRange(regionCurves);
                            }
                        }
                    }

                    // Simplify hole regions
                    for (int i = 0; i < holeRegions.Count; i++)
                    {
                        Polyline polyline;
                        if (holeRegions[i].TryGetPolyline(out polyline))
                        {
                            holeRegions[i] = SimplifyPolylineByAngle(polyline, new Interval(2, 178)).ToNurbsCurve();
                        }
                    }
                }

            }
        }
        public static Point3d IdentifyPointInClosedPolyline(Curve curve, double offsetDistance = 2, bool toCenter = false)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve), "IdentifyPointInClosedPolyline: The provided curve is null.");
            }

            if (!curve.IsClosed || !curve.IsPlanar())
            {
                throw new ArgumentException("IdentifyPointInClosedPolyline: The provided curve must be closed and planar.", nameof(curve));
            }

            Plane plane;
            if (!curve.TryGetPlane(out plane))
            {
                throw new InvalidOperationException("IdentifyPointInClosedPolyline: Failed to get the plane of the polyline.");
            }

            // Convert curve to polyline
            Polyline polyline;
            if (!curve.TryGetPolyline(out polyline))
            {
                polyline = ConvertCurveToPolyline(curve);
            }

            // Check if the polyline is convex
            if (IsConvex(polyline))
            {
                return polyline.CenterPoint();
            }

            // Step 1: get the correct direction
            Curve[] offsetCurves = curve.Offset(plane, offsetDistance, 0.001, CurveOffsetCornerStyle.Sharp);
            if (offsetCurves != null)
            {
                Point3d initialPoint = offsetCurves[0].PointAtEnd;
                offsetDistance = curve.Contains(initialPoint, plane, 0.001) == PointContainment.Inside ? offsetDistance : -offsetDistance;
            }

            // Step 2: divide the offset distance to return a valid result
            offsetCurves = curve.Offset(plane, offsetDistance, 0.001, CurveOffsetCornerStyle.Sharp);
            if (offsetCurves == null)
            {
                int stepCount = 0;
                while ((offsetCurves == null) && stepCount < 20)
                {
                    offsetCurves = curve.Offset(plane, offsetDistance, 0.001, CurveOffsetCornerStyle.Sharp);
                    if (offsetCurves == null)
                    {
                        offsetDistance /= 2;
                        stepCount++;
                    }
                }
            }

            Point3d resultpoint = new();

            // Step 3: optional offset to center
            if (toCenter)
            {
                // Find the largest curve by area
                Curve largestCurve = offsetCurves.OrderByDescending(c => AreaMassProperties.Compute(c).Area).FirstOrDefault();

                int offsetCount = 0;
                Curve[] previousResult = offsetCurves;
                while (largestCurve != null && offsetCount < 20)
                {
                    offsetCurves = largestCurve.Offset(plane, offsetDistance, 0.001, CurveOffsetCornerStyle.Sharp);
                    if (offsetCurves == null)
                    {
                        break;
                    }
                    else
                    {
                        previousResult = offsetCurves;
                        largestCurve = offsetCurves.OrderByDescending(c => AreaMassProperties.Compute(c).Area).FirstOrDefault();
                        offsetCount++;
                    }
                }
                // If the offset result == null, extract point from the previous result
                resultpoint = previousResult[0].PointAtEnd;
            }
            else
            {
                // Get a point on the resulting polyline
                resultpoint = offsetCurves[0].PointAtEnd;
            }

            return resultpoint;
        }

    }
}
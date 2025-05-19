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
        public static Polyline ConvertCurveToPolyline(Curve curve, double tolerance = 0.01)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve), "ConvertCurveToPolyline: The provided curve is null.");
            }

            if (!curve.IsValid)
            {
                throw new ArgumentException("ConvertCurveToPolyline: The provided curve is not valid.", nameof(curve));
            }

            Polyline polyline;
            if (!curve.TryGetPolyline(out polyline))
            {
                // If the curve cannot be directly converted to a polyline, approximate it
                var polylineCurve = curve.ToPolyline(tolerance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 0.0, 0.0);
                if (polylineCurve == null || !polylineCurve.TryGetPolyline(out polyline))
                {
                    throw new InvalidOperationException("ConvertCurveToPolyline: Failed to convert the curve to a polyline.");
                }
            }

            return polyline;
        }
        public static Curve ClockwiseCurve(Curve curve)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve), "ClockwiseCurve: The provided curve is null.");
            }

            if (!curve.IsClosed)
            {
                throw new ArgumentException("ClockwiseCurve: The provided curve is not closed.", nameof(curve));
            }

            // Check the orientation of the curve
            if (curve.ClosedCurveOrientation(Plane.WorldXY) != CurveOrientation.Clockwise)
            {
                // Reverse the curve to make it clockwise
                curve.Reverse();
            }

            return curve;
        }
        public static void OffsetLine2Sides(Line baseLine, double totalDistance, out Line leftLine, out Line rightLine, out Curve closedBoundary)
        {
            if (totalDistance <= 0)
            {
                throw new ArgumentException("OffsetLine2Sides: Total distance must be greater than zero.", nameof(totalDistance));
            }

            double offsetDistance = totalDistance / 2.0;

            // Calculate the direction vector of the base line
            Vector3d direction = baseLine.Direction;
            direction.Unitize();

            // Calculate the offset direction vectors using the cross product with the Z-axis
            Vector3d offsetDirectionLeft = Vector3d.CrossProduct(Vector3d.ZAxis, direction);
            offsetDirectionLeft.Unitize();
            offsetDirectionLeft *= offsetDistance;

            Vector3d offsetDirectionRight = -offsetDirectionLeft;

            // Create the left and right offset lines
            Point3d leftStart = baseLine.From + offsetDirectionLeft;
            Point3d leftEnd = baseLine.To + offsetDirectionLeft;
            leftLine = new Line(leftStart, leftEnd);

            Point3d rightStart = baseLine.From + offsetDirectionRight;
            Point3d rightEnd = baseLine.To + offsetDirectionRight;
            rightLine = new Line(rightStart, rightEnd);

            //add closed boundary using ConnectCurves
            closedBoundary = ConnectCurves(leftLine.ToNurbsCurve(), rightLine.ToNurbsCurve());
        }
        public static void OffsetLine1Side(Line baseLine, double distance, out Line offsetLine, out Curve connectedCurve, bool closed = false)
        {
            // Calculate the direction vector of the base line
            Vector3d direction = baseLine.Direction;
            direction.Unitize();

            // Calculate the offset direction vector using the cross product with the Z-axis
            Vector3d offsetDirection = Vector3d.CrossProduct(Vector3d.ZAxis, direction);
            offsetDirection.Unitize();
            offsetDirection *= distance;

            // Create the offset line
            Point3d start = baseLine.From + offsetDirection;
            Point3d end = baseLine.To + offsetDirection;
            offsetLine = new Line(start, end);

            connectedCurve = null;

            if (closed)
            {
                Curve baseCurve = baseLine.ToNurbsCurve();
                Curve offsetCurve = offsetLine.ToNurbsCurve();
                connectedCurve = ConnectCurves(baseCurve, offsetCurve);
            }
        }
        public static Line FlipLine(Line line)
        {
            return new Line(line.To, line.From);
        }
        public static Line TrimLineWithCurves(Line line, List<Curve> closedCurves)
        {
            // Ensure all curves are closed.
            foreach (var closedCurve in closedCurves)
            {
                if (!closedCurve.IsClosed)
                {
                    throw new ArgumentException("TrimLineWithCurves: All curves must be closed.", nameof(closedCurves));
                }
            }

            // Initialize the largest line with the original line
            Line largestLine = line;
            double largestLength = 0;

            // List to collect all intersection parameters
            List<double> allParameters = new List<double>();

            foreach (var closedCurve in closedCurves)
            {
                // Calculate the intersection points between the line and the curve.
                var events = Intersection.CurveCurve(line.ToNurbsCurve(), closedCurve, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

                // If there are no intersections, the line is either completely inside or outside the curve.
                if (events == null || events.Count == 0)
                {
                    continue; // Skip to the next curve
                }

                // Collect the parameters of the intersection points on the line.
                allParameters.AddRange(events.Select(e => e.ParameterA));
            }

            // Split the line at the collected intersection points.
            var segments = line.ToNurbsCurve().Split(allParameters);

            // Find the largest line segment outside the curves.
            foreach (var segment in segments)
            {
                var midPoint = segment.PointAtNormalizedLength(0.5);
                bool isOutsideAllCurves = true;

                foreach (var closedCurve in closedCurves)
                {
                    if (closedCurve.Contains(midPoint, Plane.WorldXY, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) != PointContainment.Outside)
                    {
                        isOutsideAllCurves = false;
                        break;
                    }
                }

                if (isOutsideAllCurves)
                {
                    var length = segment.GetLength();
                    if (length > largestLength)
                    {
                        largestLength = length;
                        largestLine = new Line(segment.PointAtStart, segment.PointAtEnd);
                    }
                }
            }

            return largestLine;
        }
        public static List<Curve> RemoveDuplicateLineCurves(List<Curve> curves, double tolerance = 0.001)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "RemoveDuplicateLines: The provided list of curves is null or empty.");
            }

            List<Curve> uniqueCurves = new List<Curve>();
            HashSet<int> processedIndices = new HashSet<int>();

            for (int i = 0; i < curves.Count; i++)
            {
                if (processedIndices.Contains(i)) continue;

                var curve1 = curves[i];
                if (curve1 == null) continue;

                var start1 = curve1.PointAtStart;
                var end1 = curve1.PointAtEnd;

                // Add the first instance to uniqueCurves immediately
                uniqueCurves.Add(curve1);
                processedIndices.Add(i);

                // Then mark any duplicates of this curve
                for (int j = i + 1; j < curves.Count; j++)
                {
                    var curve2 = curves[j];
                    if (curve2 == null) continue;

                    var start2 = curve2.PointAtStart;
                    var end2 = curve2.PointAtEnd;

                    // Check both forward and reverse directions
                    bool sameEndpoints = (
                        // Forward direction
                        (start1.DistanceTo(start2) < tolerance &&
                         end1.DistanceTo(end2) < tolerance) ||
                        // Reverse direction
                        (start1.DistanceTo(end2) < tolerance &&
                         end1.DistanceTo(start2) < tolerance)
                    );

                    if (sameEndpoints)
                    {
                        processedIndices.Add(j);
                    }
                }
            }

            return uniqueCurves;
        }
        public static Curve ConnectCurves(Curve curve1, Curve curve2)
        {
            if (curve1 == null || curve2 == null)
            {
                throw new ArgumentNullException("ConnectCurves: One or both of the provided curves are null.");
            }

            if (!curve1.IsValid || !curve2.IsValid)
            {
                throw new ArgumentException("ConnectCurves: One or both of the provided curves are not valid.");
            }

            // Create lines to connect the start and end points of the curves
            Line startToStart = new Line(curve1.PointAtStart, curve2.PointAtStart);
            Line endToEnd = new Line(curve1.PointAtEnd, curve2.PointAtEnd);

            // Create a list to join the curves and lines
            List<Curve> curvesToJoin = new List<Curve>();
            curvesToJoin.Add(curve1);
            curvesToJoin.Add(startToStart.ToNurbsCurve());
            curvesToJoin.Add(curve2);
            curvesToJoin.Add(endToEnd.ToNurbsCurve());

            // Join the curves into a single curve
            Curve[] joinedCurves = Curve.JoinCurves(curvesToJoin);
            if (joinedCurves.Length == 1)
            {
                return joinedCurves[0];
            }
            else
            {
                throw new InvalidOperationException("ConnectCurves: Failed to join curves into a single curve.");
            }

        }
        public static Curve ExtendCurveToCrvList(Curve curve, List<Curve> targetCurves, bool extensionOnly = false)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve), "ExtendCurveToCrvList: The provided curve is null.");
            }

            if (targetCurves == null || !targetCurves.Any())
            {
                throw new ArgumentNullException(nameof(targetCurves), "ExtendCurveToCrvList: The provided list of target curves is null or empty.");
            }

            List<GeometryBase> geometry = new List<GeometryBase>(targetCurves);
            Curve extendedCurve = null;

            // Get the enum values in reverse order
            var curveEnds = Enum.GetValues(typeof(CurveEnd)).Cast<CurveEnd>().Reverse();

            // Try extending the curve with different CurveEnd values in reverse order
            foreach (CurveEnd end in curveEnds)
            {
                extendedCurve = curve.Extend(end, CurveExtensionStyle.Line, geometry);
                if (extendedCurve != null)
                {
                    break;
                }
            }

            // If the extension fails, use the original curve
            if (extendedCurve == null)
            {
                extendedCurve = curve;
            }

            // If extensionOnly is true, return only the extended part
            if (extensionOnly && extendedCurve != curve)
            {
                var extendedPart = extendedCurve.Split(new double[] { curve.Domain.T1 }).LastOrDefault();
                if (extendedPart != null)
                {
                    return extendedPart;
                }
            }

            return extendedCurve;
        }
        public static Polyline SimplifyPolylineByAngle(Polyline polyline, Interval angleRange)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "SimplifyPolylineByAngle: The provided polyline is null.");
            }

            if (angleRange == Interval.Unset)
            {
                angleRange = new Interval(2, 178);
            }

            if (!angleRange.IsValid)
            {
                throw new ArgumentException("SimplifyPolylineByAngle: The provided angle range is not valid.", nameof(angleRange));
            }

            polyline = RemoveInvalidSegments(polyline);

            var nodes = CreatePolylineNodes(polyline);
            var simplifiedPoints = new List<Point3d>();

            // Maintain the first point if the polyline is open
            if (!polyline.IsClosed && nodes.Count > 0)
            {
                simplifiedPoints.Add(nodes.First().Point);
            }

            foreach (var node in nodes)
            {
                if (node.Angle.HasValue && angleRange.IncludesParameter(node.Angle.Value))
                {
                    simplifiedPoints.Add(node.Point);
                }
            }

            // Maintain the last point if the polyline is open
            if (!polyline.IsClosed && nodes.Count > 0)
            {
                simplifiedPoints.Add(nodes.Last().Point);
            }

            // Ensure the polyline is closed if the input polyline is closed
            if (polyline.IsClosed && simplifiedPoints.Count > 0)
            {
                simplifiedPoints.Add(simplifiedPoints.First());
            }

            return new Polyline(simplifiedPoints);
        }
        public static (List<Curve> shatteredCurves, List<int> originalEdgeIndices) ShatterLineLikeCurvesAtIntersectionsAndPoints(List<Curve> curves, List<Point3d> points = null)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "ShatterCurvesAtIntersectionsAndPoints: The provided list of curves is null or empty.");
            }

            List<Curve> shatteredCurves = new List<Curve>();
            List<int> originalEdgeIndices = new List<int>();

            // Combined validation and processing loop
            for (int i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                if (curve == null || !curve.IsValid || !curve.IsLinear())
                {
                    continue;
                }

                HashSet<double> intersectionParameters = new HashSet<double>();

                // Check for intersections and overlaps with other curves
                for (int j = 0; j < curves.Count; j++)
                {
                    if (i == j) continue;

                    var otherCurve = curves[j];
                    if (!otherCurve.IsLinear()) continue;


                    // Handle regular intersections
                    var intersections = Intersection.CurveCurve(curve, otherCurve, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    if (intersections != null && intersections.Count > 0)
                    {
                        foreach (var intersection in intersections)
                        {
                            if (intersection.IsPoint)
                            {
                                if (intersection.ParameterA > curve.Domain.Min && intersection.ParameterA < curve.Domain.Max)
                                {
                                    intersectionParameters.Add(intersection.ParameterA);
                                }
                            }
                        }
                    }

                }

                // Handle additional points if provided
                if (points != null)
                {
                    foreach (var point in points)
                    {
                        double t;
                        if (curve.ClosestPoint(point, out t))
                        {
                            if (point.DistanceTo(curve.PointAt(t)) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                            {
                                if (t > curve.Domain.Min && t < curve.Domain.Max)
                                {
                                    intersectionParameters.Add(t);
                                }
                            }
                        }
                    }
                }

                if (intersectionParameters.Any())
                {
                    // Sort parameters to ensure proper splitting order
                    var sortedParameters = intersectionParameters.OrderBy(p => p).ToArray();
                    var splitCurves = curve.Split(sortedParameters);
                    shatteredCurves.AddRange(splitCurves);
                    originalEdgeIndices.AddRange(Enumerable.Repeat(i, splitCurves.Length));
                }
                else
                {
                    shatteredCurves.Add(curve);
                    originalEdgeIndices.Add(i);
                }
            }

            return (shatteredCurves, originalEdgeIndices);
        }
        public static Polyline OffsetVariablePolyline(Polyline polyline, List<double> distances)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "OffsetVariablePolyline: The provided polyline is null.");
            }

            if (distances == null || distances.Count < polyline.SegmentCount)
            {
                throw new ArgumentException("OffsetVariablePolyline: The provided distances list must equal or exceed the number of polyline segments.", nameof(distances));
            }

            var segments = polyline.GetSegments();
            var offsetSegments = new List<Line>();

            // Determine the direction of the first segment
            Vector3d firstSegmentDirection = segments[0].Direction;
            firstSegmentDirection.Unitize();

            // Calculate the cross product of the first segment direction and the Z-axis of the XY plane
            Vector3d crossProduct = Vector3d.CrossProduct(firstSegmentDirection, Vector3d.ZAxis);
            crossProduct.Unitize();

            // Calculate the angle between the result vector from the cross product and the first segment direction
            double angle = Vector3d.VectorAngle(crossProduct, firstSegmentDirection);

            // If the angle is 90 degrees, flip the plane
            Plane plane = Plane.WorldXY;
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var distance = distances[i];

                // Calculate the direction vector of the segment
                Vector3d direction = segment.Direction;
                direction.Unitize();

                // Calculate the cross product of the segment direction and the Z-axis of the plane
                Vector3d offsetDirection = Vector3d.CrossProduct(direction, plane.ZAxis);
                offsetDirection.Unitize();
                offsetDirection *= distance;

                // Calculate the offset points
                Point3d offsetStart = segment.From + offsetDirection;
                Point3d offsetEnd = segment.To + offsetDirection;

                // Create the offset segment
                offsetSegments.Add(new Line(offsetStart, offsetEnd));
            }

            // Create nodes from the polyline
            var nodes = CreatePolylineNodes(polyline);

            // Assign offseted segment to node Segment
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (var segment in nodes[i].Segments)
                {
                    segment.OffsetedSegment = offsetSegments[segment.Index];
                }
            }

            // List to store the final polyline points
            var finalPolylinePoints = new List<Point3d>();

            if (!polyline.IsClosed)
            {
                // Handle open polyline
                if (nodes.Count > 0 && nodes[0].SegmentCount == 1)
                {
                    // Get the From point of the first offset segment
                    finalPolylinePoints.Add(offsetSegments[0].From);
                }

                // Iterate over nodes to find intersecting points
                foreach (var node in nodes)
                {
                    if (node.SegmentCount == 2)
                    {
                        var segment1 = node.Segments[0].OffsetedSegment;
                        var segment2 = node.Segments[1].OffsetedSegment;

                        if (node.Angle == 0)
                        {
                            // Get the connection points between the two segments
                            finalPolylinePoints.Add(segment1.To);
                            finalPolylinePoints.Add(segment2.From);
                        }
                        else
                        {
                            // Find the intersecting point using LineLine
                            double a, b;
                            if (Intersection.LineLine(segment1, segment2, out a, out b))
                            {
                                var intersectionPoint = segment1.PointAt(a);
                                finalPolylinePoints.Add(intersectionPoint);
                            }
                        }
                    }
                }

                if (nodes.Count > 0 && nodes[nodes.Count - 1].SegmentCount == 1)
                {
                    // Get the To point of the last offset segment
                    finalPolylinePoints.Add(offsetSegments[offsetSegments.Count - 1].To);
                }
            }
            else
            {
                // Handle closed polyline
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node.SegmentCount == 2)
                    {
                        var segment1 = node.Segments[0].OffsetedSegment;
                        var segment2 = node.Segments[1].OffsetedSegment;

                        if (node.Angle == 0)
                        {
                            // Get the connection points between the two segments
                            finalPolylinePoints.Add(segment1.To);
                            finalPolylinePoints.Add(segment2.From);
                        }
                        else
                        {
                            // Find the intersecting point using LineLine
                            double a, b;
                            if (Intersection.LineLine(segment1, segment2, out a, out b))
                            {
                                var intersectionPoint = segment1.PointAt(a);
                                finalPolylinePoints.Add(intersectionPoint);
                            }
                        }
                    }
                }

                // Connect start point and end point of the result polyline
                finalPolylinePoints.Add(finalPolylinePoints.First());
            }

            // Create the final polyline from the points
            Polyline finalPolyline = new Polyline(finalPolylinePoints);
            return finalPolyline;
        }
        public static Polyline RemoveInvalidSegments(Polyline polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "RemoveInvalidSegments: The provided polyline is null.");
            }

            var validSegments = new List<Line>();

            foreach (var segment in polyline.GetSegments())
            {
                if (segment.IsValid)
                {
                    validSegments.Add(segment);
                }
            }

            if (validSegments.Count == 0)
            {
                throw new InvalidOperationException("RemoveInvalidSegments: No valid segments found in the provided polyline.");
            }

            var validPolyline = new Polyline(validSegments.SelectMany(s => new[] { s.From, s.To }).Distinct().ToList());

            if (polyline.IsClosed && validPolyline.Count > 0)
            {
                validPolyline.Add(validPolyline.First());
            }

            return validPolyline;
        }
        public static bool ClosedCurveBrepRegionDifference(List<Curve> inputCurves, Brep brep, double tolerance, out List<Curve> resultRegions)
        {
            resultRegions = new List<Curve>();

            // Validate input curves
            if (inputCurves == null || inputCurves.Count == 0)
                throw new ArgumentNullException(nameof(inputCurves), "Input curve list is null or empty.");
            foreach (var inputCurve in inputCurves)
            {
                if (inputCurve == null)
                    throw new ArgumentNullException(nameof(inputCurves), "One of the input curves is null.");
                if (!inputCurve.IsClosed)
                    throw new ArgumentException("All input curves must be closed.", nameof(inputCurves));
                if (!inputCurve.IsPlanar())
                    throw new ArgumentException("All input curves must be planar.", nameof(inputCurves));
            }

            // Validate Brep
            if (brep == null)
                throw new ArgumentNullException(nameof(brep), "Input Brep is null.");
            if (!brep.IsSolid)
                throw new ArgumentException("Input Brep must be solid (closed).", nameof(brep));

            // Use the plane of the first curve (assume all curves are coplanar)
            Plane curvePlane;
            if (!inputCurves[0].TryGetPlane(out curvePlane))
                throw new ArgumentException("Failed to get plane from input curves.", nameof(inputCurves));

            // Intersect the Brep with the plane of the curves
            Curve[] intersectionCurves;
            Point3d[] intersectionPoints;
            Intersection.BrepPlane(brep, curvePlane, tolerance, out intersectionCurves, out intersectionPoints);

            if (intersectionCurves == null || intersectionCurves.Length == 0)
                return false;

            // Find intersection curves that are closed and planar
            var closedIntersectionCurves = intersectionCurves
                .Where(c => c.IsClosed && c.IsPlanar())
                .ToList();

            if (closedIntersectionCurves.Count == 0)
                return false;

            // For each intersection curve, use ClosedCurveRegionDifference
            foreach (var subtractor in closedIntersectionCurves)
            {
                List<Curve> regions;
                if (ClosedCurveRegionDifference(inputCurves, subtractor, tolerance, out regions))
                {
                    resultRegions.AddRange(regions);
                }
            }

            return resultRegions.Count > 0;
        }
        public static bool ClosedCurveRegionDifference(List<Curve> inputCurves, Curve subtractor, double tolerance, out List<Curve> resultRegions)
        {
            resultRegions = new List<Curve>();

            // Validate input curves
            if (inputCurves == null || inputCurves.Count == 0)
            {
                resultRegions = new List<Curve>();
                return false;
            }
            foreach (var inputCurve in inputCurves)
            {
                if (inputCurve == null)
                {
                    resultRegions = new List<Curve>();
                    return false;
                }
                if (!inputCurve.IsClosed)
                {
                    resultRegions = new List<Curve>();
                    return false;
                }
                if (!inputCurve.IsPlanar())
                {
                    resultRegions = new List<Curve>();
                    return false;
                }
            }

            // Validate subtractor
            if (subtractor == null)
            {
                resultRegions = new List<Curve>();
                return false;
            }
            if (!subtractor.IsClosed)
            {
                resultRegions = new List<Curve>();
                return false;
            }
            if (!subtractor.IsPlanar())
            {
                resultRegions = new List<Curve>();
                return false;
            }

            // Check all curves are co-planar
            Plane basePlane;
            if (!inputCurves[0].TryGetPlane(out basePlane))
            {
                resultRegions = new List<Curve>();
                return false;
            }

            foreach (var inputCurve in inputCurves.Skip(1))
            {
                Plane curvePlane;
                if (!inputCurve.TryGetPlane(out curvePlane) || !basePlane.IsCoplanar(curvePlane, tolerance))
                {
                    resultRegions = new List<Curve>();
                    return false;
                }
            }
            {
                Plane subtractorPlane;
                if (!subtractor.TryGetPlane(out subtractorPlane) || !basePlane.IsCoplanar(subtractorPlane, tolerance))
                {
                    resultRegions = new List<Curve>();
                    return false;
                }
            }


            // Combine all input curves and the subtractor for region boolean
            var allCurves = new List<Curve>(inputCurves) { subtractor };

            // Use region boolean to get all regions
            var regions = Curve.CreateBooleanRegions(allCurves, basePlane, false, tolerance);
            if (regions == null || regions.RegionCount == 0)
                return false;

            // For each region, use IdentifyPointInClosedPolyline to get a point inside, and check if it's outside the subtractor
            for (int i = 0; i < regions.RegionCount; i++)
            {
                var regionCurves = regions.RegionCurves(i);
                if (regionCurves == null || regionCurves.Length == 0)
                    continue;

                var regionCurve = regionCurves[0];
                if (!regionCurve.IsClosed)
                    continue;

                // Use IdentifyPointInClosedPolyline to get a point inside the region
                var pointInRegion = IdentifyPointInClosedPolyline(regionCurve, 0.5, false);

                // If the point is outside the subtractor, add to result
                if (subtractor.Contains(pointInRegion, basePlane, tolerance) == PointContainment.Outside)
                    resultRegions.Add(regionCurve);
            }

            return resultRegions.Count > 0;
        }
    }
}
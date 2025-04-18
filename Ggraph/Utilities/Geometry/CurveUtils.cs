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
    public static class CurveUtils
    {
        public static double CalculateCurveArea(List<Curve> boundary)
        {
            // Filter out any curves that are not closed or not planar
            var validCurves = boundary.Where(curve => curve.IsClosed && curve.IsPlanar()).ToList();

            if (!validCurves.Any())
            {
                return -99999999999;
            }

            double totalArea = 0;

            // Create a planar surface from the valid boundary curves
            Brep[] breps = Brep.CreatePlanarBreps(validCurves, 0.001);

            if (breps != null)
            {
                // Calculate the area of the surface
                AreaMassProperties amp = AreaMassProperties.Compute(breps);
                if (amp != null)
                {
                    totalArea = amp.Area;
                }
            }

            return totalArea;
        }


        public static double GetCurveXDimension(List<Curve> curves, bool rotate90 = false)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "GetCurveXDimension: The provided list of curves is null or empty.");
            }

            double minX = double.MaxValue;
            double maxX = double.MinValue;

            foreach (var curve in curves)
            {
                if (curve != null)
                {
                    Curve transformedCurve = curve;
                    if (rotate90)
                    {
                        // Rotate the curve by 90 degrees around the XY plane
                        Transform rotation = Transform.Rotation(Math.PI / 2, Plane.WorldXY.Origin);
                        transformedCurve = curve.DuplicateCurve();
                        transformedCurve.Transform(rotation);
                    }

                    BoundingBox bbox = transformedCurve.GetBoundingBox(false);
                    if (bbox.Min.X < minX) minX = bbox.Min.X;
                    if (bbox.Max.X > maxX) maxX = bbox.Max.X;
                }
                else
                {
                    throw new ArgumentNullException(nameof(curve), "GetCurveXDimension: The provided curve is null.");
                }
            }

            return Math.Round(maxX - minX, 2);
        }

        public static void GetCurveYDimension(List<Curve> curves, Plane basePlane, out double topY, out double botY)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "GetCurveYDimension: The provided list of curves is null or empty.");
            }

            topY = double.MinValue;
            botY = double.MaxValue;

            foreach (var curve in curves)
            {
                if (curve != null)
                {
                    BoundingBox bbox = curve.GetBoundingBox(basePlane);
                    if (bbox.Min.Y < botY) botY = bbox.Min.Y;
                    if (bbox.Max.Y > topY) topY = bbox.Max.Y;
                }
                else
                {
                    throw new ArgumentNullException(nameof(curve), "GetCurveYDimension: The provided curve is null.");
                }
            }

            botY = Math.Abs(Math.Round(botY, 2));
            topY = Math.Abs(Math.Round(topY, 2));
        }

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

        public static List<CurveWithContainmentCount> CreateCurveWithContainment(List<Curve> curves)
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

        public static List<Curve> GetMostOuterCurves(List<CurveWithContainmentCount> curvesWithContainmentCount)
        {
            if (curvesWithContainmentCount == null || !curvesWithContainmentCount.Any())
            {
                throw new ArgumentNullException(nameof(curvesWithContainmentCount), "GetOuterCurves: The provided list of curves with containment count is null or empty.");
            }
            List<Curve> outerCurves = new List<Curve>();
            foreach (var curveWithContainmentCount in curvesWithContainmentCount)
            {
                if (curveWithContainmentCount.ContainmentCount == 0)
                {
                    outerCurves.Add(curveWithContainmentCount.Curve);
                }
            }
            return outerCurves;
        }

        public static List<CurveGroup> GroupCurvesByContainment(List<CurveWithContainmentCount> curvesWithContainmentCount)
        {
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
        public static bool AreCurvesIntersecting(List<Curve> curves)
        {
            if (curves == null || curves.Count < 2)
            {
                return false; // No intersections possible with less than 2 curves
            }

            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    if (curves[i] == null || curves[j] == null)
                    {
                        continue; // Skip null curves
                    }

                    var intersectionEvents = Intersection.CurveCurve(curves[i], curves[j], 0.001, 0.001);
                    if (intersectionEvents != null && intersectionEvents.Count > 0)
                    {
                        return true; // Intersection found
                    }
                }
            }

            return false; // No intersections found
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
                throw new InvalidOperationException("IdentifyPointInClosedPolyline: Failed to convert the curve to a polyline.");
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

        public static bool IsConvex(Polyline polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "IsConvex: The provided polyline is null.");
            }

            // Create a PolylineCurve from the input polyline
            PolylineCurve polylineCurve = new PolylineCurve(polyline);

            // Create the convex hull from the control points of the input polyline
            int[] hullIndices;
            Point2d[] ControlPoints = polyline.Select(pt => new Point2d(pt.X, pt.Y)).ToArray();
            PolylineCurve convexHullCurve = PolylineCurve.CreateConvexHull2d(ControlPoints, out hullIndices);
            if (convexHullCurve == null)
            {
                throw new InvalidOperationException("IsConvex: Failed to create the convex hull.");
            }

            // Compare the number of control points
            int inputControlPointCount = polyline.Count;
            int convexHullControlPointCount = convexHullCurve.PointCount;

            // If the input polyline has the same control points than the convex hull, it is convex
            return inputControlPointCount == convexHullControlPointCount;
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

        // Method to filter most outer curve, even and odd curve
        public static void FilterCurvesByContainment(List<Curve> curves, out List<Curve> outerCurves, out List<Curve> evenCurves, out List<Curve> oddCurves)
        {
            if (curves == null || !curves.Any())
            {
                throw new ArgumentNullException(nameof(curves), "FilterCurvesByContainment: The provided list of curves is null or empty.");
            }

            // Step 1: Create CurveWithContainmentCount
            var curvesWithContainmentCount = CreateCurveWithContainment(curves);

            // Step 2: Group curves by containment
            var curveGroups = GroupCurvesByContainment(curvesWithContainmentCount);

            // Initialize the output lists
            outerCurves = new List<Curve>();
            evenCurves = new List<Curve>();
            oddCurves = new List<Curve>();

            // Populate the output lists based on containment count
            foreach (var curveWithContainment in curvesWithContainmentCount)
            {
                if (curveWithContainment.ContainmentCount == 0)
                {
                    outerCurves.Add(curveWithContainment.Curve);
                }
                else if (curveWithContainment.ContainmentCount % 2 == 0)
                {
                    evenCurves.Add(curveWithContainment.Curve);
                }
                else
                {
                    oddCurves.Add(curveWithContainment.Curve);
                }
            }
        }


        // Offset polyline segment with list of distances
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


        public static List<PolylineNode> CreatePolylineNodes(Polyline polyline)
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

        // Sort list Concurrent-lines by angles
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
        public static void ConstructLandscapeFromGraph(Graph graph, Curve siteBoundary, List<Curve> buildingCoverBoundaries, List<Curve> spaceBoundaries, out List<Curve> RoadLine, out List<Curve> Pavement, out List<Curve> Green)
        {
            RoadLine = new List<Curve>();
            Pavement = new List<Curve>();
            Green = new List<Curve>();

            // Check if the graph is null or has no edges
            if (graph == null || graph.QuickGraphObj == null || !graph.QuickGraphObj.Edges.Any())
            {
                throw new ArgumentNullException(nameof(graph), "ConstructLandscapeFromGraph: The provided graph is null or has no edges.");
            }

            // Convert siteBoundary to polyline with tolerance = 0.01
            Polyline sitePolyline;
            if (!siteBoundary.TryGetPolyline(out sitePolyline))
            {
                sitePolyline = siteBoundary.ToPolyline(0.01, 0, 0, 0).ToPolyline();
            }

            List<Curve> edgeCurves = new List<Curve>();
            List<double> edgeWidths = new List<double>();
            // Collect all edge curves and their corresponding widths
            foreach (var edge in graph.QuickGraphObj.Edges)
            {
                if (edge == null || !edge.Attributes.ContainsKey("Width"))
                {
                    throw new ArgumentException("ConstructLandscapeFromGraph: Each edge must have a 'Width' key with a valid value.", nameof(graph));
                }

                double width = Convert.ToDouble(edge.Attributes["Width"]);
                if (width <= 0)
                {
                    throw new ArgumentException("ConstructLandscapeFromGraph: The 'Width' value must be greater than zero.", nameof(graph));
                }

                edgeCurves.Add(edge.EdgeCurve);
                edgeWidths.Add(width / 2.0);
            }

            List<Polyline> outerPolylines = new List<Polyline>();
            List<List<double>> offsetDistancesList = new List<List<double>>();
            // Process each node in the graph
            foreach (var node in graph.QuickGraphObj.Vertices)
            {
                if (node.Valence > 1)
                {
                    // Get connected edges and ensure all lines have their start point as the node
                    var connectedEdges = graph.QuickGraphObj.AdjacentEdges(node).ToList();
                    List<Line> lines = new List<Line>();
                    foreach (var edge in connectedEdges)
                    {
                        var startPoint = edge.EdgeCurve.PointAtStart;
                        var endPoint = edge.EdgeCurve.PointAtEnd;

                        if (startPoint != node.Point)
                        {
                            lines.Add(new Line(endPoint, startPoint));
                        }
                        else
                        {
                            lines.Add(new Line(startPoint, endPoint));
                        }
                    }

                    // Sort connected lines by angle
                    var (sortedLines, sortedIndices) = SortLinesByAngle(lines);

                    // Create polylines from the sorted lines and the node point
                    for (int i = 0; i < sortedLines.Count; i++)
                    {
                        var line1 = sortedLines[i];
                        var line2 = sortedLines[(i + 1) % sortedLines.Count];

                        Polyline polyline = new Polyline();
                        polyline.Add(line1.To);
                        polyline.Add(node.Point);
                        polyline.Add(line2.To);
                        outerPolylines.Add(polyline);
                    }

                    // Get the offset distances corresponding to the edge widths
                    foreach (var polyline in outerPolylines)
                    {
                        List<double> offsetDistances = new List<double>();
                        var segments = polyline.GetSegments();
                        foreach (var segment in segments)
                        {
                            foreach (var edge in connectedEdges)
                            {
                                if (segment.From == edge.EdgeCurve.PointAtStart && segment.To == edge.EdgeCurve.PointAtEnd ||
                                    segment.From == edge.EdgeCurve.PointAtEnd && segment.To == edge.EdgeCurve.PointAtStart)
                                {
                                    offsetDistances.Add(Convert.ToDouble(edge.Attributes["Width"]) / 2.0);
                                    break;
                                }
                            }
                        }
                        offsetDistancesList.Add(offsetDistances);
                    }

                    // Offset the polylines by the corresponding distances
                    for (int i = 0; i < outerPolylines.Count; i++)
                    {
                        Polyline offsetPolyline = OffsetVariablePolyline(outerPolylines[i], offsetDistancesList[i]);
                        Curve nurbsCurve = offsetPolyline.ToNurbsCurve();

                        // Fillet the Nurbs curve
                        Curve filletedCurve = Curve.CreateFilletCornersCurve(nurbsCurve, 2, 0.001, 0.001);
                        if (filletedCurve != null)
                        {
                            RoadLine.Add(filletedCurve);
                        }
                        else
                        {
                            RoadLine.Add(nurbsCurve);
                        }
                    }

                    // Clear variable value
                    outerPolylines.Clear();
                    offsetDistancesList.Clear();
                }
                else if (node.Valence == 1)
                {
                    // Handle nodes with valence = 1
                    var edge = graph.QuickGraphObj.AdjacentEdges(node).First();
                    var direction = edge.EdgeCurve.PointAtEnd - edge.EdgeCurve.PointAtStart;
                    var crossProduct = Vector3d.CrossProduct(direction, Vector3d.ZAxis);
                    crossProduct.Unitize();
                    double width = Convert.ToDouble(edge.Attributes["Width"]) / 2.0;

                    // Draw lines in both directions of the cross product
                    Line leftLine = new Line(node.Point, node.Point + crossProduct * width);
                    Line rightLine = new Line(node.Point, node.Point - crossProduct * width);

                    RoadLine.Add(leftLine.ToNurbsCurve());
                    RoadLine.Add(rightLine.ToNurbsCurve());
                }
            }

            // Get the outer
            var outerRegions = Curve.CreateBooleanRegions(RoadLine, Plane.WorldXY, true, 0.001);
            var outerRegionsCurve = new List<Curve>();

            for (int i = 0; i < outerRegions.RegionCount; i++)
            {
                outerRegionsCurve.AddRange(outerRegions.RegionCurves(i));
            }

            // Convert outerRegionsCurve to polyline and Simplify
            List<Polyline> outerRegionPolylines = new List<Polyline>();
            List<Curve> simplifiedCurves = new List<Curve>();
            foreach (var curve in outerRegionsCurve)
            {
                Polyline polyline = new Polyline(curve.ToPolyline(0.02, 0, 0, 0).ToPolyline());
                if (curve != null)
                {
                    // Simplify polyline using angle range 1-179 degrees
                    var simplifiedPolyline = SimplifyPolylineByAngle(polyline, new Interval(2, 178));
                    simplifiedCurves.Add(simplifiedPolyline.ToNurbsCurve());
                }
                else
                {
                    throw new InvalidOperationException("Failed to convert curve to polyline.");
                }
            }

            // Clear the RoadLine and add the simplified curves
            RoadLine.Clear();
            RoadLine.AddRange(simplifiedCurves);

            // Create Pavement Regions from outer RoadLine
            /// Get region difference between siteBoundary and outerRoadLine
            var regionDifference = Curve.CreateBooleanDifference(sitePolyline.ToNurbsCurve(), simplifiedCurves, 0.01);

            /// Offset simplifiedCurves with value = 1.2
            var offsetSimplifiedCurves = new List<Curve>();
            foreach (var curve in simplifiedCurves)
            {
                var offsetCurves = curve.Offset(Plane.WorldXY, 1.2, 0.01, CurveOffsetCornerStyle.Sharp);
                if (offsetCurves != null)
                {
                    offsetSimplifiedCurves.AddRange(offsetCurves);
                }
            }
            /// Get region intersection between regionDifference and offset
            foreach (var curve in regionDifference)
            {
                var regionIntersection = Curve.CreateBooleanIntersection(curve, offsetSimplifiedCurves.First(), 0.01);
                Pavement.AddRange(regionIntersection);
            }

            // Get region difference between siteBoundary with offsetSimplifiedCurves & optional spaceBoundaries
            var allConstraints = new List<Curve>(offsetSimplifiedCurves);

            // Add building cover boundaries as constraints if provided
            if (buildingCoverBoundaries != null && buildingCoverBoundaries.Any())
            {
                allConstraints.AddRange(buildingCoverBoundaries);
            }

            // Add space boundaries as constraints if provided
            if (spaceBoundaries != null && spaceBoundaries.Any())
            {
                allConstraints.AddRange(spaceBoundaries);
            }

            // Create combined constraints for boolean difference
            var combinedConstraints = Curve.CreateBooleanUnion(allConstraints, 0.01);
            if (combinedConstraints != null && combinedConstraints.Length > 0)
            {
                var LandscaperegionDifference = Curve.CreateBooleanDifference(
                    sitePolyline.ToNurbsCurve(),
                    combinedConstraints,
                    0.01
                );
                if (LandscaperegionDifference != null)
                {
                    Green.AddRange(LandscaperegionDifference);
                }
            }
            else
            {
                var LandscaperegionDifference = Curve.CreateBooleanDifference(
                    sitePolyline.ToNurbsCurve(),
                    offsetSimplifiedCurves,
                    0.01
                );
                if (LandscaperegionDifference != null)
                {
                    Green.AddRange(LandscaperegionDifference);
                }
            }


            //////----------------------------------------


            // Get all edges of graph and get holeRegion
            var graphEdges = graph.QuickGraphObj.Edges.ToList();
            List<Curve> graphEdgesCurves = new List<Curve>();
            foreach (var edge in graphEdges)
            {
                if (edge.EdgeCurve != null)
                {
                    graphEdgesCurves.Add(edge.EdgeCurve);
                }
            }
            var holeRegions = Curve.CreateBooleanRegions(graphEdgesCurves, Plane.WorldXY, false, 0.001);
            var holeRegionsCurve = new List<Curve>();

            for (int i = 0; i < holeRegions.RegionCount; i++)
            {
                var regionCurves = holeRegions.RegionCurves(i);
                foreach (var curve in regionCurves)
                {
                    if (curve.ClosedCurveOrientation(Plane.WorldXY) != CurveOrientation.Clockwise)
                    {
                        curve.Reverse();
                    }
                    holeRegionsCurve.Add(curve);
                }
            }

            // Convert holeRegionsCurve to polyline
            List<Polyline> holeRegionPolylines = new List<Polyline>();
            foreach (var curve in holeRegionsCurve)
            {
                Polyline polyline;
                if (curve.TryGetPolyline(out polyline))
                {

                    holeRegionPolylines.Add(polyline);
                }
                else
                {
                    throw new InvalidOperationException("Failed to convert curve to polyline.");
                }
            }

            List<List<double>> holeOffsetDistancesList = new List<List<double>>();
            // Get the offset distances corresponding to the HOLE edge widths
            foreach (var polyline in holeRegionPolylines)
            {
                List<double> offsetDistances = new List<double>();
                var segments = polyline.GetSegments();
                foreach (var segment in segments)
                {
                    foreach (var edge in graphEdges)
                    {
                        if (segment.From == edge.EdgeCurve.PointAtStart && segment.To == edge.EdgeCurve.PointAtEnd ||
                            segment.From == edge.EdgeCurve.PointAtEnd && segment.To == edge.EdgeCurve.PointAtStart)
                        {
                            offsetDistances.Add(Convert.ToDouble(edge.Attributes["Width"]) / 2.0);
                            break;
                        }
                    }
                }
                holeOffsetDistancesList.Add(offsetDistances);
            }

            // Convert holeOffsetPolyline to curve and use method CreateBooleanRegions to calculate holeRegion
            List<Curve> holeOffsetCurves = new List<Curve>();
            for (int i = 0; i < holeRegionPolylines.Count; i++)
            {
                Polyline holeOffsetPolyline = OffsetVariablePolyline(holeRegionPolylines[i], holeOffsetDistancesList[i]);
                holeOffsetCurves.Add(holeOffsetPolyline.ToNurbsCurve());
            }

            // Calculate hole regions using CreateBooleanRegions
            var holeRegionsResult = Curve.CreateBooleanRegions(holeOffsetCurves, Plane.WorldXY, true, 0.001);
            if (holeRegionsResult != null && holeRegionsResult.RegionCount > 0)
            {
                holeRegionsCurve.Clear();
                for (int i = 0; i < holeRegionsResult.RegionCount; i++)
                {
                    var regionCurves = holeRegionsResult.RegionCurves(i);
                    if (regionCurves != null && regionCurves.Length > 0)
                    {
                        foreach (var regionCurve in regionCurves)
                        {
                            // Fillet the Nurbs curve
                            Curve filletedCurve = Curve.CreateFilletCornersCurve(regionCurve, 3, 0.001, 0.001);
                            if (filletedCurve != null)
                            {
                                // Convert the filleted curve to a polyline
                                Polyline polyline = filletedCurve.ToPolyline(0.02, 0, 0, 0).ToPolyline();
                                RoadLine.Add(polyline.ToNurbsCurve());
                                var PolylineCurve = polyline.ToNurbsCurve();
                                var polylineOffsets = PolylineCurve.Offset(Plane.WorldXY, -1.2, 0.01, CurveOffsetCornerStyle.Sharp);
                                if (polylineOffsets != null)
                                {
                                    Pavement.AddRange(polylineOffsets);
                                }
                                Pavement.Add(polyline.ToNurbsCurve());
                            }
                            else
                            {
                                RoadLine.Add(regionCurve);
                            }
                        }
                    }
                }
            }
            RoadLine.AddRange(holeRegionsCurve);
        }

        /// <summary>
        /// Creates a rectangle representing the bounding box of all input curves with optional padding.
        /// </summary>
        /// <param name="curves">Collection of curves to calculate bounding box for.</param>
        /// <param name="padding">Optional padding percentage (0.05 = 5% padding on each side).</param>
        /// <returns>A Rectangle representing the bounding box of all curves.</returns>
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

        /// <summary>
        /// Converts a System.Drawing.Rectangle to a Rhino.Geometry.Curve (specifically a PolylineCurve).
        /// </summary>
        /// <param name="rectangle">The rectangle to convert.</param>
        /// <returns>A closed curve representing the rectangle.</returns>
        public static Curve RectangleToCurve(Rectangle rectangle)
        {
            if (rectangle.IsEmpty)
            {
                throw new ArgumentException("RectangleToCurve: The provided rectangle is empty.", nameof(rectangle));
            }

            // Create a polyline from the rectangle vertices
            Polyline polyline = new Polyline();
            polyline.Add(new Point3d(rectangle.Left, rectangle.Top, 0));
            polyline.Add(new Point3d(rectangle.Right, rectangle.Top, 0));
            polyline.Add(new Point3d(rectangle.Right, rectangle.Bottom, 0));
            polyline.Add(new Point3d(rectangle.Left, rectangle.Bottom, 0));
            polyline.Add(new Point3d(rectangle.Left, rectangle.Top, 0)); // Close the polyline

            // Convert the polyline to a curve
            return polyline.ToNurbsCurve();
        }

        // method to orient a curve from source to target plane
        public static Curve OrientCurve(Curve curve, Plane sourcePlane, Plane targetPlane)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve), "OrientCurve: The provided curve is null.");
            }

            // Check if the curve is a polyline
            Polyline polyline;
            if (curve.TryGetPolyline(out polyline))
            {
                // Orient the polyline
                Transform xform = Transform.PlaneToPlane(sourcePlane, targetPlane);
                polyline.Transform(xform);

                // Convert the oriented polyline back to a curve
                Curve orientedCurve = polyline.ToNurbsCurve();

                // Check if the closed status of the oriented curve matches the original curve
                if (orientedCurve.IsClosed != curve.IsClosed)
                {
                    throw new InvalidOperationException("OrientCurve: The resulting curve does not have the same closed status as the original curve.");
                }

                return orientedCurve;
            }
            else
            {
                // If not a polyline, proceed with the original logic
                Transform xform = Transform.PlaneToPlane(sourcePlane, targetPlane);
                Curve orientedCurve = curve.DuplicateCurve();
                orientedCurve.Transform(xform);

                // Check if the closed status of the oriented curve matches the original curve
                if (orientedCurve.IsClosed != curve.IsClosed)
                {
                    throw new InvalidOperationException("OrientCurve: The resulting curve does not have the same closed status as the original curve.");
                }

                return orientedCurve;
            }
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


        //public static void DrawParking (Curve Boundary,List<Curve> Constrain, out List<Curve> parkingCurves)
        //{
        //    parkingCurves = new List<Curve>();

        //    // Check if the boundary is null or not closed
        //    if (Boundary == null || !Boundary.IsClosed)
        //    {
        //        throw new ArgumentException("DrawParking: The provided boundary curve is null or not closed.", nameof(Boundary));
        //    }

        //    // Ensure the Boundary is clockwise
        //    if (Boundary.ClosedCurveOrientation(Plane.WorldXY) != CurveOrientation.Clockwise)
        //    {
        //        Boundary.Reverse();
        //    }

        //    // Offset to create the parking area
        //    var offsetCurves = Boundary.Offset(Plane.WorldXY, -5, 0.01, CurveOffsetCornerStyle.Sharp);

        //}
    }
}
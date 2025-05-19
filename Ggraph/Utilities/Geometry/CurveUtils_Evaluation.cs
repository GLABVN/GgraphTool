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
        public static List<double> GetPolylineSegmentAngles(Polyline polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline), "GetPolylineSegmentAngles: The provided polyline is null.");
            }

            if (polyline.Count < 3)
            {
                throw new ArgumentException("GetPolylineSegmentAngles: The polyline must have at least 3 points to calculate angles.", nameof(polyline));
            }

            List<double> angles = new List<double>();

            for (int i = 1; i < polyline.Count - 1; i++)
            {
                // Get the direction vectors of the two consecutive segments
                Vector3d direction1 = polyline[i] - polyline[i - 1];
                Vector3d direction2 = polyline[i + 1] - polyline[i];

                // Normalize the direction vectors
                direction1.Unitize();
                direction2.Unitize();

                // Calculate the angle between the two vectors
                double angle = Vector3d.VectorAngle(direction1, direction2) * (180.0 / Math.PI); // Convert to degrees

                angles.Add(angle);
            }

            return angles;
        }
    }
}
﻿using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Glab.Utilities;
using Glab.C_Graph;

namespace Glab.C_Graph.Tools
{
    public class SkeletonizeBoundary : GH_Component
    {
        public SkeletonizeBoundary()
          : base("Skeletonize Boundary", "",
              "Generates skeleton curves and graph using Voronoi methodology",
              "Glab", "Graph")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary Curve", "C", "Curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Division Length", "D", "Length to divide the polyline", GH_ParamAccess.item, 4.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Skeleton Curves", "SK", "Skeleton Curves generated from Voronoi diagram", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Skeleton Graphs", "G", "Skeleton Graphs generated from Voronoi diagram", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Unique Lines", "UL", "Unique Voronoi lines before trimming", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve the input curves
            GH_Structure<GH_Curve> curveTree = new GH_Structure<GH_Curve>();
            if (!DA.GetDataTree(0, out curveTree)) return;

            // Retrieve the division length
            double divisionLength = 4.0;
            DA.GetData(1, ref divisionLength);

            // Validate the input tree structure (self-validate, as in ConstructSpace)
            curveTree = TreeUtils.ValidateTreeStructure(curveTree, curveTree);

            // Prepare the output trees
            GH_Structure<GH_Curve> skeletonOutputTree = new GH_Structure<GH_Curve>();
            GH_Structure<GH_ObjectWrapper> graphOutputTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_Curve> uniqueLinesOutputTree = new GH_Structure<GH_Curve>();

            // Iterate through each branch in the tree
            for (int pathIndex = 0; pathIndex < curveTree.Paths.Count; pathIndex++)
            {
                GH_Path path = curveTree.Paths[pathIndex];

                // Use ExtractBranchData to get the curves for this branch
                List<Curve> curves = TreeUtils.ExtractBranchData(curveTree, pathIndex);

                // Collect all valid polylines in this branch
                List<Polyline> polylines = new List<Polyline>();
                foreach (Curve curve in curves)
                {
                    if (curve is PolylineCurve polylineCurve)
                    {
                        Polyline polyline;
                        if (polylineCurve.TryGetPolyline(out polyline))
                        {
                            polylines.Add(polyline);
                        }
                    }
                    else
                    {
                        // Convert any other curve type to a polyline using the utility method
                        Polyline poly = CurveUtils.ConvertCurveToPolyline(curve);
                        polylines.Add(poly);
                    }
                }

                if (polylines.Count > 0)
                {
                    // Generate skeleton curves, graphs, and unique lines for the whole branch
                    List<Polyline> skeletonPolylines;
                    List<Graph> skeletonGraphs;
                    List<Curve> uniqueLines;
                    VoronoiSkeleton.ExtractVoronoiSkeleton(polylines, out skeletonPolylines, out skeletonGraphs, out uniqueLines, divisionLength);

                    // Add the skeleton curves to the output tree
                    foreach (Polyline skeletonPolyline in skeletonPolylines)
                    {
                        skeletonOutputTree.Append(new GH_Curve(skeletonPolyline.ToNurbsCurve()), path);
                    }

                    // Add the skeleton graphs to the output tree
                    if (skeletonGraphs != null)
                    {
                        foreach (var skeletonGraph in skeletonGraphs)
                        {
                            graphOutputTree.Append(new GH_ObjectWrapper(skeletonGraph), path);
                        }
                    }

                    // Add the unique lines to the output tree
                    if (uniqueLines != null)
                    {
                        foreach (Curve uniqueLine in uniqueLines)
                        {
                            uniqueLinesOutputTree.Append(new GH_Curve(uniqueLine), path);
                        }
                    }
                }
            }

            // Set the output data
            DA.SetDataTree(0, skeletonOutputTree);
            DA.SetDataTree(1, graphOutputTree);
            DA.SetDataTree(2, uniqueLinesOutputTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid
        {
            get { return new Guid("DF78790C-3FBC-4745-9667-215CBEF34805"); }
        }
    }
}
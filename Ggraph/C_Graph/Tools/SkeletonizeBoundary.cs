using System;
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
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Retrieve the input curves
            GH_Structure<GH_Curve> curveTree = new GH_Structure<GH_Curve>();
            if (!DA.GetDataTree(0, out curveTree)) return;

            // Retrieve the division length
            double divisionLength = 4.0;
            DA.GetData(1, ref divisionLength);

            // Prepare the output trees
            GH_Structure<GH_Curve> skeletonOutputTree = new GH_Structure<GH_Curve>();
            GH_Structure<GH_ObjectWrapper> graphOutputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through each branch in the tree
            foreach (GH_Path path in curveTree.Paths)
            {
                List<GH_Curve> branch = curveTree.get_Branch(path).Cast<GH_Curve>().ToList();
                List<Curve> curves = branch.Select(c => c.Value).ToList();

                // Process each curve in the branch
                foreach (Curve curve in curves)
                {
                    if (curve is PolylineCurve polylineCurve)
                    {
                        Polyline polyline;
                        if (polylineCurve.TryGetPolyline(out polyline))
                        {
                            // Generate both skeleton curves and graph
                            List<Polyline> skeletonPolylines;
                            Graph skeletonGraph;
                            VoronoiSkeleton.ExtractVoronoiSkeleton(polyline, out skeletonPolylines, out skeletonGraph, divisionLength);

                            // Add the skeleton curves to the output tree
                            foreach (Polyline skeletonPolyline in skeletonPolylines)
                            {
                                skeletonOutputTree.Append(new GH_Curve(skeletonPolyline.ToNurbsCurve()), path);
                            }

                            // Add the skeleton graph to the output tree
                            if (skeletonGraph != null)
                            {
                                graphOutputTree.Append(new GH_ObjectWrapper(skeletonGraph), path);
                            }
                        }
                    }
                }
            }

            // Set the output data
            DA.SetDataTree(0, skeletonOutputTree);
            DA.SetDataTree(1, graphOutputTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid
        {
            get { return new Guid("DF78790C-3FBC-4745-9667-215CBEF34805"); }
        }
    }
}
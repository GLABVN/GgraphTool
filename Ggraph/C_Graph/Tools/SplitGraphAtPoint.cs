using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Glab.Utilities;

namespace Glab.C_Graph.Tools
{
    public class SplitGraphAtPoint : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SplitGraphAtPoint class.
        /// </summary>
        public SplitGraphAtPoint()
          : base("Split Graph At Point", "SplitGraph",
              "Splits the input graphs at the specified points",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to split", GH_ParamAccess.tree);
            pManager.AddPointParameter("Points", "P", "Tree of points to split the graphs at", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Subgraphs", "SG", "Subgraphs resulting from the split", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_Point> pointTree = new GH_Structure<GH_Point>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out pointTree)) return;

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            pointTree = TreeUtils.SimplifyTree(pointTree);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> outputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input tree
            foreach (GH_Path path in graphTree.Paths)
            {
                // Get branches for the current path
                List<Graph> graphs = graphTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    Graph graph = null;
                    goo.CastTo(out graph);
                    return graph;
                }).ToList();

                // Get points from the corresponding branch in the point tree
                List<Point3d> points = pointTree.get_Branch(path).Cast<GH_Point>().Select(ghPoint => ghPoint.Value).ToList();

                // Split the graphs in the current branch
                foreach (var graph in graphs)
                {
                    var subgraphs = GraphUtils.SplitGraphAtPoints(graph, points);

                    // Add the subgraphs to the output tree
                    foreach (var subgraph in subgraphs)
                    {
                        outputTree.Append(new GH_ObjectWrapper(subgraph), path);
                    }
                }
            }

            // Set output data
            DA.SetDataTree(0, outputTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("9501275F-39BD-465A-BF9E-921B6B8C560C"); }
        }
    }
}
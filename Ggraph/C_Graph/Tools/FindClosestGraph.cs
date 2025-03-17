using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Glab.Utilities;
using Newtonsoft.Json;

namespace Glab.C_Graph.Tools
{
    public class FindClosestGraph : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FindClosestGraph class.
        /// </summary>
        public FindClosestGraph()
          : base("Find Closest Graph", "FindClosestGraph",
              "Finds the closest graph for each test point based on proximity",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to search", GH_ParamAccess.tree);
            pManager.AddPointParameter("Test Points", "P", "Tree of test points to find the closest graphs for", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Closest Graphs", "CG", "Tree of closest graphs for each test point", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Distances", "D", "Tree of minimum distances from test points to their closest graphs", GH_ParamAccess.tree);
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

            // Initialize output data structures
            var closestGraphsTree = new GH_Structure<GH_ObjectWrapper>();
            var minDistancesTree = new GH_Structure<GH_Number>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in graphTree.Paths)
            {
                // Get graphs from the current branch
                var graphs = graphTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    Graph graph = null;
                    goo.CastTo(out graph);
                    return graph;
                }).ToList();

                // Get test points from the corresponding branch in the point tree
                var testPoints = pointTree.get_Branch(path).Cast<GH_Point>().Select(ghPoint => ghPoint.Value).ToList();

                // Call the FindClosestGraphs method
                List<double> minDistances;
                var closestGraphsDict = GraphUtils.FindClosestGraphs(graphs, testPoints, out minDistances);

                // Extract the graphs from the dictionary
                var closestGraphs = closestGraphsDict.Values.ToList();

                // Convert the closestGraphs list to a list of GH_ObjectWrapper
                var closestGraphsList = closestGraphs.Select(graph => new GH_ObjectWrapper(graph)).ToList();

                // Convert the minDistances list to a list of GH_Number
                var minDistancesList = minDistances.Select(d => new GH_Number(d)).ToList();

                // Append the results to the output trees
                closestGraphsTree.AppendRange(closestGraphsList, path);
                minDistancesTree.AppendRange(minDistancesList, path);
            }

            // Set output data
            DA.SetDataTree(0, closestGraphsTree);
            DA.SetDataTree(1, minDistancesTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D528915B"); }
        }
    }
}
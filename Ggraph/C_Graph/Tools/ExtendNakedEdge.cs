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
    public class ExtendNakedEdge : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ExtendNakedEdge class.
        /// </summary>
        public ExtendNakedEdge()
          : base("Extend Naked Edge", "ExtendEdge",
              "Extends the naked edges of the graph by a specified distance",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to extend naked edges", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Distances", "D", "Tree of distances to extend the naked edges", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Modified Graphs", "MG", "Tree of graphs with extended naked edges", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_Number> distanceTree = new GH_Structure<GH_Number>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out distanceTree)) return;

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            distanceTree = TreeUtils.SimplifyTree(distanceTree);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> modifiedGraphTree = new GH_Structure<GH_ObjectWrapper>();

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

                // Get distances from the corresponding branch in the distance tree
                var distances = distanceTree.get_Branch(path).Cast<GH_Number>().Select(ghNumber => ghNumber.Value).ToList();

                // Check if the number of graphs and distances match
                if (graphs.Count != distances.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of graphs and distances in the branch must be the same.");
                    return;
                }

                // Iterate through each graph
                for (int i = 0; i < graphs.Count; i++)
                {
                    var graph = graphs[i];
                    var distance = distances[i];

                    // Extend the naked edges of the graph using GraphUtils method
                    var modifiedGraph = GraphUtils.ExtendNakedEdges(graph, distance);

                    // Append the modified graph to the output tree
                    modifiedGraphTree.Append(new GH_ObjectWrapper(modifiedGraph), path);
                }
            }

            // Set output data
            DA.SetDataTree(0, modifiedGraphTree);
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
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D528155B"); }
        }
    }
}
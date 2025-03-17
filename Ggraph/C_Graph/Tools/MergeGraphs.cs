using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Glab.Utilities;

namespace Glab.C_Graph.Tools
{
    public class MergeGraphs : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MergeGraphs class.
        /// </summary>
        public MergeGraphs()
          : base("Merge Graphs", "MergeGraphs",
              "Merges graphs in the same branch",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to merge", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Merged Graphs", "MG", "Merged graphs", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variable
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;

            // Simplify input data tree using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);

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

                // Merge the graphs in the current branch
                Graph mergedGraph = GraphUtils.CombineGraphs(graphs);

                // Add the merged graph to the output tree
                outputTree.Append(new GH_ObjectWrapper(mergedGraph), path);
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
            get { return new Guid("D8F76237-B14B-4127-A030-C1215AE758EE"); }
        }
    }
}
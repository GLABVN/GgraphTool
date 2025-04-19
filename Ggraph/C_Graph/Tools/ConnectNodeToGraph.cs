using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Glab.Utilities;

namespace Glab.C_Graph.Tools
{
    public class ConnectNodeToGraph : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConnectNodeToGraph class.
        /// </summary>
        public ConnectNodeToGraph()
          : base("Connect Node To Graph", "ConnectNode",
              "Connects nodes to the graph based on proximity",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to connect nodes to", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Nodes", "N", "Tree of nodes to connect to the graphs", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Modified Graphs", "MG", "Tree of graphs with nodes connected", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> nodeTree = new GH_Structure<IGH_Goo>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out nodeTree)) return;

            // Validate input trees
            graphTree = TreeUtils.ValidateTreeStructure(graphTree, graphTree, check1Branch1Item: true); // Ensure each branch in graphTree has exactly one graph
            nodeTree = TreeUtils.ValidateTreeStructure(graphTree, nodeTree); // Validate nodeTree against graphTree


            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> modifiedGraphTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var nodes = TreeUtils.ExtractBranchData<GNode>(nodeTree, pathIndex);

                var graph = graphs[0];

                // Call the ConnectNodesToGraph method
                Graph modifiedGraph = GraphUtils.ConnectNodesToGraph(graph, nodes);

                // Append the modified graph to the output tree
                modifiedGraphTree.Append(new GH_ObjectWrapper(modifiedGraph), graphTree.Paths[pathIndex]);
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
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D528955B"); }
        }
    }
}
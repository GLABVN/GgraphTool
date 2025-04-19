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
    public class FindNeighborNodes : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FindNeighborNodes class.
        /// </summary>
        public FindNeighborNodes()
          : base("Find Neighbor Nodes", "FindNeighbors",
              "Finds the neighboring nodes for a given node in a graph within a specified distance",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to find neighbor nodes in", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Nodes", "N", "Tree of nodes to find neighbor nodes for", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Max Distance", "D", "Maximum distance to search for neighbor nodes", GH_ParamAccess.item, 0.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Neighbor Nodes", "NN", "Tree of neighbor nodes for each node", GH_ParamAccess.tree);
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
            double maxDistance = 0.0;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out nodeTree)) return;
            if (!DA.GetData(2, ref maxDistance)) return;

            // Validate input trees
            graphTree = TreeUtils.ValidateTreeStructure(graphTree, graphTree, check1Branch1Item: true); // Validate graphTree against itself
            nodeTree = TreeUtils.ValidateTreeStructure(graphTree, nodeTree);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> neighborNodesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var nodes = TreeUtils.ExtractBranchData<GNode>(nodeTree, pathIndex);

                // Ensure each branch processes one graph
                if (graphs.Count != 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Each branch in the graph tree must contain exactly one graph.");
                    return;
                }

                var graph = graphs[0];

                // Iterate through each node in the branch
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                {
                    var node = nodes[nodeIndex];

                    // Call the FindNeighborNodes method
                    var neighborNodesDict = GraphUtils.FindNeighborNodes(node, maxDistance);

                    // Append the neighbor nodes to the output tree under a unique subpath
                    var subPath = graphTree.Paths[pathIndex].AppendElement(nodeIndex);
                    foreach (var kvp in neighborNodesDict)
                    {
                        neighborNodesTree.AppendRange(kvp.Value.Select(n => new GH_ObjectWrapper(n)), subPath);
                    }
                }
            }

            // Set output data
            DA.SetDataTree(0, neighborNodesTree);
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
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D128951B"); }
        }
    }
}
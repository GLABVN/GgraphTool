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
    public class FindShortestPath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FindShortestPath class.
        /// </summary>
        public FindShortestPath()
          : base("Find Shortest Path", "FindPath",
              "Finds the shortest path between two nodes in a graph",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to find the shortest path in", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Start Nodes", "S", "Tree of start nodes for the shortest path", GH_ParamAccess.tree);
            pManager.AddGenericParameter("End Nodes", "E", "Tree of end nodes for the shortest path", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Path Nodes", "PN", "Tree of nodes in the shortest path", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Path Edges", "PE", "Tree of edges in the shortest path", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Path Length", "PL", "Tree of lengths of the shortest paths", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> startNodeTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> endNodeTree = new GH_Structure<IGH_Goo>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out startNodeTree)) return;
            if (!DA.GetDataTree(2, out endNodeTree)) return;

            // Validate input trees
            graphTree = TreeUtils.ValidateTreeStructure(graphTree, graphTree, check1Branch1Item: true); // Validate graphTree against itself
            startNodeTree = TreeUtils.ValidateTreeStructure(graphTree, startNodeTree);
            endNodeTree = TreeUtils.ValidateTreeStructure(graphTree, endNodeTree);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> pathNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> pathEdgesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_Number> pathLengthTree = new GH_Structure<GH_Number>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var startNodes = TreeUtils.ExtractBranchData<GNode>(startNodeTree, pathIndex);
                var endNodes = TreeUtils.ExtractBranchData<GNode>(endNodeTree, pathIndex);

                // Check if the number of start nodes, and end nodes match
                if (startNodes.Count != endNodes.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of start nodes and end nodes in the branch must be the same.");
                    return;
                }

                for (int i = 0; i < startNodes.Count; i++)
                { 
                    var startNode = startNodes[i];
                    var endNode = endNodes[i];

                    // Call the FindShortestPath method
                    GraphUtils.FindShortestPath(graphs[0], startNode, endNode, out List<GNode> pathNodes, out List<GEdge> pathEdges, out double pathLength);

                    // Append the path nodes, edges, and length to the output trees
                    var subPath = graphTree.Paths[pathIndex].AppendElement(i);
                    pathNodesTree.AppendRange(pathNodes.Select(n => new GH_ObjectWrapper(n)), subPath);
                    pathEdgesTree.AppendRange(pathEdges.Select(e => new GH_ObjectWrapper(e)), subPath);
                    pathLengthTree.Append(new GH_Number(pathLength), subPath);
                }

            }

            // Set output data
            DA.SetDataTree(0, pathNodesTree);
            DA.SetDataTree(1, pathEdgesTree);
            DA.SetDataTree(2, pathLengthTree);
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
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D128955B"); }
        }
    }
}
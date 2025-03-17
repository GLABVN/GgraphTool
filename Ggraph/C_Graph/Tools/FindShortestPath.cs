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

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            startNodeTree = TreeUtils.SimplifyTree(startNodeTree);
            endNodeTree = TreeUtils.SimplifyTree(endNodeTree);

            // Check if the number of branches in the input trees is the same
            if (graphTree.Branches.Count != startNodeTree.Branches.Count || graphTree.Branches.Count != endNodeTree.Branches.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of branches in the input trees must be the same.");
                return;
            }

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> pathNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> pathEdgesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_Number> pathLengthTree = new GH_Structure<GH_Number>();

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

                // Get start nodes from the corresponding branch in the start node tree
                var startNodes = startNodeTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    GNode node = null;
                    goo.CastTo(out node);
                    return node;
                }).ToList();

                // Get end nodes from the corresponding branch in the end node tree
                var endNodes = endNodeTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    GNode node = null;
                    goo.CastTo(out node);
                    return node;
                }).ToList();

                // Check if the number of graphs, start nodes, and end nodes match
                if (graphs.Count != startNodes.Count || graphs.Count != endNodes.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of graphs, start nodes, and end nodes in the branch must be the same.");
                    return;
                }

                // Iterate through each graph
                for (int i = 0; i < graphs.Count; i++)
                {
                    var graph = graphs[i];
                    var startNode = startNodes[i];
                    var endNode = endNodes[i];

                    // Call the FindShortestPath method
                    GraphUtils.FindShortestPath(graph, startNode, endNode, out List<GNode> pathNodes, out List<GEdge> pathEdges, out double pathLength);

                    // Append the path nodes, edges, and length to the output trees
                    var subPath = path.AppendElement(i);
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
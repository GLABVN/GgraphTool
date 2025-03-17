using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using Glab.Utilities;

namespace Glab.C_Graph
{
    public class ConstructGraph : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateGraphFromNodeAndEdge class.
        /// </summary>
        public ConstructGraph()
          : base("Construct Graph", "ConstructGraph",
              "Creates graphs from input nodes and edges. Each branch of nodes and edges produces a separate graph.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input nodes as tree
            pManager.AddGenericParameter("Nodes", "N", "Input nodes as a tree", GH_ParamAccess.tree);
            pManager[0].Optional = true;
            // Input edges as tree
            pManager.AddGenericParameter("Edges", "E", "Input edges as a tree", GH_ParamAccess.tree);
            // Boolean parameter to shatter edges
            pManager.AddBooleanParameter("Shatter Edge?", "D", "If true, isolated points on an edge will shatter the edge and add the node to the graph", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Output Graph objects as a tree
            pManager.AddGenericParameter("Graphs", "G", "Output Graph objects as a tree", GH_ParamAccess.tree);
            // Output isolated nodes as a tree
            pManager.AddGenericParameter("Isolated Nodes", "N", "Isolated nodes from each graph", GH_ParamAccess.tree);
            // Output isolated edges as a tree
            pManager.AddGenericParameter("Isolated Edges", "E", "Isolated edges from each graph", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This method does the work of creating graphs from nodes and edges.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> nodesTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> edgesTree = new GH_Structure<IGH_Goo>();
            bool shatterEdge = false;

            // Get input data
            bool hasNodes = DA.GetDataTree(0, out nodesTree) && !nodesTree.IsEmpty;
            if (!DA.GetDataTree(1, out edgesTree)) return;
            if (!DA.GetData(2, ref shatterEdge)) return;

            // Simplify input data trees using TreeUtils
            nodesTree = TreeUtils.SimplifyTree(nodesTree);
            edgesTree = TreeUtils.SimplifyTree(edgesTree);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> graphsTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedEdgesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in edgesTree.Paths)
            {
                // Get edges from the current branch
                var edges = edgesTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    GEdge edge = null;
                    goo.CastTo(out edge);
                    return edge;
                }).Where(edge => edge != null).ToList();

                List<GNode> nodes = new List<GNode>();
                if (hasNodes)
                {
                    // Get nodes from the current branch
                    nodes = nodesTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                    {
                        GNode node = null;
                        goo.CastTo(out node);
                        return node;
                    }).Where(node => node != null).ToList();
                }

                // Create graphs using the static method with isolated elements
                var graphs = GraphUtils.CreateGraphsFromNodesAndEdges(nodes, edges, out var isolatedNodes, out var isolatedEdges, shatterEdge);

                // Add runtime message if more than one graph is formed
                if (graphs.Count > 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"More than one graph formed at path {path}: {graphs.Count} graphs.");
                }

                // Add runtime message for isolated nodes and edges
                int totalIsolatedNodes = isolatedNodes.Count;
                int totalIsolatedEdges = isolatedEdges.Count;
                if (totalIsolatedNodes > 0 || totalIsolatedEdges > 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"Path {path}: Found {totalIsolatedNodes} isolated nodes and {totalIsolatedEdges} isolated edges");
                }

                // Process each graph
                foreach (var graph in graphs)
                {
                    graphsTree.Append(new GH_ObjectWrapper(graph), path);
                }

                // Add isolated nodes to output
                foreach (var node in isolatedNodes)
                {
                    isolatedNodesTree.Append(new GH_ObjectWrapper(node), path);
                }

                // Add isolated edges to output
                foreach (var edge in isolatedEdges)
                {
                    isolatedEdgesTree.Append(new GH_ObjectWrapper(edge), path);
                }
            }

            // Set output data
            DA.SetDataTree(0, graphsTree);
            DA.SetDataTree(1, isolatedNodesTree);
            DA.SetDataTree(2, isolatedEdgesTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("F40249BA-AFF9-4C09-8056-18A50DE951FB"); }
        }
    }
}
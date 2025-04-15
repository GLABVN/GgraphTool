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
            pManager.AddGenericParameter("Additional Nodes", "AN", "Optional tree of additional nodes to include in the merge", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Additional Edges", "AE", "Optional tree of additional edges to include in the merge", GH_ParamAccess.tree);

            // Mark "Additional Nodes" and "Additional Edges" as optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Merged Graphs", "MG", "Merged graphs", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Isolated Nodes", "IN", "Nodes that are isolated after merging", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Isolated Edges", "IE", "Edges that are isolated after merging", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> additionalNodesTree = null;
            GH_Structure<IGH_Goo> additionalEdgesTree = null;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            DA.GetDataTree(1, out additionalNodesTree);
            DA.GetDataTree(2, out additionalEdgesTree);

            // Simplify input data tree using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            additionalNodesTree = additionalNodesTree != null ? TreeUtils.SimplifyTree(additionalNodesTree) : null;
            additionalEdgesTree = additionalEdgesTree != null ? TreeUtils.SimplifyTree(additionalEdgesTree) : null;

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> mergedGraphsTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedEdgesTree = new GH_Structure<GH_ObjectWrapper>();

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

                // Get additional nodes and edges for the current path if available
                List<GNode> additionalNodes = additionalNodesTree?.get_Branch(path)?.Cast<IGH_Goo>().Select(goo =>
                {
                    GNode node = null;
                    goo.CastTo(out node);
                    return node;
                }).ToList();

                List<GEdge> additionalEdges = additionalEdgesTree?.get_Branch(path)?.Cast<IGH_Goo>().Select(goo =>
                {
                    GEdge edge = null;
                    goo.CastTo(out edge);
                    return edge;
                }).ToList();

                // Merge the graphs in the current branch
                List<GNode> isolatedNodes;
                List<GEdge> isolatedEdges;
                Graph mergedGraph = GraphUtils.CombineGraphs(graphs, out isolatedNodes, out isolatedEdges, additionalNodes, additionalEdges);

                // Add the merged graph to the output tree
                mergedGraphsTree.Append(new GH_ObjectWrapper(mergedGraph), path);

                // Add isolated nodes and edges to their respective output trees
                foreach (var node in isolatedNodes)
                {
                    isolatedNodesTree.Append(new GH_ObjectWrapper(node), path);
                }

                foreach (var edge in isolatedEdges)
                {
                    isolatedEdgesTree.Append(new GH_ObjectWrapper(edge), path);
                }
            }

            // Set output data
            DA.SetDataTree(0, mergedGraphsTree);
            DA.SetDataTree(1, isolatedNodesTree);
            DA.SetDataTree(2, isolatedEdgesTree);
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
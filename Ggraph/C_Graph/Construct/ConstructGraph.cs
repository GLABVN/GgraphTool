using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
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
            // Input attributes as JSON string tree
            pManager.AddTextParameter("Attributes", "A", "Attributes as JSON string tree to set for each graph", GH_ParamAccess.tree);
            pManager[3].Optional = true;
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
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();
            bool shatterEdge = false;

            // Get input data
            if (!DA.GetDataTree(0, out nodesTree)) return;
            if (!DA.GetDataTree(1, out edgesTree)) return;
            if (!DA.GetData(2, ref shatterEdge)) return;
            DA.GetDataTree(3, out attributesTree);

            // Validate input trees
            TreeUtils.ValidateTreeStructure(edgesTree, edgesTree); // Validate edgesTree against itself
            TreeUtils.ValidateTreeStructure(edgesTree, nodesTree);
            TreeUtils.ValidateTreeStructure(edgesTree, attributesTree);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> graphsTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedEdgesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < edgesTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var edges = TreeUtils.ExtractBranchData<GEdge>(edgesTree, pathIndex);
                var nodes = TreeUtils.ExtractBranchData<GNode>(nodesTree, pathIndex);
                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex);

                // Parse attributes if available
                Dictionary<string, object> attributeDict = null;
                if (attributes.Count > 0)
                {
                    var attributeJson = attributes[0]; // Use the first attribute JSON string
                    if (!string.IsNullOrEmpty(attributeJson))
                    {
                        attributeDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(attributeJson);
                    }
                }

                // Create graphs using the static method with isolated elements
                var graphs = GraphUtils.CreateGraphsFromNodesAndEdges(nodes, edges, out var isolatedNodes, out var isolatedEdges, shatterEdge);

                // Add runtime message if more than one graph is formed
                if (graphs.Count > 1)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"More than one graph formed at path {edgesTree.Paths[pathIndex]}: {graphs.Count} graphs.");
                }

                // Add runtime message for isolated nodes and edges
                int totalIsolatedNodes = isolatedNodes.Count;
                int totalIsolatedEdges = isolatedEdges.Count;
                if (totalIsolatedNodes > 0 || totalIsolatedEdges > 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        $"Path {edgesTree.Paths[pathIndex]}: Found {totalIsolatedNodes} isolated nodes and {totalIsolatedEdges} isolated edges");
                }

                // Process each graph
                foreach (var graph in graphs)
                {
                    // Set attributes if provided
                    if (attributeDict != null)
                    {
                        foreach (var attribute in attributeDict)
                        {
                            graph.Attributes[attribute.Key] = attribute.Value;
                        }
                    }

                    graphsTree.Append(new GH_ObjectWrapper(graph), edgesTree.Paths[pathIndex]);
                }

                // Add isolated nodes to output
                foreach (var node in isolatedNodes)
                {
                    isolatedNodesTree.Append(new GH_ObjectWrapper(node), edgesTree.Paths[pathIndex]);
                }

                // Add isolated edges to output
                foreach (var edge in isolatedEdges)
                {
                    isolatedEdgesTree.Append(new GH_ObjectWrapper(edge), edgesTree.Paths[pathIndex]);
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
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Glab.Utilities;
using Newtonsoft.Json;

namespace Glab.C_Graph.Tools
{
    public class FilterNodeAndEdge : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FilterNodeAndEdge class.
        /// </summary>
        public FilterNodeAndEdge()
          : base("Filter Node And Edge", "FilterNodeEdge",
              "Filters nodes and edges in the graph based on type, properties, and attributes",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to filter", GH_ParamAccess.tree);
            pManager.AddTextParameter("Type", "T", "Type to filter nodes and edges", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Properties", "P", "Properties as JSON string to filter nodes and edges", GH_ParamAccess.tree); // Moved Properties input
            pManager[2].Optional = true;
            pManager.AddTextParameter("Attributes", "A", "Attributes as JSON string to filter nodes and edges", GH_ParamAccess.tree);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Filtered Nodes", "N", "Filtered nodes", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Filtered Edges", "E", "Filtered edges", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_String> typeTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> propertiesTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            DA.GetDataTree(1, out typeTree);
            DA.GetDataTree(2, out propertiesTree);
            DA.GetDataTree(3, out attributesTree);

            // Validate input trees
            TreeUtils.ValidateTreeStructure(graphTree, graphTree, check1Branch1Item: true); // Validate graphTree against itself
            TreeUtils.ValidateTreeStructure(graphTree, typeTree);
            TreeUtils.ValidateTreeStructure(graphTree, propertiesTree);
            TreeUtils.ValidateTreeStructure(graphTree, attributesTree);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> filteredNodes = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> filteredEdges = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Validate input trees
                graphTree = TreeUtils.ValidateTreeStructure(graphTree, graphTree, check1Branch1Item: true); // Validate graphTree against itself
                typeTree = TreeUtils.ValidateTreeStructure(graphTree, typeTree);
                propertiesTree = TreeUtils.ValidateTreeStructure(graphTree, propertiesTree);
                attributesTree = TreeUtils.ValidateTreeStructure(graphTree, attributesTree);
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var types = TreeUtils.ExtractBranchData(typeTree, pathIndex);
                var properties = TreeUtils.ExtractBranchData(propertiesTree, pathIndex)
                    .Select(json => json == null ? null : JsonConvert.DeserializeObject<Dictionary<string, object>>(json))
                    .ToList();

                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex)
                    .Select(json => json == null ? null : JsonConvert.DeserializeObject<Dictionary<string, object>>(json))
                    .ToList();


                var graph = graphs[0];

                // Filter nodes
                var nodes = graph.QuickGraphObj.Vertices.Where(node =>
                {
                    // Check matches for type, properties, and attributes
                    var matchesType = types.Select(type => type == null || type == node.Type).ToList();
                    var matchesProperties = properties.Select(propDict =>
                        propDict == null || propDict.All(kv => node.PropJSON.ContainsKey(kv.Key) && node.PropJSON[kv.Key]?.ToString() == kv.Value?.ToString())).ToList();
                    var matchesAttributes = attributes.Select(attrDict =>
                        attrDict == null || attrDict.All(kv => node.Attributes.ContainsKey(kv.Key) && node.Attributes[kv.Key]?.ToString() == kv.Value?.ToString())).ToList();

                    // Combine all conditions into a list of lists
                    var matchedConditions = new List<List<bool>> { matchesType, matchesProperties, matchesAttributes };

                    // Use TreeUtils.CrossCondition to evaluate the combined conditions
                    return TreeUtils.CrossCondition(matchedConditions);
                }).ToList();

                // Filter edges
                var edges = graph.QuickGraphObj.Edges.Where(edge =>
                {
                    // Check matches for type, properties, and attributes
                    var matchesType = types.Select(type => type == null || type == edge.Type).ToList();
                    var matchesProperties = properties.Select(propDict =>
                        propDict == null || propDict.All(kv => edge.PropJSON.ContainsKey(kv.Key) && edge.PropJSON[kv.Key]?.ToString() == kv.Value?.ToString())).ToList();
                    var matchesAttributes = attributes.Select(attrDict =>
                        attrDict == null || attrDict.All(kv => edge.Attributes.ContainsKey(kv.Key) && edge.Attributes[kv.Key]?.ToString() == kv.Value?.ToString())).ToList();

                    // Combine all conditions into a list of lists
                    var matchedConditions = new List<List<bool>> { matchesType, matchesProperties, matchesAttributes };

                    // Use TreeUtils.CrossCondition to evaluate the combined conditions
                    return TreeUtils.CrossCondition(matchedConditions);
                }).ToList();

                // Append filtered nodes and edges to the output trees
                foreach (var node in nodes)
                {
                    filteredNodes.Append(new GH_ObjectWrapper(node), graphTree.Paths[pathIndex]);
                }
                foreach (var edge in edges)
                {
                    filteredEdges.Append(new GH_ObjectWrapper(edge), graphTree.Paths[pathIndex]);
                }
            }

            // Set output data
            DA.SetDataTree(0, filteredNodes);
            DA.SetDataTree(1, filteredEdges);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("82BDD1E6-E17D-44D2-85A0-7BF9D528951B"); }
        }
    }
}
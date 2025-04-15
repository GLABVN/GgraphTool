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
            GH_Structure<GH_String> propertiesTree = new GH_Structure<GH_String>(); // Properties input
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            bool hasType = DA.GetDataTree(1, out typeTree) && !typeTree.IsEmpty;
            bool hasProperties = DA.GetDataTree(2, out propertiesTree) && !propertiesTree.IsEmpty; // Check for properties input
            bool hasAttributes = DA.GetDataTree(3, out attributesTree) && !attributesTree.IsEmpty;

            // Simplify input data tree using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            if (hasType) typeTree = TreeUtils.SimplifyTree(typeTree);
            if (hasProperties) propertiesTree = TreeUtils.SimplifyTree(propertiesTree);
            if (hasAttributes) attributesTree = TreeUtils.SimplifyTree(attributesTree);

            // If no filters are provided, set output to empty and return
            if (!hasType && !hasProperties && !hasAttributes)
            {
                DA.SetDataTree(0, new GH_Structure<GH_ObjectWrapper>());
                DA.SetDataTree(1, new GH_Structure<GH_ObjectWrapper>());
                return;
            }

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> filteredNodes = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> filteredEdges = new GH_Structure<GH_ObjectWrapper>();

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

                List<string> types = new List<string>();
                if (hasType)
                {
                    var branch = typeTree.get_Branch(path);
                    if (branch != null)
                    {
                        types = branch.Cast<GH_String>().Select(ghString => ghString.Value).ToList();
                    }
                }

                List<Dictionary<string, object>> properties = new List<Dictionary<string, object>>(); // Parse properties
                if (hasProperties)
                {
                    var branch = propertiesTree.get_Branch(path);
                    if (branch != null)
                    {
                        properties = branch.Cast<GH_String>()
                            .Select(ghString => JsonConvert.DeserializeObject<Dictionary<string, object>>(ghString.Value))
                            .ToList();
                    }
                }

                List<Dictionary<string, object>> attributes = new List<Dictionary<string, object>>();
                if (hasAttributes)
                {
                    var branch = attributesTree.get_Branch(path);
                    if (branch != null)
                    {
                        attributes = branch.Cast<GH_String>()
                            .Select(ghString => JsonConvert.DeserializeObject<Dictionary<string, object>>(ghString.Value))
                            .ToList();
                    }
                }

                // Iterate through each graph
                foreach (var graph in graphs)
                {
                    // Access nodes and edges from QuickGraphObj
                    var nodes = graph.QuickGraphObj.Vertices.Where(node =>
                    {
                        bool isTypeProvided = hasType;
                        bool isPropertiesProvided = hasProperties;
                        bool isAttributesProvided = hasAttributes;

                        bool matchesType = !isTypeProvided || types.Contains(node.Type);
                        bool matchesProperties = !isPropertiesProvided || properties.All(propDict =>
                            propDict.All(kv =>
                                node.PropJSON.ContainsKey(kv.Key) &&
                                node.PropJSON[kv.Key]?.ToString() == kv.Value?.ToString()
                            ));
                        bool matchesAttributes = !isAttributesProvided || attributes.All(attrDict =>
                            attrDict.All(kv =>
                                node.Attributes.ContainsKey(kv.Key) &&
                                node.Attributes[kv.Key]?.ToString() == kv.Value?.ToString()
                            ));

                        return (isTypeProvided && matchesType && isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes) || // All provided and matched
                               (isTypeProvided && matchesType && !isPropertiesProvided && !isAttributesProvided) || // Only type provided and matched
                               (isPropertiesProvided && matchesProperties && !isTypeProvided && !isAttributesProvided) || // Only properties provided and matched
                               (isAttributesProvided && matchesAttributes && !isTypeProvided && !isPropertiesProvided) || // Only attributes provided and matched
                               (isTypeProvided && matchesType && isAttributesProvided && matchesAttributes) || // Only type and attributes provided and matched
                               (isTypeProvided && matchesType && isPropertiesProvided && matchesProperties) || // Only type and properties provided and matched)
                               (isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes) || // Only properties and attributes provided and matched
                               (isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes); // Only properties and attributes provided and matched
                    }).ToList();

                    var edges = graph.QuickGraphObj.Edges.Where(edge =>
                    {
                        bool isTypeProvided = hasType;
                        bool isPropertiesProvided = hasProperties;
                        bool isAttributesProvided = hasAttributes;

                        bool matchesType = !isTypeProvided || types.Contains(edge.Type);
                        bool matchesProperties = !isPropertiesProvided || properties.All(propDict =>
                            propDict.All(kv =>
                                edge.PropJSON.ContainsKey(kv.Key) &&
                                edge.PropJSON[kv.Key]?.ToString() == kv.Value?.ToString()
                            ));
                        bool matchesAttributes = !isAttributesProvided || attributes.All(attrDict =>
                            attrDict.All(kv =>
                                edge.Attributes.ContainsKey(kv.Key) &&
                                edge.Attributes[kv.Key]?.ToString() == kv.Value?.ToString()
                            ));

                        return (isTypeProvided && matchesType && isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes) || // All provided and matched
                               (isTypeProvided && matchesType && !isPropertiesProvided && !isAttributesProvided) || // Only type provided and matched
                               (isPropertiesProvided && matchesProperties && !isTypeProvided && !isAttributesProvided) || // Only properties provided and matched
                               (isAttributesProvided && matchesAttributes && !isTypeProvided && !isPropertiesProvided) || // Only attributes provided and matched
                                (isTypeProvided && matchesType && isAttributesProvided && matchesAttributes) || // Only type and attributes provided and matched
                                (isTypeProvided && matchesType && isPropertiesProvided && matchesProperties) || // Only type and properties provided and matched)
                                (isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes) || // Only properties and attributes provided and matched
                                (isPropertiesProvided && matchesProperties && isAttributesProvided && matchesAttributes); // Only properties and attributes provided and matched

                    }).ToList();

                    // Append filtered nodes and edges to the output trees
                    foreach (var node in nodes)
                    {
                        filteredNodes.Append(new GH_ObjectWrapper(node), path);
                    }
                    foreach (var edge in edges)
                    {
                        filteredEdges.Append(new GH_ObjectWrapper(edge), path);
                    }
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
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino.Geometry;
using Glab.Utilities;

namespace Glab.C_Graph.Tools
{
    public class ModifyGraphAttribute : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ModifyGraphAttribute class.
        /// </summary>
        public ModifyGraphAttribute()
          : base("Modify Graph Attribute", "MGA",
              "Modify attributes and type of a Graph object",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Graph as tree
            pManager.AddGenericParameter("Graph", "G", "Graph object as tree", GH_ParamAccess.tree);
            // Type as tree (optional)
            pManager.AddTextParameter("Type", "T", "New type for the graph", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            // JSON string as tree (optional)
            pManager.AddTextParameter("Attributes", "A", "Attributes as JSON string tree", GH_ParamAccess.tree);
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Updated Graph
            pManager.AddGenericParameter("Graph", "G", "Updated Graph with modified attributes and type", GH_ParamAccess.tree);
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
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            bool hasType = DA.GetDataTree(1, out typeTree) && !typeTree.IsEmpty;
            bool hasAttributes = DA.GetDataTree(2, out attributesTree) && !attributesTree.IsEmpty;

            // Simplify input data tree using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            typeTree = TreeUtils.SimplifyTree(typeTree);
            attributesTree = TreeUtils.SimplifyTree(attributesTree);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> updatedGraphTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in graphTree.Paths)
            {
                // Get branches for the current path
                List<Graph> graphs = graphTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
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

                List<string> attributes = new List<string>();
                if (hasAttributes)
                {
                    var branch = attributesTree.get_Branch(path);
                    if (branch != null)
                    {
                        attributes = branch.Cast<GH_String>().Select(ghString => ghString.Value).ToList();
                    }
                }

                // Check if the count of graphs and attributes are equal
                if (attributes.Count > 0 && graphs.Count != attributes.Count)
                {
                    if (attributes.Count == 1)
                    {
                        // Repeat the single attribute for all graphs
                        attributes = Enumerable.Repeat(attributes[0], graphs.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of graphs and attributes should be equal or attributes should have a single item.");
                        return;
                    }
                }

                // Check if the count of graphs and types are equal
                if (types.Count > 0 && graphs.Count != types.Count)
                {
                    if (types.Count == 1)
                    {
                        // Repeat the single type for all graphs
                        types = Enumerable.Repeat(types[0], graphs.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of graphs and types should be equal or types should have a single item.");
                        return;
                    }
                }

                // Iterate through each graph and corresponding attributes
                for (int i = 0; i < graphs.Count; i++)
                {
                    // Deep copy the graph
                    Graph graphCopy = graphs[i].DeepCopy();

                    // Get the type and attributes for the current graph
                    string type = types.Count > 0 ? types[i] : null;
                    string attributesJson = attributes.Count > 0 ? attributes[i] : null;

                    // Update the graph's type
                    if (type != null)
                    {
                        graphCopy.Type = type;
                    }

                    // Update the graph's attributes
                    if (attributesJson != null)
                    {
                        var attributeDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(attributesJson);
                        foreach (var attribute in attributeDict)
                        {
                            graphCopy.Attributes[attribute.Key] = attribute.Value;
                        }
                    }

                    // Append the updated graph to the output tree
                    updatedGraphTree.Append(new GH_ObjectWrapper(graphCopy), path);
                }
            }

            // Set output data
            DA.SetDataTree(0, updatedGraphTree);
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
            get { return new Guid("0EAFC503-2E19-41F8-B244-13791C36FABB"); }
        }
    }
}

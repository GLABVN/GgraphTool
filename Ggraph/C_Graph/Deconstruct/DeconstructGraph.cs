using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json;

namespace Glab.C_Graph
{
    public class DeconstructGraph : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructGraph class.
        /// </summary>
        public DeconstructGraph()
          : base("Deconstruct Graph", "DeGraph",
              "Deconstructs a Graph object into its attributes, nodes, edges, isolated nodes, and isolated edges.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input Graph object as tree
            pManager.AddGenericParameter("Graph", "G", "Input Graph object as tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Output attributes as text
            pManager.AddTextParameter("Attributes", "A", "Graph attributes as JSON string", GH_ParamAccess.tree);
            // Output nodes as tree
            pManager.AddGenericParameter("Nodes", "N", "Graph nodes as tree", GH_ParamAccess.tree);
            // Output edges as tree
            pManager.AddGenericParameter("Edges", "E", "Graph edges as tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphData = new GH_Structure<IGH_Goo>();

            // Output variables
            GH_Structure<GH_String> graphAttributes = new GH_Structure<GH_String>();
            GH_Structure<GH_ObjectWrapper> nodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> edgesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedNodesTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> isolatedEdgesTree = new GH_Structure<GH_ObjectWrapper>();

            // Get graph data
            if (!DA.GetDataTree(0, out graphData)) return;

            // Deconstruct graph data
            // Iterate through each path in the input data
            for (int i = 0; i < graphData.PathCount; i++)
            {
                // Get the current path
                GH_Path path = graphData.Paths[i];

                // Get the list of graphs in the current branch
                var graphBranch = graphData.get_Branch(path);

                // Iterate through each item in the current branch
                for (int j = 0; j < graphBranch.Count; j++)
                {
                    var goo = graphBranch[j];
                    if (goo == null) continue;

                    // Attempt to cast the IGH_Goo item to a GH_ObjectWrapper
                    if (!(goo is GH_ObjectWrapper wrapper) || wrapper.Value == null)
                        continue;

                    // Check if the wrapped object is a Graph
                    if (wrapper.Value is Graph graph)
                    {
                        // Create a subpath for each graph
                        GH_Path subPath = path.AppendElement(j);

                        // Run the ConvertPropertiesToAttributes method
                        graph.ConvertPropertiesToAttributes();

                        // Serialize graph attributes to JSON with pretty-printing
                        string attributesJson = JsonConvert.SerializeObject(graph.Attributes, Formatting.Indented);
                        graphAttributes.Append(new GH_String(attributesJson), subPath);

                        // Add nodes to the nodes tree
                        foreach (var node in graph.QuickGraphObj.Vertices)
                        {
                            nodesTree.Append(new GH_ObjectWrapper(node), subPath);
                        }

                        // Add edges to the edges tree
                        foreach (var edge in graph.QuickGraphObj.Edges)
                        {
                            edgesTree.Append(new GH_ObjectWrapper(edge), subPath);
                        }
                    }
                    else
                    {
                        // Handle cases where the input is not a Graph object
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a valid Graph object.");
                        return;
                    }
                }
            }

            // Set output data
            DA.SetDataTree(0, graphAttributes);
            DA.SetDataTree(1, nodesTree);
            DA.SetDataTree(2, edgesTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

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
            get { return new Guid("4FB2E897-11F3-41AF-BE73-86EA2800D0D2"); }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json;

namespace Glab.C_Graph
{
    public class DeconstructEdge : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructEdge class.
        /// </summary>
        public DeconstructEdge()
          : base("Deconstruct Edge", "DeEdge",
              "Deconstructs an Edge object into its properties and related data.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input Edge object as tree
            pManager.AddGenericParameter("Edge", "E", "Input Edge object as tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Output properties as text
            pManager.AddTextParameter("Properties", "P", "Edge properties as JSON string", GH_ParamAccess.tree);
            // Output source node
            pManager.AddGenericParameter("Source Node", "S", "Source node of the edge", GH_ParamAccess.tree);
            // Output target node
            pManager.AddGenericParameter("Target Node", "T", "Target node of the edge", GH_ParamAccess.tree);
            // Output edge geometry
            pManager.AddCurveParameter("Geometry", "G", "Edge geometry", GH_ParamAccess.tree);
            // Output linked objects
            pManager.AddGenericParameter("Linked Objects", "L", "Linked objects of the edge", GH_ParamAccess.tree);
            // Output attributes as text
            pManager.AddTextParameter("Attributes", "A", "Edge attributes as JSON string", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This method does the work of deconstructing the Edge object.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> edgeData = new GH_Structure<IGH_Goo>();

            // Get edge data
            if (!DA.GetDataTree(0, out edgeData)) return;

            // Initialize output data structures
            GH_Structure<GH_String> edgeProperties = new GH_Structure<GH_String>();
            GH_Structure<GH_ObjectWrapper> sourceNodes = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> targetNodes = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_Curve> edgeGeometries = new GH_Structure<GH_Curve>();
            GH_Structure<GH_ObjectWrapper> linkedObjects = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_String> edgeAttributes = new GH_Structure<GH_String>();

            // Deconstruct edge data
            // Iterate through each path in the input data
            for (int i = 0; i < edgeData.PathCount; i++)
            {
                // Get the current path
                GH_Path path = edgeData.Paths[i];

                // Get the list of edges in the current branch
                var edgeBranch = edgeData.get_Branch(path);

                // Iterate through each item in the current branch
                foreach (var goo in edgeBranch)
                {
                    if (goo == null) continue;

                    // Attempt to cast the IGH_Goo item to a GH_ObjectWrapper
                    if (!(goo is GH_ObjectWrapper wrapper) || wrapper.Value == null)
                        continue;

                    // Check if the wrapped object is an Edge
                    if (wrapper.Value is GEdge edge)
                    {
                        // Serialize edge properties (PropJSON) to JSON
                        string propertiesJson = JsonConvert.SerializeObject(edge.PropDict, Formatting.Indented);
                        edgeProperties.Append(new GH_String(propertiesJson), path);

                        // Serialize edge attributes to JSON
                        string attributesJson = JsonConvert.SerializeObject(edge.Attributes, Formatting.Indented);
                        edgeAttributes.Append(new GH_String(attributesJson), path);

                        // Add source node to the output
                        sourceNodes.Append(new GH_ObjectWrapper(edge.Source), path);

                        // Add target node to the output
                        targetNodes.Append(new GH_ObjectWrapper(edge.Target), path);

                        // Add geometry to the output (if available)
                        if (edge.EdgeCurve != null)
                        {
                            edgeGeometries.Append(new GH_Curve(edge.EdgeCurve), path);
                        }
                        else
                        {
                            edgeGeometries.Append(null, path);
                        }

                        // Add linked objects to the output
                        if (edge.LinkedObjects != null && edge.LinkedObjects.Count > 0)
                        {
                            // Create a new path for the linked objects
                            GH_Path linkedObjectPath = path;

                            // Add each linked object wrapped in GH_ObjectWrapper
                            foreach (var obj in edge.LinkedObjects)
                            {
                                linkedObjects.Append(new GH_ObjectWrapper(obj), linkedObjectPath);
                            }
                        }
                        else
                        {
                            // Add null if no linked objects exist
                            linkedObjects.Append(null, path);
                        }
                    }
                    else
                    {
                        // Handle cases where the input is not an Edge object
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a valid Edge object.");
                        return;
                    }
                }
            }

            // Set output data
            DA.SetDataTree(0, edgeProperties);
            DA.SetDataTree(1, sourceNodes);
            DA.SetDataTree(2, targetNodes);
            DA.SetDataTree(3, edgeGeometries);
            DA.SetDataTree(4, linkedObjects);
            DA.SetDataTree(5, edgeAttributes);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Gets the unique ID for this component.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("D2F4C9A6-BC9F-4E5C-9D51-3E0A8C894D7F"); }
        }
    }
}
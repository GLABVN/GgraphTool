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
    public class DeconstructNode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DeconstructNode class.
        /// </summary>
        public DeconstructNode()
          : base("Deconstruct Node", "DeNode",
              "Deconstructs a Node object into its attributes and point.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input Node object as tree
            pManager.AddGenericParameter("Node", "N", "Input Node object as tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Output attributes as text
            pManager.AddTextParameter("Attributes", "A", "Node attributes as JSON string", GH_ParamAccess.tree);
            // Output point
            pManager.AddPointParameter("Point", "P", "Node point", GH_ParamAccess.tree);
            // Output linked objects
            pManager.AddGenericParameter("Linked Objects", "L", "Linked objects of the node", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> nodeData = new GH_Structure<IGH_Goo>();

            // Get node data
            if (!DA.GetDataTree(0, out nodeData)) return;

            // Initialize output data structures
            GH_Structure<GH_String> nodeAttributes = new GH_Structure<GH_String>();
            GH_Structure<GH_Point> nodePoints = new GH_Structure<GH_Point>();
            GH_Structure<GH_ObjectWrapper> linkedObjects = new GH_Structure<GH_ObjectWrapper>();

            // Deconstruct node data
            // Iterate through each path in the input data
            for (int i = 0; i < nodeData.PathCount; i++)
            {
                // Get the current path
                GH_Path path = nodeData.Paths[i];

                // Get the list of nodes in the current branch
                var nodeBranch = nodeData.get_Branch(path);

                // Iterate through each item in the current branch
                foreach (var goo in nodeBranch)
                {
                    if (goo == null) continue;

                    // Attempt to cast the IGH_Goo item to a GH_ObjectWrapper
                    if (!(goo is GH_ObjectWrapper wrapper) || wrapper.Value == null)
                        continue;

                    // Check if the wrapped object is a Node
                    if (wrapper.Value is GNode node)
                    {
                        // Run the ConvertPropertiesToAttributes method
                        node.ConvertPropertiesToAttributes();

                        // Extract the point from the node
                        Point3d point = node.Point;
                        nodePoints.Append(new GH_Point(point), path);

                        // Serialize node attributes to JSON
                        string attributesJson = JsonConvert.SerializeObject(node.Attributes, Formatting.Indented);
                        nodeAttributes.Append(new GH_String(attributesJson), path);

                        // Add linked objects to the output
                        if (node.LinkedObjects != null && node.LinkedObjects.Count > 0)
                        {
                            // Create a new path for the linked objects
                            GH_Path linkedObjectPath = path;

                            // Add each linked object wrapped in GH_ObjectWrapper
                            foreach (var obj in node.LinkedObjects)
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
                        // Handle cases where the input is not a Node object
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a valid Node object.");
                        return;
                    }
                }
            }

            // Set output data
            DA.SetDataTree(0, nodeAttributes);
            DA.SetDataTree(1, nodePoints);
            DA.SetDataTree(2, linkedObjects);
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
            get { return new Guid("D1E4C8A5-AB8F-4F4B-9C50-2E9A8B784D6E"); }
        }
    }
}
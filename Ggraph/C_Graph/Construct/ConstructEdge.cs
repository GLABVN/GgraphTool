using System;
using System.Collections.Generic;
using System.Linq;
using Glab.Utilities;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;

namespace Glab.C_Graph.Construct
{
    public class ConstructEdge : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructEdge class.
        /// </summary>
        public ConstructEdge()
          : base("Construct Edge", "CEdge",
              "Creates edges from input curves and optional JSON strings.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input curves as tree
            pManager.AddCurveParameter("Curves", "C", "Input curves as a tree", GH_ParamAccess.tree);
            // Input types as tree (optional)
            pManager.AddTextParameter("Types", "T", "Types as a tree", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            // Input JSON strings as tree (optional)
            pManager.AddTextParameter("Attributes", "A", "Attributes as JSON string tree", GH_ParamAccess.tree);
            pManager[2].Optional = true;
            // Input linked objects as tree (optional)
            pManager.AddGenericParameter("Linked Objects", "L", "Linked objects as a tree", GH_ParamAccess.tree);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Output edges as tree
            pManager.AddGenericParameter("Edges", "E", "Output edges as a tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<GH_Curve> curvesTree = new GH_Structure<GH_Curve>();
            GH_Structure<GH_String> typesTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();
            GH_Structure<IGH_Goo> linkedObjectsTree = new();

            // Get input data
            if (!DA.GetDataTree(0, out curvesTree)) return;
            DA.GetDataTree(1, out typesTree);
            DA.GetDataTree(2, out attributesTree);
            DA.GetDataTree(3, out linkedObjectsTree);

            // Validate and simplify input trees
            TreeUtils.ValidateTreeStructure(curvesTree, curvesTree);
            TreeUtils.ValidateTreeStructure(curvesTree, typesTree, repeatLast: true, defaultValue: new GH_String("unset"));
            TreeUtils.ValidateTreeStructure(curvesTree, attributesTree, repeatLast: true);
            TreeUtils.ValidateTreeStructure(curvesTree, linkedObjectsTree, raiseEqualBranchItemCountError: true);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> edgesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees using a for loop
            for (int pathIndex = 0; pathIndex < curvesTree.Paths.Count; pathIndex++)
            {
                // Use ExtractBranchData to get branches for the current path
                var curves = TreeUtils.ExtractBranchData(curvesTree, pathIndex);
                var types = TreeUtils.ExtractBranchData(typesTree, pathIndex);
                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex);
                var linkedObjects = TreeUtils.ExtractBranchData<IGH_Goo>(linkedObjectsTree, pathIndex);

                // Create edges from curves, types, attributes, and linked objects
                for (int i = 0; i < curves.Count; i++)
                {
                    var curve = curves[i];
                    var type = i < types.Count ? types[i] : null;
                    var attributeJson = i < attributes.Count ? attributes[i] : null;
                    var linkedObject = i < linkedObjects.Count ? linkedObjects[i] : null;

                    // Create edge
                    GEdge edge = new GEdge(curve, type);

                    // Deserialize attributes and add to edge if available
                    if (attributeJson != null)
                    {
                        var attributeDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(attributeJson);
                        foreach (var attribute in attributeDict)
                        {
                            edge.Attributes[attribute.Key] = attribute.Value;
                        }
                    }

                    // Add linked object to edge if available
                    if (linkedObject != null)
                    {
                        edge.LinkedObjects.Add(linkedObject);
                    }

                    // Wrap the Edge object
                    GH_ObjectWrapper edgeWrapper = new GH_ObjectWrapper(edge);

                    // Append the edge to the output tree at the current path
                    edgesTree.Append(edgeWrapper, curvesTree.Paths[pathIndex]);
                }
            }



            // Set output data
            DA.SetDataTree(0, edgesTree);
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
            get { return new Guid("BD1CC09B-05A5-47D8-89B6-A45EFDC860D5"); }
        }
    }
}
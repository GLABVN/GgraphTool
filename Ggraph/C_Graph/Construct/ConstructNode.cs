using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Glab.Utilities;

namespace Glab.C_Graph.Construct
{
    public class ConstructNode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ConstructNode class.
        /// </summary>
        public ConstructNode()
          : base("Construct Node", "CNode",
              "Creates nodes from input points and optional JSON strings.",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Input points as tree
            pManager.AddPointParameter("Points", "P", "Input points as a tree", GH_ParamAccess.tree);
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
            // Output nodes as tree
            pManager.AddGenericParameter("Nodes", "N", "Output nodes as a tree", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<GH_Point> pointsTree = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> typesTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();
            GH_Structure<IGH_Goo> linkedObjectsTree = new();

            // Get input data
            if (!DA.GetDataTree(0, out pointsTree)) return;
            DA.GetDataTree(1, out typesTree);
            DA.GetDataTree(2, out attributesTree);
            DA.GetDataTree(3, out linkedObjectsTree);

            // Validate input trees
            pointsTree = TreeUtils.ValidateTreeStructure(pointsTree, pointsTree); // Validate pointsTree against itself
            typesTree = TreeUtils.ValidateTreeStructure(pointsTree, typesTree, repeatLast: true, defaultValue: new GH_String("unset"));
            attributesTree = TreeUtils.ValidateTreeStructure(pointsTree, attributesTree, repeatLast: true);
            linkedObjectsTree = TreeUtils.ValidateTreeStructure(pointsTree, linkedObjectsTree, raiseEqualBranchItemCountError: true);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> nodesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < pointsTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var points = TreeUtils.ExtractBranchData(pointsTree, pathIndex);
                var types = TreeUtils.ExtractBranchData(typesTree, pathIndex);
                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex);
                var linkedObjects = TreeUtils.ExtractBranchData<IGH_Goo>(linkedObjectsTree, pathIndex);

                // Create nodes from points, types, attributes, and linked objects
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var type = i < types.Count ? types[i] : null;
                    var attributeJson = i < attributes.Count ? attributes[i] : null;
                    var linkedObject = i < linkedObjects.Count ? linkedObjects[i] : null;

                    // Create node
                    GNode node = new GNode(point, type);

                    // Deserialize attributes and add to node if available
                    if (attributeJson != null)
                    {
                        var attributeDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(attributeJson);
                        foreach (var attribute in attributeDict)
                        {
                            node.Attributes[attribute.Key] = attribute.Value;
                        }
                    }

                    // Add linked object to node if available
                    if (linkedObject != null)
                    {
                        node.LinkedObjects.Add(linkedObject);
                    }

                    // Wrap the Node object
                    GH_ObjectWrapper nodeWrapper = new GH_ObjectWrapper(node);

                    // Append the node to the output tree at the current path
                    nodesTree.Append(nodeWrapper, pointsTree.Paths[pathIndex]);
                }
            }

            // Set output data
            DA.SetDataTree(0, nodesTree);
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
            get { return new Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDEF0"); }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino.Geometry;
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
            GH_Structure<IGH_Goo> linkedObjectsTree = new GH_Structure<IGH_Goo>();

            // Get input data
            if (!DA.GetDataTree(0, out pointsTree)) return;
            bool hasTypes = DA.GetDataTree(1, out typesTree) && !typesTree.IsEmpty;
            bool hasAttributes = DA.GetDataTree(2, out attributesTree) && !attributesTree.IsEmpty;
            bool hasLinkedObjects = DA.GetDataTree(3, out linkedObjectsTree) && !linkedObjectsTree.IsEmpty;

            // Simplify input data trees using TreeUtils
            pointsTree = TreeUtils.SimplifyTree(pointsTree);
            if (hasTypes)
            {
                typesTree = TreeUtils.SimplifyTree(typesTree);
            }
            if (hasAttributes)
            {
                attributesTree = TreeUtils.SimplifyTree(attributesTree);
            }
            if (hasLinkedObjects)
            {
                linkedObjectsTree = TreeUtils.SimplifyTree(linkedObjectsTree);
            }

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> nodesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in pointsTree.Paths)
            {
                // Get points from the current branch
                var points = pointsTree.get_Branch(path).Cast<GH_Point>().Select(ghPoint => ghPoint.Value).ToList();
                var types = hasTypes ? typesTree.get_Branch(path).Cast<GH_String>().Select(ghString => ghString.Value).ToList() : new List<string>();
                var attributes = hasAttributes ? attributesTree.get_Branch(path).Cast<GH_String>().Select(ghString => ghString.Value).ToList() : new List<string>();
                var linkedObjects = hasLinkedObjects ? linkedObjectsTree.get_Branch(path).Cast<IGH_Goo>().ToList() : new List<IGH_Goo>();

                // Check if the count of points and types are equal
                if (hasTypes && points.Count != types.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of points and types should be equal.");
                    return;
                }

                // Check if the count of points and attributes are equal
                if (hasAttributes && points.Count != attributes.Count)
                {
                    if (attributes.Count == 1)
                    {
                        // Repeat the single attribute for all points
                        attributes = Enumerable.Repeat(attributes[0], points.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of points and attributes should be equal or attributes should have a single item.");
                        return;
                    }
                }

                // Check if the count of points and linked objects are equal
                if (hasLinkedObjects && points.Count != linkedObjects.Count)
                {
                    if (linkedObjects.Count == 1)
                    {
                        // Repeat the single linked object for all points
                        linkedObjects = Enumerable.Repeat(linkedObjects[0], points.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of points and linked objects should be equal or linked objects should have a single item.");
                        return;
                    }
                }

                // Create nodes from points, types, attributes, and linked objects
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var type = hasTypes && i < types.Count ? types[i] : null;
                    var attributeJson = hasAttributes && i < attributes.Count ? attributes[i] : null;
                    var linkedObject = hasLinkedObjects && i < linkedObjects.Count ? linkedObjects[i] : null;

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
                    nodesTree.Append(nodeWrapper, path);
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
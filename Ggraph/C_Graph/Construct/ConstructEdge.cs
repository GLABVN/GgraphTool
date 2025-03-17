using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino.Geometry;

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
            GH_Structure<IGH_Goo> linkedObjectsTree = new GH_Structure<IGH_Goo>();

            // Get input data
            if (!DA.GetDataTree(0, out curvesTree)) return;
            bool hasTypes = DA.GetDataTree(1, out typesTree) && !typesTree.IsEmpty;
            bool hasAttributes = DA.GetDataTree(2, out attributesTree) && !attributesTree.IsEmpty;
            bool hasLinkedObjects = DA.GetDataTree(3, out linkedObjectsTree) && !linkedObjectsTree.IsEmpty;

            // Simplify input data trees
            curvesTree.Simplify(GH_SimplificationMode.CollapseAllOverlaps);
            if (hasTypes)
            {
                typesTree.Simplify(GH_SimplificationMode.CollapseAllOverlaps);
            }
            if (hasAttributes)
            {
                attributesTree.Simplify(GH_SimplificationMode.CollapseAllOverlaps);
            }
            if (hasLinkedObjects)
            {
                linkedObjectsTree.Simplify(GH_SimplificationMode.CollapseAllOverlaps);
            }

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> edgesTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in curvesTree.Paths)
            {
                // Get curves from the current branch
                var curves = curvesTree.get_Branch(path).Cast<GH_Curve>().Select(ghCurve => ghCurve.Value).ToList();
                var types = hasTypes ? typesTree.get_Branch(path).Cast<GH_String>().Select(ghString => ghString.Value).ToList() : new List<string>();
                var attributes = hasAttributes ? attributesTree.get_Branch(path).Cast<GH_String>().Select(ghString => ghString.Value).ToList() : new List<string>();
                var linkedObjects = hasLinkedObjects ? linkedObjectsTree.get_Branch(path).Cast<IGH_Goo>().ToList() : new List<IGH_Goo>();

                // Check if the count of curves and types are equal
                if (hasTypes && curves.Count != types.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of curves and types should be equal.");
                    return;
                }

                // Check if the count of curves and attributes are equal
                if (hasAttributes && curves.Count != attributes.Count)
                {
                    if (attributes.Count == 1)
                    {
                        // Repeat the single attribute for all curves
                        attributes = Enumerable.Repeat(attributes[0], curves.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of curves and attributes should be equal or attributes should have a single item.");
                        return;
                    }
                }

                // Check if the count of curves and linked objects are equal
                if (hasLinkedObjects && curves.Count != linkedObjects.Count)
                {
                    if (linkedObjects.Count == 1)
                    {
                        // Repeat the single linked object for all curves
                        linkedObjects = Enumerable.Repeat(linkedObjects[0], curves.Count).ToList();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The count of curves and linked objects should be equal or linked objects should have a single item.");
                        return;
                    }
                }

                // Create edges from curves, types, attributes, and linked objects
                for (int i = 0; i < curves.Count; i++)
                {
                    var curve = curves[i];
                    var type = hasTypes && i < types.Count ? types[i] : null;
                    var attributeJson = hasAttributes && i < attributes.Count ? attributes[i] : null;
                    var linkedObject = hasLinkedObjects && i < linkedObjects.Count ? linkedObjects[i] : null;

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
                    edgesTree.Append(edgeWrapper, path);
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
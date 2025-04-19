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
    public class ModifyGraphNode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FindNodeByPoint class.
        /// </summary>
        public ModifyGraphNode()
          : base("Find/Modify Graph Node", "FindNode",
              "Finds nodes in the same position as input points",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to search", GH_ParamAccess.tree);
            pManager.AddPointParameter("Points", "P", "Points to find corresponding nodes", GH_ParamAccess.tree);
            pManager.AddTextParameter("Type to Change", "T", "New type for the found nodes", GH_ParamAccess.tree);
            pManager[2].Optional = true;
            pManager.AddTextParameter("Attributes to Change", "A", "Attributes as JSON string for the found nodes", GH_ParamAccess.tree);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Updated Graphs", "G", "Updated graphs with modified nodes", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Updated Nodes", "N", "Found nodes", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_Point> pointTree = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> typeTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out pointTree)) return;
            DA.GetDataTree(2, out typeTree);
            DA.GetDataTree(3, out attributesTree);

            // Validate input trees
            // Validate input trees
            graphTree = TreeUtils.ValidateTreeStructure(pointTree, graphTree, check1Branch1Item: true); // Validate graphTree against itself
            pointTree = TreeUtils.ValidateTreeStructure(pointTree, pointTree);
            typeTree = TreeUtils.ValidateTreeStructure(pointTree, typeTree, repeatLast: true);
            attributesTree = TreeUtils.ValidateTreeStructure(pointTree, attributesTree, repeatLast: true);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> updatedGraphTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> outputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var points = TreeUtils.ExtractBranchData(pointTree, pathIndex);
                var types = TreeUtils.ExtractBranchData(typeTree, pathIndex);
                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex);

                // Process each graph and corresponding points
                for (int i = 0; i < points.Count; i++)
                {
                    string type = types[i];
                    string attributesJson = attributes[i];
                    Dictionary<string, object> attributeDict = attributesJson != null
                        ? JsonConvert.DeserializeObject<Dictionary<string, object>>(attributesJson)
                        : null;

                    var node = GraphUtils.FindEditNode(graphs[0], points[i], type, attributeDict);
                    if (node != null)
                    {
                        // Wrap the Node object
                        GH_ObjectWrapper nodeWrapper = new GH_ObjectWrapper(node);

                        // Append the node to the output tree at the current path
                        outputTree.Append(nodeWrapper, graphTree.Paths[pathIndex]);
                    }
                    
                }
                updatedGraphTree.Append(new GH_ObjectWrapper(graphs[0]), graphTree.Paths[pathIndex]);
            }

            // Set output data
            DA.SetDataTree(0, updatedGraphTree);
            DA.SetDataTree(1, outputTree);
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
            get { return new Guid("EE293650-5ACD-4331-8AC2-8CE12F552672"); }
        }
    }
}
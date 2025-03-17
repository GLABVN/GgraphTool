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
    public class ModifyGraphEdge : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FindEdgeByLine class.
        /// </summary>
        public ModifyGraphEdge()
          : base("Find/Modify Graph Edge", "FindEdge",
              "Finds edges corresponding to input lines",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to search", GH_ParamAccess.tree);
            pManager.AddLineParameter("Lines", "L", "Lines to find corresponding edges", GH_ParamAccess.tree);
            pManager.AddTextParameter("Type", "T", "New type for the found edges", GH_ParamAccess.tree);
            pManager[2].Optional = true;
            pManager.AddTextParameter("Attributes", "A", "Attributes as JSON string for the found edges", GH_ParamAccess.tree);
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Updated Graphs", "G", "Updated graphs with modified edges", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Updated Edges", "E", "Found edges", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_Line> lineTree = new GH_Structure<GH_Line>();
            GH_Structure<GH_String> typeTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            if (!DA.GetDataTree(1, out lineTree)) return;
            bool hasType = DA.GetDataTree(2, out typeTree) && !typeTree.IsEmpty;
            bool hasAttributes = DA.GetDataTree(3, out attributesTree) && !attributesTree.IsEmpty;

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            lineTree = TreeUtils.SimplifyTree(lineTree);
            typeTree = TreeUtils.SimplifyTree(typeTree);
            attributesTree = TreeUtils.SimplifyTree(attributesTree);

            // Initialize output data structures
            GH_Structure<GH_ObjectWrapper> updatedGraphTree = new GH_Structure<GH_ObjectWrapper>();
            GH_Structure<GH_ObjectWrapper> outputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input trees
            foreach (GH_Path path in graphTree.Paths)
            {
                // Get graphs and lines from the current branch
                var graphs = graphTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    Graph graph = null;
                    goo.CastTo(out graph);
                    return graph;
                }).ToList();

                var lines = lineTree.get_Branch(path).Cast<GH_Line>().Select(ghLine => ghLine.Value).ToList();

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

                // Iterate through each line and corresponding graphs
                for (int i = 0; i < lines.Count; i++)
                {
                    string type = types.Count > 0 ? types[i % types.Count] : null;
                    string attributesJson = attributes.Count > 0 ? attributes[i % attributes.Count] : null;
                    Dictionary<string, object> attributeDict = attributesJson != null
                        ? JsonConvert.DeserializeObject<Dictionary<string, object>>(attributesJson)
                        : null;

                    foreach (var graph in graphs)
                    {
                        var edge = GraphUtils.FindEditEdge(graph, lines[i], type, attributeDict);
                        if (edge != null)
                        {
                            // Wrap the Edge object
                            GH_ObjectWrapper edgeWrapper = new GH_ObjectWrapper(edge);

                            // Append the edge to the output tree at the current path
                            outputTree.Append(edgeWrapper, path);
                        }
                    }
                }

                // Append the updated graphs to the output tree
                foreach (var graph in graphs)
                {
                    updatedGraphTree.Append(new GH_ObjectWrapper(graph), path);
                }
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
            get { return new Guid("05F9103E-45ED-4FD1-A191-2C38E14E9D17"); }
        }
    }
}



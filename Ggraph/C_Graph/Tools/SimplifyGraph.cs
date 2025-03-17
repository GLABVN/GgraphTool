using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Glab.Utilities;

namespace Glab.C_Graph.Tools
{
    public class SimplifyGraph : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SimplifyGraph class.
        /// </summary>
        public SimplifyGraph()
          : base("Simplify Graph", "SimplifyGraph",
              "Simplifies graphs by removing nodes with specific valence and angle criteria",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to simplify", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Min Angle", "MinA", "Minimum angle to consider for node removal", GH_ParamAccess.item, 160.0);
            pManager.AddNumberParameter("Max Angle", "MaxA", "Maximum angle to consider for node removal", GH_ParamAccess.item, 180.0);
            pManager.AddBooleanParameter("Collapse Short Edges?", "CSE", "Option to collapse short edges before simplifying by angle", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Min Length", "MinL", "Minimum length to consider for edge collapse", GH_ParamAccess.item, 1.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Simplified Graphs", "SG", "Simplified graphs", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            double minAngle = 160.0;
            double maxAngle = 180.0;
            bool collapseShortEdges = false;
            double minLength = 1.0;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            DA.GetData(1, ref minAngle);
            DA.GetData(2, ref maxAngle);
            DA.GetData(3, ref collapseShortEdges);
            DA.GetData(4, ref minLength);

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> outputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input tree
            foreach (GH_Path path in graphTree.Paths)
            {
                // Get branches for the current path
                List<Graph> graphs = graphTree.get_Branch(path).Cast<IGH_Goo>().Select(goo =>
                {
                    Graph graph = null;
                    goo.CastTo(out graph);
                    return graph;
                }).ToList();

                // Process each graph
                foreach (var graph in graphs)
                {
                    Graph modifiedGraph = graph.DeepCopy();

                    // Collapse short edges if the option is enabled
                    if (collapseShortEdges)
                    {
                        modifiedGraph = GraphUtils.CollapseShortEdges(modifiedGraph, minLength);
                    }

                    // Simplify the graph by angle
                    GraphUtils.SimplifyGraphByAngle(modifiedGraph, minAngle, maxAngle);
                    outputTree.Append(new GH_ObjectWrapper(modifiedGraph), path);
                }
            }

            // Set output data
            DA.SetDataTree(0, outputTree);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("3501275F-39BD-465A-BF9E-921B6B8C561C"); }
        }
    }
}
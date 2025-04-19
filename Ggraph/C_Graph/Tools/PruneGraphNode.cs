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
    public class PruneGraphNode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PruneNodeByType class.
        /// </summary>
        public PruneGraphNode()
          : base("Prune Graph Node", "PruneNodes",
              "Prunes nodes in the input graphs by valence, type, and attributes",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to prune", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Valence To Prune", "V", "Valence of nodes to prune", GH_ParamAccess.tree, 1);
            pManager.AddTextParameter("Type To Exclude", "T", "Type of nodes to exclude", GH_ParamAccess.tree);
            pManager.AddTextParameter("Attributes To Exclude", "A", "JSON string of attributes to exclude", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Prune Type Unset Only", "P", "Prune nodes with type = null only", GH_ParamAccess.item, true);

            // Make all parameters except Graphs and PruneNullOnly optional
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Pruned Graphs", "PG", "Pruned graphs", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            GH_Structure<GH_Integer> valenceTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_String> typeTree = new GH_Structure<GH_String>();
            GH_Structure<GH_String> attributesTree = new GH_Structure<GH_String>();
            bool pruneUnsetTypeOnly = true;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            DA.GetDataTree(1, out valenceTree);
            DA.GetDataTree(2, out typeTree);
            DA.GetDataTree(3, out attributesTree);
            DA.GetData(4, ref pruneUnsetTypeOnly);

            // Validate input trees
            // Validate input trees
            graphTree = TreeUtils.ValidateTreeStructure(graphTree, graphTree); // Validate graphTree against itself
            valenceTree = TreeUtils.ValidateTreeStructure(graphTree, valenceTree, repeatLast: true, defaultValue: new GH_Integer(1));
            typeTree = TreeUtils.ValidateTreeStructure(graphTree, typeTree, repeatLast: true);
            attributesTree = TreeUtils.ValidateTreeStructure(graphTree, attributesTree, repeatLast: true);

            // Initialize output data structure
            GH_Structure<GH_ObjectWrapper> outputTree = new GH_Structure<GH_ObjectWrapper>();

            // Iterate through paths in the input tree
            for (int pathIndex = 0; pathIndex < graphTree.Paths.Count; pathIndex++)
            {
                // Extract branches for the current path
                var graphs = TreeUtils.ExtractBranchData<Graph>(graphTree, pathIndex);
                var valences = TreeUtils.ExtractBranchData(valenceTree, pathIndex);
                var types = TreeUtils.ExtractBranchData(typeTree, pathIndex);
                var attributes = TreeUtils.ExtractBranchData(attributesTree, pathIndex);

                // Process each graph in the current branch
                for (int i = 0; i < graphs.Count; i++)
                {
                    var graph = graphs[i];
                    var valence = valences[i];
                    var type = types[i];
                    var jsonString = attributes[i];

                    // Prune the graph with matching parameters
                    var prunedGraph = GraphUtils.PruneGraphByType(graph, type, jsonString, valence, pruneUnsetTypeOnly);
                    outputTree.Append(new GH_ObjectWrapper(prunedGraph), graphTree.Paths[pathIndex]);
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
            get { return new Guid("3501275F-39BD-465A-BF9E-921B6B8C560C"); }
        }
    }
}
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
            pManager.AddBooleanParameter("Prune Type Null Only", "P", "Prune nodes with type = null only", GH_ParamAccess.item, true);

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
            bool pruneNullOnly = true;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree)) return;
            bool hasValence = DA.GetDataTree(1, out valenceTree) && !valenceTree.IsEmpty;
            bool hasType = DA.GetDataTree(2, out typeTree) && !typeTree.IsEmpty;
            bool hasAttributes = DA.GetDataTree(3, out attributesTree) && !attributesTree.IsEmpty;
            DA.GetData(4, ref pruneNullOnly);

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);
            if (hasValence) valenceTree = TreeUtils.SimplifyTree(valenceTree);
            if (hasType) typeTree = TreeUtils.SimplifyTree(typeTree);
            if (hasAttributes) attributesTree = TreeUtils.SimplifyTree(attributesTree);

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

                // Get branches for all inputs for this path
                var graphBranch = graphs;  // We already have this processed
                var valenceBranch = hasValence ? valenceTree.get_Branch(path) : null;
                var typeBranch = hasType ? typeTree.get_Branch(path) : null;
                var attributesBranch = hasAttributes ? attributesTree.get_Branch(path) : null;

                // Get the maximum count to iterate through
                int maxCount = graphBranch.Count;
                if (hasValence) maxCount = Math.Min(maxCount, valenceBranch.Count);
                if (hasType) maxCount = Math.Min(maxCount, typeBranch.Count);
                if (hasAttributes) maxCount = Math.Min(maxCount, attributesBranch.Count);

                // Process each item in parallel
                for (int i = 0; i < maxCount; i++)
                {
                    // Get the graph to process
                    var graph = graphBranch[i];

                    // Get matching parameters from the same index
                    int valence = 1;
                    if (hasValence && i < valenceBranch.Count)
                    {
                        valence = (valenceBranch[i] as GH_Integer).Value;
                    }

                    string type = null;
                    if (hasType && i < typeBranch.Count)
                    {
                        type = (typeBranch[i] as GH_String).Value;
                        if (string.IsNullOrEmpty(type)) type = null;
                    }

                    string jsonString = null;
                    if (hasAttributes && i < attributesBranch.Count)
                    {
                        jsonString = (attributesBranch[i] as GH_String).Value;
                        if (string.IsNullOrEmpty(jsonString)) jsonString = null;
                    }

                    // Prune the graph with matching parameters
                    var prunedGraph = GraphUtils.PruneGraphByType(graph, type, jsonString, valence, pruneNullOnly);
                    outputTree.Append(new GH_ObjectWrapper(prunedGraph), path);
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
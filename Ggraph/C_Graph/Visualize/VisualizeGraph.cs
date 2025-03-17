using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino;
using Glab.Utilities;
using Newtonsoft.Json;

namespace Glab.C_Graph.Visualize
{
    public class VisualizeGraph : GH_Component
    {
        private Color previewColor = Color.Red;
        private List<Curve> edgeCurves = new List<Curve>();
        private List<Point3d> nodePoints = new List<Point3d>();
        private List<TextDot> nodeValenceDots = new List<TextDot>();
        private List<TextDot> edgeValenceDots = new List<TextDot>();
        private List<(string text, Point3d position)> nodeAttributeTexts = new List<(string, Point3d)>();

        /// <summary>
        /// Initializes a new instance of the VisualizeGraph class.
        /// </summary>
        public VisualizeGraph()
          : base("Visualize Graph", "VisualizeGraph",
              "Visualizes graphs by extracting edge curves, node points, and valence number text objects",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "Tree of graphs to visualize", GH_ParamAccess.tree);
            pManager.AddColourParameter("Preview Color", "C", "Color for text preview", GH_ParamAccess.item, Color.Black);
            pManager.AddBooleanParameter("Show Node Valence", "SN", "Show valence number text dots for nodes", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Show Edge Valence", "SE", "Show valence number text dots for edges", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Show Node Attribute", "SA", "Show node attribute text dots", GH_ParamAccess.item, false);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Initialize input variables
            GH_Structure<IGH_Goo> graphTree = new GH_Structure<IGH_Goo>();
            bool showNodeValence = true;
            bool showEdgeValence = true;
            bool showNodeAttribute = false;

            // Get input data
            if (!DA.GetDataTree(0, out graphTree))
            {
                // Clear previous data if graphTree is null
                edgeCurves.Clear();
                nodePoints.Clear();
                nodeValenceDots.Clear();
                edgeValenceDots.Clear();
                nodeAttributeTexts.Clear();
                return;
            }

            // Get the preview color
            GH_Colour ghColor = null;
            if (DA.GetData(1, ref ghColor))
            {
                previewColor = ghColor.Value;
            }

            // Get the show node valence flag
            DA.GetData(2, ref showNodeValence);

            // Get the show edge valence flag
            DA.GetData(3, ref showEdgeValence);

            // Get the show node attribute flag
            DA.GetData(4, ref showNodeAttribute);

            // Simplify input data trees using TreeUtils
            graphTree = TreeUtils.SimplifyTree(graphTree);

            // Clear previous data
            edgeCurves.Clear();
            nodePoints.Clear();
            nodeValenceDots.Clear();
            edgeValenceDots.Clear();
            nodeAttributeTexts.Clear();

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
                    // Extract edge curves and node points
                    foreach (var edge in graph.QuickGraphObj.Edges)
                    {
                        edgeCurves.Add(edge.EdgeCurve);

                        // Create text dot for edge valence if showEdgeValence is true
                        if (showEdgeValence)
                        {
                            Point3d midpoint = edge.EdgeCurve.PointAtNormalizedLength(0.5);
                            string valenceText = edge.Valence.ToString();
                            TextDot textDot = new TextDot(valenceText, midpoint);
                            edgeValenceDots.Add(textDot);
                        }
                    }
                    foreach (var node in graph.QuickGraphObj.Vertices)
                    {
                        nodePoints.Add(node.Point);

                        // Create text dot for node valence if showNodeValence is true
                        if (showNodeValence)
                        {
                            string valenceText = node.Valence.ToString();
                            TextDot textDot = new TextDot(valenceText, node.Point);
                            nodeValenceDots.Add(textDot);
                        }

                        // Create text for node attribute if showNodeAttribute is true
                        if (showNodeAttribute)
                        {
                            string attributesJson = JsonConvert.SerializeObject(node.Attributes, Formatting.Indented);
                            nodeAttributeTexts.Add((attributesJson, node.Point));
                        }
                    }
                }
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);

            // Draw the edge curves
            foreach (var curve in edgeCurves)
            {
                args.Display.DrawCurve(curve, previewColor);
            }

            // Draw the node points
            foreach (var point in nodePoints)
            {
                args.Display.DrawPoint(point, Rhino.Display.PointStyle.Circle, 5, previewColor);
            }

            // Draw the node valence dots
            foreach (var textDot in nodeValenceDots)
            {
                args.Display.DrawDot(textDot, previewColor, Color.White, Color.Black);
            }

            // Draw the edge valence dots
            foreach (var textDot in edgeValenceDots)
            {
                args.Display.DrawDot(textDot, previewColor, Color.White, Color.Black);
            }

            // Draw the node attribute texts
            foreach (var (text, position) in nodeAttributeTexts)
            {
                args.Display.Draw2dText(text, Color.Aqua, position, false, 12, "Arial");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        protected override Bitmap Icon
        {
            get { return null; }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("3501275F-39BD-465A-BF9E-921B6B8C565C"); }
        }
    }
}
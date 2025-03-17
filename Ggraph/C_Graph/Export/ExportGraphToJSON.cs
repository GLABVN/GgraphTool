using System;
using System.Collections.Generic;
using System.IO;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Text;

namespace Glab.C_Graph.Export
{
    public class ExportGraphToJSON : GH_Component
    {
        private static StringBuilder _messageLog = new StringBuilder();

        /// <summary>
        /// Initializes a new instance of the SaveGraphToJSON class.
        /// </summary>
        public ExportGraphToJSON()
          : base("Export Graph To JSON", "ExportJSON",
              "Export a list of graphs to a JSON file at the specified file path",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Graphs", "G", "The list of graphs to save as JSON", GH_ParamAccess.list);
            pManager.AddTextParameter("FilePath", "P", "Full file path including filename and .json extension", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Include Geometry", "Geo", "Whether to include geometry data in the JSON. For now, EdgeCurve will be converted to Line", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Export", "E", "Trigger to export the graphs", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Message", "M", "Message log with timestamps", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Graph> graphs = new List<Graph>();
            string filePath = string.Empty;
            bool includeGeometry = true;
            bool save = false;

            if (!DA.GetDataList(0, graphs)) { AddMessage(false, "No graphs provided."); return; }
            if (!DA.GetData(1, ref filePath)) { AddMessage(false, "No file path provided."); return; }
            if (!DA.GetData(2, ref includeGeometry)) return;
            if (!DA.GetData(3, ref save)) return;

            // Only proceed if save is triggered
            if (save)
            {
                try
                {
                    // Generate JSON string from graphs
                    string jsonString = GraphUtils.ExportGraphsToJSON(graphs, includeGeometry);

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write to file
                    File.WriteAllText(filePath, jsonString);

                    // Log success message
                    AddMessage(true, $"Successfully saved graphs to {filePath}");
                }
                catch (Exception ex)
                {
                    // Log error message
                    AddMessage(false, $"Failed to save graphs: {ex.Message}");
                }
            }

            // Output message log
            DA.SetData(0, _messageLog.ToString());
        }

        private void AddMessage(bool success, string message = null)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logMessage = success ? (message ?? "Success") : $"Failure: {message}";
            _messageLog.Insert(0, $"{timestamp} - {logMessage}\n");
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.quinary;
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("26580EB1-7599-4A9E-B152-135862ABBDEF"); }
        }
    }
}
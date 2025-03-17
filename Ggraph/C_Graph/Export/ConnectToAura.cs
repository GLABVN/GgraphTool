using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Neo4j.Driver;

namespace Glab.C_Graph.Neo4j
{
    public class ConnectToAura : GH_Component
    {
        private static StringBuilder _messageLog = new StringBuilder();
        private static bool _isConnected = false;
        private static string _lastUri = string.Empty;
        private static string _lastUsername = string.Empty;
        private static string _lastPassword = string.Empty;

        /// <summary>
        /// Initializes a new instance of the ConnectToAura class.
        /// </summary>
        public ConnectToAura()
          : base("Connect To Aura", "ConnectAura",
              "Connect to Neo4j Aura instance, push graph data or clean existing data",
              "Glab", "Graph")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("URI", "URI", "The URI of the Neo4j Aura instance", GH_ParamAccess.item);
            pManager.AddTextParameter("Username", "User", "The username for Neo4j Aura", GH_ParamAccess.item, "neo4j");
            pManager.AddTextParameter("Password", "Pass", "The password for Neo4j Aura", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Connect", "Con", "Connect to Neo4j Aura when true, disconnect when false", GH_ParamAccess.item, false);
            pManager.AddGenericParameter("Graph", "G", "The graph to push to Neo4j Aura", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Push", "P", "Trigger to push the graph to Neo4j Aura", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Clean", "C", "Clean all graph data from Neo4j Aura before pushing", GH_ParamAccess.item, false);

            // Make the Graph parameter optional since it's not needed for cleaning only
            pManager[4].Optional = true;
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
            string uri = string.Empty;
            string username = "neo4j";
            string password = string.Empty;
            bool connect = false;
            Graph graph = null;
            bool push = false;
            bool clean = false;

            if (!DA.GetData(0, ref uri)) return;
            if (!DA.GetData(1, ref username)) return;
            if (!DA.GetData(2, ref password)) return;
            if (!DA.GetData(3, ref connect)) return;
            DA.GetData(4, ref graph); // Graph is optional
            if (!DA.GetData(5, ref push)) return;
            if (!DA.GetData(6, ref clean)) return;

            try
            {
                // Handle connection state based on the 'connect' parameter
                if (connect && (!_isConnected || uri != _lastUri || username != _lastUsername || password != _lastPassword))
                {
                    // Connect or reconnect if credentials changed
                    if (_isConnected)
                    {
                        Task.Run(async () => await ToNeo4j.Close()).Wait();
                    }

                    ToNeo4j.Initialize(uri, username, password);
                    _lastUri = uri;
                    _lastUsername = username;
                    _lastPassword = password;
                    _isConnected = true;
                    AddMessage(true, "Login to Neo4j Aura successful.");

                    // Check if the server is up
                    bool serverUp = Task.Run(async () => await ToNeo4j.IsServerUp()).Result;

                    if (serverUp)
                    {
                        AddMessage(true, "Neo4j server is online and responsive.");
                    }
                    else
                    {
                        AddMessage(false, "Neo4j server connection established but server is not responding correctly.");
                        _isConnected = false;  // Mark as not connected if server check fails
                    }
                }

                else if (!connect && _isConnected)
                {
                    // Disconnect if requested
                    Task.Run(async () => await ToNeo4j.Close()).Wait();
                    _isConnected = false;
                    AddMessage(true, "Disconnected from Neo4j Aura.");
                }

                // Only proceed with operations if connected
                if (_isConnected)
                {
                    // Clean database if requested
                    if (clean)
                    {
                        var cleanResult = Task.Run(async () => await ToNeo4j.CleanAllGraphData()).Result;
                        if (cleanResult)
                            AddMessage(true, "Successfully cleaned all graph data.");
                        else
                            AddMessage(false, "Failed to clean graph data.");
                    }

                    // Push graph if requested and provided
                    if (push && graph != null)
                    {
                        Task.Run(async () => await ToNeo4j.PushGraphToNeo4j(graph)).Wait();
                        AddMessage(true, "Successfully pushed graph to Neo4j Aura.");
                    }
                    else if (push && graph == null)
                    {
                        AddMessage(false, "Cannot push: No graph provided.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(false, ex.Message);
                _isConnected = false;
            }

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
            get { return new Guid("B692BBB8-813A-4C90-97A3-41F1872AE645"); }
        }
    }
}
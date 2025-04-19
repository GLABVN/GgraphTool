using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Parameters;
using System.Windows.Forms;
using System.IO;

namespace Glab.Utilities
{
    public class CustomTextParameter : Param_String
    {
        private readonly string parameterType;

        // Custom constructor with parameter type
        public CustomTextParameter(string type)
        {
            parameterType = type;
        }
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);

            // Add a separator for better visual organization
            menu.Items.Add(new ToolStripSeparator());

            // Create menu item with more descriptive text and image
            var extractMenuItem = new ToolStripMenuItem
            {
                Text = "Extract to Value List",
                ToolTipText = "Creates a dropdown value list based on parameter type",
                Name = "Extract Value List"
            };
            extractMenuItem.Click += ExtractParameter;

            menu.Items.Add(extractMenuItem);
        }

        // Modify ExtractParameter to use parameterType
        private void ExtractParameter(object sender, EventArgs e)
        {
            var document = OnPingDocument();
            if (document == null) return;

            var valueList = new GH_ValueList
            {
                ListMode = GH_ValueListMode.DropDown
            };

            valueList.ListItems.Clear();

            var items = parameterType switch
            {
                "FileName" => GetFileNames(),
                "SpaceCategory" => GetSpaceCategories(),
                "SpaceType" => GetSpaceTypes(),
                "AparmentType" => GetApartmentTypes(),
                "JustificationType" =>GetJustificationTypes(),
                "Regulation" => GetRegulation(),
                _ => new List<string>()
            };

            // Add new items
            foreach (var item in items)
            {
                valueList.ListItems.Add(new GH_ValueListItem(item, $"\"{item}\""));
            }

            // Ensure attributes are created
            if (valueList.Attributes == null)
            {
                valueList.CreateAttributes();
            }

            var pivot = Attributes.Pivot;
            var offset = new System.Drawing.PointF(pivot.X - 250, pivot.Y - 11);

            // Remove any existing connected value list
            var sources = Sources;
            if (sources.Count > 0)
            {
                foreach (var source in sources)
                {
                    if (source is GH_ValueList)
                    {
                        document.RemoveObject(source, false);
                    }
                }
            }

            valueList.Attributes.Pivot = offset;
            document.AddObject(valueList, false);
            AddSource(valueList);
        }

        public override Guid ComponentGuid => new Guid("D69E7A83-7F67-46E8-8FDD-A0CF812B22D4");

        public static List<string> GetFileNames()
        {
            string currentFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string currentDirectory = Path.GetDirectoryName(currentFilePath);
            string folderPath = Path.Combine(currentDirectory, "RhinoFiles", "TypicalLevelShape");

            if (!Directory.Exists(folderPath))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(folderPath, "*.3dm");
            var fileNames = new List<string>();
            foreach (var file in files)
            {
                fileNames.Add(Path.GetFileNameWithoutExtension(file));
            }

            return fileNames;
        }

        public static List<string> GetSpaceCategories()
        {
            return new List<string>
                {
                    "Giao thông",
                    "Căn hộ",
                    "Tiện ích",
                    "Kĩ thuật",
                    "Cảnh quan",
                    "TMDV",
                };
        }

        public static List<string> GetSpaceTypes()
        {
            return new List<string>
                {
                    "Hành lang",
                    "Thang bộ",
                    "Lõi",
                    "Đỗ xe",
                    "Nhà trẻ",
                    "Gym",
                    "Bể bơi",
                    "SHCĐ",
                    "Đường",
                    "Vỉa hè",
                    "Cây xanh"
                };
        }

        public static List<string> GetApartmentTypes()
        {
            return new List<string>
                {
                    "Tiêu chuẩn",
                    "Duplex",
                    "Penthouse"
                };
        }

        public static List<string> GetJustificationTypes()
        {
            return new List<string>
                {
                    "BottomLeft",
                    "BottomCenter",
                    "BottomRight",
                    "MiddleLeft",
                    "MiddleCenter",
                    "MiddleRight",
                    "TopLeft",
                    "TopCenter",
                    "TopRight"
                };
        }
        public static List<string> GetRegulation()
        {
            return new List<string>
                {
                    "1245/BXD-KHCN",
                    "34/2024/QĐ-UBND TP Hà Nội"
                };
        }
    }
}

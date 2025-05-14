# GgraphTool

GgraphTool is an open-source Grasshopper plugin built on the QuickGraph library. It empowers users to design and modify personalized 3D graphs with custom attributes and linked objects directly within Grasshopper.

## Features

- **3D Graph Creation & Modification:** Design, edit, add, remove, and split nodes and edges in your graph with ease.
- **Custom Attributes:** Assign user-defined data to nodes and edges for flexible graph modeling.
- **Linked Objects:** Attach external Grasshopper or Rhino objects to graph elements.
- **Shortest Path Calculation:** Find optimal paths between nodes using built-in algorithms.
- **Corridor Skeleton Extraction:** Extract skeletons for corridor layouts (even/odd are supported).
- **Dead-End Removal:** Automatically remove dead ends from your graph.
- **Edge Enhancement:** Improve and refine graph edges.
- **Export as JSON:** Save your graph data in JSON format.
- **Neo4j Aura Integration:** Seamlessly push your graph to [Neo4j Aura](https://neo4j.com/cloud/aura/), a real-time graph database service.

## Installation

1. Download or clone this repository.
2. Copy the entire `GHA` folder to your Grasshopper `Libraries` directory (typically found at `%AppData%\Grasshopper\Libraries`).

## Usage

1. Open Grasshopper in Rhino.
2. Find the **Glab** tab and drag GgraphTool components onto the canvas.
3. Use the components to create, modify, and export your graphs, or connect to Neo4j Aura.

## Requirements

- Rhino 6 or later
- Grasshopper
- .NET Framework 4.8 or compatible

## Example

See the [`GHA/Example`](GHA/Example) folder for sample files and workflows.

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or documentation improvements.

## Contact

For questions or support, please contact the author or open an issue on GitHub.
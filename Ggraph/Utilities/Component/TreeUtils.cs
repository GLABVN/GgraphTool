using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Glab.Utilities
{
    public static class TreeUtils
    {
        public static GH_Structure<T> SimplifyTree<T>(GH_Structure<T> tree) where T : IGH_Goo
        {
            // Create a new GH_Structure to store the simplified paths
            var newTree = new GH_Structure<T>();

            // Check if there's only one branch
            if (tree.Paths.Count == 1)
            {
                var singlePath = tree.Paths[0];
                var singleIndices = singlePath.Indices.ToList();

                if (singleIndices.All(x => x == 0))
                {
                    // If all indices are 0, leave the path as 0
                    singleIndices = new List<int> { 0 };
                }
                else
                {
                    // Remove all the 0s at the start and end of the list
                    singleIndices = singleIndices.SkipWhile(x => x == 0).Reverse().SkipWhile(x => x == 0).Reverse().ToList();
                }

                var simplifiedPath = new GH_Path(singleIndices.ToArray());
                newTree.AppendRange((IEnumerable<T>)tree.get_Branch(singlePath), simplifiedPath);
                return newTree;
            }
            else
            {
                // Collect all indices in a list of lists
                var allIndices = new List<List<int>>();
                int? expectedLength = null;

                foreach (var path in tree.Paths)
                {
                    var indices = path.Indices.ToList();
                    if (expectedLength == null)
                    {
                        expectedLength = indices.Count;
                    }

                    // Check if the indices list has the same length
                    if (indices.Count == expectedLength)
                    {
                        allIndices.Add(indices);
                    }
                    else
                    {
                        // Skip paths with different lengths
                        continue;
                    }
                }

                // Group indices by their positions
                var groupedIndicesDict = new Dictionary<int, List<int>>();
                foreach (var indices in allIndices)
                {
                    for (int i = 0; i < indices.Count; i++)
                    {
                        if (!groupedIndicesDict.ContainsKey(i))
                        {
                            groupedIndicesDict[i] = new List<int>();
                        }
                        groupedIndicesDict[i].Add(indices[i]);
                    }
                }

                // Process the grouped indices to remove repeated values
                foreach (var indices in allIndices)
                {
                    var simplifiedIndices = new List<int>();
                    for (int i = 0; i < indices.Count; i++)
                    {
                        var uniqueValues = new HashSet<int>(groupedIndicesDict[i]);
                        if (uniqueValues.Count > 1)
                        {
                            simplifiedIndices.Add(indices[i]);
                        }
                    }

                    var simplifiedPath = new GH_Path(simplifiedIndices.ToArray());

                    // Append the branch from the original tree to the new tree with the simplified path
                    newTree.AppendRange((IEnumerable<T>)tree.get_Branch(new GH_Path(indices.ToArray())), simplifiedPath);
                }

                // Return the new tree with simplified paths
                return newTree;
            }
        }
        
        public static IGH_Structure[] ValidateTrees<T>(bool repeat = false, params IGH_Structure[] trees) where T : IGH_Goo
        {
            // Simplify paths of each tree
            for (int i = 0; i < trees.Length; i++)
            {
                var treeType = trees[i].GetType().GetGenericArguments()[0];
                var simplifyTreeMethod = typeof(TreeUtils).GetMethod("SimplifyTree").MakeGenericMethod(treeType);
                trees[i] = (IGH_Structure)simplifyTreeMethod.Invoke(null, new object[] { trees[i] });
            }

            // Check if all trees have the same path count
            int pathCount = trees[0].PathCount;
            foreach (var tree in trees)
            {
                if (tree.PathCount != pathCount)
                    throw new InvalidOperationException("Tree structures are not identical.");
            }

            var newTrees = new IGH_Structure[trees.Length];
            for (int j = 0; j < trees.Length; j++)
            {
                newTrees[j] = new GH_Structure<T>();
            }

            // Check if all trees have the same branch count for each path
            for (int i = 0; i < pathCount; i++)
            {
                GH_Path path = trees[0].get_Path(i);
                int maxItemCount = trees.Max(tree => tree.get_Branch(path).Count);

                for (int j = 0; j < trees.Length; j++)
                {
                    var tree = trees[j];
                    if (!tree.PathExists(path))
                        throw new InvalidOperationException("Tree structures are not identical.");

                    var branch = tree.get_Branch(path);
                    if (branch.Count != maxItemCount)
                    {
                        // If repeat is true, fill the branch with the last item
                        if (repeat)
                        {
                            var lastItem = branch[branch.Count - 1];
                            while (branch.Count < maxItemCount)
                            {
                                branch.Add(lastItem);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Tree structures are not identical.");
                        }
                    }

                    ((GH_Structure<T>)newTrees[j]).AppendRange((IEnumerable<T>)branch, path);
                }
            }

            return newTrees;
        }

        public static void EnsureOneItemPerBranch<T>(GH_Structure<T> tree) where T : IGH_Goo
        {
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                if (branch.Count != 1)
                {
                    throw new InvalidOperationException($"Branch at path {path} does not contain exactly one item.");
                }
            }
        }
    }
}

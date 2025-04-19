using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Glab.Utilities
{
    public static class TreeUtils
    {
        private static GH_Structure<T> SimplifyTree<T>(GH_Structure<T> tree) where T : IGH_Goo
        {
            var newTree = new GH_Structure<T>();

            // Handle the case where the tree has no paths
            if (tree.Paths.Count == 0)
            {
                return newTree; // Return an empty tree
            }

            // Handle the case where the tree has only one branch
            if (tree.Paths.Count == 1)
            {
                var singlePath = tree.Paths[0];
                var indices = singlePath.Indices;

                if (indices.Length != 1 || indices[0] != 0)
                {
                    var simplifiedPath = new GH_Path(0);
                    newTree.AppendRange(tree.get_Branch(singlePath).Cast<T>(), simplifiedPath);
                }
                else
                {
                    newTree.AppendRange(tree.get_Branch(singlePath).Cast<T>(), singlePath);
                }

                return newTree;
            }

            // Find the maximum number of leading zeros that can be removed across all paths
            int leadingZerosToRemove = tree.Paths
                .Select(path => path.Indices.TakeWhile(index => index == 0).Count())
                .DefaultIfEmpty(0) // Handle empty sequence
                .Min();

            // Check if the last index is repeated among all paths
            int? lastIndex = tree.Paths[0].Indices.LastOrDefault();
            bool allPathsHaveSameLastIndex = tree.Paths.All(path => path.Indices.Length > 0 && path.Indices.Last() == lastIndex);

            foreach (var path in tree.Paths)
            {
                var indices = path.Indices;

                // Remove leading zeros
                var simplifiedIndices = indices.Skip(Math.Min(leadingZerosToRemove, indices.Length)).ToArray();

                // Remove the last index if all paths have the same last index
                if (allPathsHaveSameLastIndex && simplifiedIndices.Length > 0)
                {
                    simplifiedIndices = simplifiedIndices.Take(simplifiedIndices.Length - 1).ToArray();
                }

                // Ensure the path is not empty
                if (simplifiedIndices.Length == 0)
                {
                    simplifiedIndices = new int[] { 0 };
                }

                try
                {
                    var simplifiedPath = new GH_Path(simplifiedIndices);
                    newTree.AppendRange(tree.get_Branch(path).Cast<T>(), simplifiedPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create a valid path for indices: {string.Join(",", simplifiedIndices)}", ex);
                }
            }

            return newTree;
        }

        public static GH_Structure<TTarget> ValidateTreeStructure<TRef, TTarget>(
            GH_Structure<TRef> refTree,
            GH_Structure<TTarget> targetTree,
            bool simplifyTree = true,
            bool checkNullEmptyTree = true,
            bool checkEmptyBranches = true,
            bool matchTreeStructure = true,
            bool checkEqualBranchCount = true,
            bool checkEqualBranchItemCount = true,
            bool raiseEqualBranchItemCountError = false,
            bool repeatLast = false,
            bool checkEqualPath = false,
            bool check1Branch1Item = false,
            TTarget defaultValue = default // If not provided = null or false or 0 etc
        )
        where TRef : IGH_Goo
        where TTarget : IGH_Goo
        {
            // Create a new tree to hold the modified structure
            var modifiedTree = new GH_Structure<TTarget>();

            // 1. Check if the target tree is null or empty
            if (checkNullEmptyTree)
            {
                if (targetTree == null || targetTree.IsEmpty)
                {
                    if (matchTreeStructure)
                    {
                        // Ensure the structure matches the reference tree
                        for (int i = 0; i < refTree.Paths.Count; i++)
                        {
                            var path = refTree.Paths[i];
                            modifiedTree.EnsurePath(path);
                            // Add the default value in each branch, even if it's null
                            modifiedTree.Append(defaultValue, path);
                        }
                    }
                    else
                    {
                        // Create a single branch with the default value
                        modifiedTree.Append(defaultValue, new GH_Path(0));
                    }
                }
                else
                {
                    // Copy the original tree structure
                    foreach (var path in targetTree.Paths)
                    {
                        modifiedTree.EnsurePath(path);
                        modifiedTree.AppendRange(targetTree.get_Branch(path).Cast<TTarget>(), path);
                    }
                }
            }

            // 2. Check if any branch in the target tree is empty
            if (checkEmptyBranches)
            {
                foreach (var path in modifiedTree.Paths)
                {
                    var branch = modifiedTree.get_Branch(path) as List<TTarget>;
                    if (branch.Count == 0 && defaultValue != null)
                    {
                        // Append the default value to the empty branch
                        modifiedTree.Append(defaultValue, path);
                    }
                }
            }

            // 3. Simplify the target tree if required
            if (simplifyTree)
            {
                var simplifiedTree = SimplifyTree(modifiedTree);
                modifiedTree = simplifiedTree;
            }

            // 4. Check if each branch in the target tree contains exactly one item
            if (check1Branch1Item)
            {
                for (int i = 0; i < modifiedTree.Paths.Count; i++)
                {
                    var path = modifiedTree.Paths[i];
                    var branch = modifiedTree.get_Branch(path) as List<TTarget>;

                    if (branch.Count != 1)
                    {
                        throw new InvalidOperationException($"Branch at path {path} should contain exactly one item, but it contains {branch.Count} items.");
                    }
                }
            }

            // 5. Check if the number of branches is the same
            if (checkEqualBranchCount)
            {
                if (refTree.Paths.Count != modifiedTree.Paths.Count)
                {
                    throw new InvalidOperationException("The number of branches among inputs does not match.");
                }

                for (int i = 0; i < refTree.Paths.Count; i++)
                {
                    GH_Path pathRef = refTree.Paths[i];
                    GH_Path pathTar = modifiedTree.Paths[i];

                    // Get the branches
                    List<TRef> branchRef = refTree.get_Branch(pathRef) as List<TRef>;
                    List<TTarget> branchTar = modifiedTree.get_Branch(pathTar) as List<TTarget>;

                    // 6. Check if the paths are the same
                    if (checkEqualPath && !pathRef.Equals(pathTar))
                    {
                        throw new InvalidOperationException($"Path mismatch: Reference path {pathRef} does not equal Target path {pathTar}.");
                    }

                    // 7. Check if the number of items in the branches is the same
                    if (checkEqualBranchItemCount && branchRef.Count != branchTar.Count)
                    {
                        var lastItem = branchTar.Last();
                        if (repeatLast)
                        {
                            branchTar.AddRange(Enumerable.Repeat(lastItem, branchRef.Count - branchTar.Count));
                        }
                        else if (raiseEqualBranchItemCountError)
                        {
                            if (lastItem != null)
                            {
                                throw new InvalidOperationException($"The number of items in the branches does not match. Reference branch count: {branchRef.Count}, Target branch count: {branchTar.Count}.");
                            }
                        }
                    }
                }
            }

            return modifiedTree;
        }

        public static bool CrossCondition(List<List<bool>> matches)
        {
            var result = new List<bool>();

            for (int i = 0; i < matches.Count; i++)
            {
                var list2Branch = matches[i];

                // Perform a mass OR boolean operation on list2Branch
                bool list2Result = list2Branch.Any(value => value);

                if (list2Result) 
                {
                    result.Add(true);
                }
                else
                {
                    result.Add(false);
                }
            }
            return result.All(v => v);
        }

        // For GH_String to string conversion
        public static List<string> ExtractBranchData(GH_Structure<GH_String> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_String>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<string>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? null).ToList() ?? new List<string>();
        }

        // For GH_Point to Point3d conversion
        public static List<Point3d> ExtractBranchData(GH_Structure<GH_Point> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");
            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Point>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<Point3d>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? Point3d.Unset).ToList() ?? new List<Point3d>();
        }

        // For GH_Number to double conversion
        public static List<double> ExtractBranchData(GH_Structure<GH_Number> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Number>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<double>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<double>();
        }

        // For GH_Integer to int conversion
        public static List<int> ExtractBranchData(GH_Structure<GH_Integer> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Integer>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<int>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<int>();
        }

        // For GH_Boolean to bool conversion
        public static List<bool> ExtractBranchData(GH_Structure<GH_Boolean> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Boolean>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<bool>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<bool>();
        }

        // For GH_Curve to Curve conversion
        public static List<Curve> ExtractBranchData(GH_Structure<GH_Curve> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Curve>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<Curve>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? null).ToList() ?? new List<Curve>();
        }

        // For GH_Line to Line conversion
        public static List<Line> ExtractBranchData(GH_Structure<GH_Line> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Line>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<Line>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<Line>();
        }

        // For GH_Colour to Color conversion
        public static List<System.Drawing.Color> ExtractBranchData(GH_Structure<GH_Colour> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Colour>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<System.Drawing.Color>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<System.Drawing.Color>();
        }

        // For GH_Vector to Vector3d conversion
        public static List<Vector3d> ExtractBranchData(GH_Structure<GH_Vector> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Vector>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<Vector3d>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? default).ToList() ?? new List<Vector3d>();
        }

        // For GH_Plane to Plane conversion
        public static List<Plane> ExtractBranchData(GH_Structure<GH_Plane> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<GH_Plane>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<Plane>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.Value ?? Plane.WorldXY).ToList() ?? new List<Plane>();
        }

        // For custom class types wrapped in IGH_Goo (typically from GH_ObjectWrapper)
        public static List<T> ExtractBranchData<T>(GH_Structure<IGH_Goo> tree, int i, bool returnEmptyForNull = false) where T : class
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<IGH_Goo>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<T>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item is GH_ObjectWrapper wrapper ? wrapper.Value as T : null).ToList() ?? new List<T>();
        }

        // For IGH_GeometricGoo to GeometryBase conversion
        public static List<GeometryBase> ExtractBranchData(GH_Structure<IGH_GeometricGoo> tree, int i, bool returnEmptyForNull = false)
        {
            if (i < 0 || i >= tree.Paths.Count)
                throw new ArgumentOutOfRangeException(nameof(i), "Index is out of range for the tree paths.");

            var path = tree.Paths[i];
            var branch = tree.get_Branch(path) as List<IGH_GeometricGoo>;
            if (returnEmptyForNull && branch != null && branch.Any(item => item == null))
            {
                return new List<GeometryBase>(); // If returnEmptyForNull is true and any item in the branch is null, return an empty list
            }
            return branch?.Select(item => item?.ScriptVariable() as GeometryBase).ToList() ?? new List<GeometryBase>();
        }


    }
}

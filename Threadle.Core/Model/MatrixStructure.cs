using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Model
{
    public class MatrixStructure : IStructure
    {
        private Dictionary<string, int> labelToIndex = new();
        private readonly double[,] _values;

        public string Name { get; set; }
        //public string Type => "matrix";

        /// <summary>
        /// Returns content info about this structure as a list of strings
        /// </summary>
        public List<string> Content
        {
            get
            {
                List<string> lines = [$"MatrixStructure: {Name}"];
                return lines;
            }
        }

        /// <summary>
        /// Gets or sets the Filename (if this structure is loaded or saved to file)
        /// </summary>
        public string Filepath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the flag whether this structure has been modified or not since last load/initiation
        /// </summary>
        public bool IsModified { get; set; }

        //public string Content
        //{
        //    get {
        //        string content = $"\t{string.Join("\t", Labels)}";
        //        for (int r = 0; r < Size; r++)
        //        {
        //            content += $"{Environment.NewLine}{Labels[labelToIndex[Labels[r]]]}";
        //            for (int c = 0; c < Size; c++)
        //                content += $"\t{_values[labelToIndex[Labels[r]], labelToIndex[Labels[c]]].ToString()}";
        //        }
        //        return content;
        //    }
        //}
        public List<string> Labels { get; set; }
        public bool IsSymmetric { get; }
        public int Size { get => Labels.Count; }

        //public MatrixStructure(string name, Nodeset nodeset, bool isSymmetric = false) : this(name, nodeset.GetAllNodeIdsAsStringList(), isSymmetric) { }

        public MatrixStructure(string name, List<string> labels, bool isSymmetric = false)
        {
            Name = name;
            Labels = labels;
            IsSymmetric = isSymmetric;
            _values = new double[Size, Size];
            for (int i = 0; i < Size; i++)
                labelToIndex.Add(labels[i], i);
        }

        public void Set(uint rowId, uint colId, double value)
        {
            Set(rowId.ToString(), colId.ToString(), value);
        }

        public void Set(string row, string column, double value)
        {
            int r = labelToIndex[row], c = labelToIndex[column];
            _values[r, c] = value;
            if (IsSymmetric)
                _values[c, r] = value;
        }

        public double Get(uint rowId, uint colId)
        {
            return Get(rowId.ToString(), colId.ToString());
        }

        public double Get(string row, string column)
        {
            return _values[labelToIndex[row], labelToIndex[column]];
        }
    }
}

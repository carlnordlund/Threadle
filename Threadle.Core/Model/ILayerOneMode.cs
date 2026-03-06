using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Threadle.Core.Model.Enums;

namespace Threadle.Core.Model
{
    public interface ILayerOneMode : ILayer
    {
        #region Properties
        EdgeDirectionality Directionality { get; }
        EdgeType EdgeValueType { get; }
        bool IsSymmetric { get; }
        bool IsDirectional { get; }
        bool IsBinary { get; }
        bool IsValued { get; }
        bool Selfties { get; }

        /// <summary>
        /// Returns the total number of edges in this 1-mode layer
        /// </summary>
        uint NbrEdges { get; }
        #endregion


        # region Methodes
        /// <summary>
        /// Returns a paginated list of all edges in the layer, each edge described as a dictionary with
        /// keys "node1" and "node2" (and for valued layers also "value").
        /// For undirected layers, each edge is returned only once (lower node id first).
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        List<Dictionary<string, object>> GetAllEdges(int offset = 0, int limit = 10000);
        #endregion

    }
}

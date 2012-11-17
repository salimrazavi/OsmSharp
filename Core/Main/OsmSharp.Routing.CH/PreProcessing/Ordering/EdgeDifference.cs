﻿// OsmSharp - OpenStreetMap tools & library.
// Copyright (C) 2012 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OsmSharp.Routing.Core.Graph;
using OsmSharp.Routing.Core.Graph.DynamicGraph;

namespace OsmSharp.Routing.CH.PreProcessing.Ordering
{
    /// <summary>
    /// The edge difference calculator.
    /// </summary>
    public class EdgeDifference : INodeWeightCalculator
    {
        /// <summary>
        /// Holds the witness calculator.
        /// </summary>
        private INodeWitnessCalculator _witness_calculator;

        /// <summary>
        /// Holds the data.
        /// </summary>
        private IDynamicGraph<CHEdgeData> _data;

        /// <summary>
        /// Creates a new edge difference calculator.
        /// </summary>
        /// <param name="graph"></param>
        public EdgeDifference(IDynamicGraph<CHEdgeData> data, INodeWitnessCalculator witness_calculator)
        {
            _data = data;
            _witness_calculator = witness_calculator;
        }

        /// <summary>
        /// Calculates the edge-difference if u would be contracted.
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        public float Calculate(uint vertex)
        {
            // simulate the construction of new edges.
            int new_edges = 0;
            int removed = 0;

            // get the neighbours.
            KeyValuePair<uint, CHEdgeData>[] neighbours = _data.GetArcs(vertex);

            foreach (KeyValuePair<uint, CHEdgeData> from in neighbours)
            { // loop over all incoming neighbours
                if(!from.Value.Backward) {continue;}

                foreach (KeyValuePair<uint, CHEdgeData> to in neighbours)
                { // loop over all outgoing neighbours
                    if(!to.Value.Forward) {continue;}

                    if (to.Key != from.Key)
                    { // the neighbours point to different vertices.
                        // a new edge is needed.
                        if (!_witness_calculator.Exists(from.Key, to.Key, vertex,
                            (float)from.Value.Weight + (float)to.Value.Weight))
                        { // no witness exists.
                            new_edges++;
                        }
                    }
                }

                // count the edges.
                //if (from.Value.Forward)
                //{
                //    removed++;
                //}
                if (from.Value.Backward)
                { // backward edges are removed; routing only to a higher level.
                    removed++;
                }
            }
            return new_edges - removed;
        }

        /// <summary>
        /// Notifies this calculator that the vertex was contracted.
        /// </summary>
        /// <param name="vertex_id"></param>
        public void NotifyContracted(uint vertex)
        {

        }
    }
}
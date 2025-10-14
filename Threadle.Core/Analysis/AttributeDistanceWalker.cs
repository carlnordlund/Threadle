using PopnetEngine.Core.Model;
using PopnetEngine.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PopnetEngine.Core.Analysis
{
    public class AttributeDistanceWalker
    {
        public NetworkModel? Network {  get; set; }
        public byte LayerId { get; set; } = 0;
        public string AttributeName { get; set; } = "";
        public int MaxStepsPerWalk { get; set; } = 5;
        public int Restarts { get; set; } = 1;
        public float CheckProb { get; set; } = 1f;

        public StructureResult? StructureResult { get; private set; }

        public void Run(Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Starting walk...");

            StructureResult = DoWalk();
            onProgress?.Invoke("Finished.");
        }

        private StructureResult? DoWalk()
        {
            HashSet<string> categoryCollection = new();
            if (Network == null)
                throw new Exception($"!Error: Network must be set before calling Run.");

            // Don't think I need this
            //attrDefinition = Network.Nodeset.GetNodeAttributeDefinitionByAttributeName(AttributeName)
            //    ?? throw new Exception($"Error: Node attribute '{AttributeName}' not found.");

            // Each attribute has an index that is used to find the right attribute at the node level
            // So get this index: its the same for all nodes
            byte attributeIndex = Network.Nodeset.FindAttributeIndex(AttributeName)
                ?? throw new Exception($"Error: Node attribute '{AttributeName}' not found.");

            // Prepare storage for measuring stuff. Note: as max distance is <= 255, then it is
            // more efficient to store these as bytes. Which is good: might be quite a lot of these measures
            //Dictionary<string, List<byte>> withinGroupDistances = new();
            Dictionary<(string, string), List<int>> betweenGroupDistances = new();

            int totalNbrSteps = 0;
            

            for (int i = 0; i < Restarts; i++)
            
                foreach (Node startNode in Network.Nodeset.Nodes)
                {
                    if (!(startNode.GetAttribute(attributeIndex) is NodeAttribute startAttribute))
                        break;

                    string startValue = startAttribute.ToString();

                    categoryCollection.Add(startValue);
                    
                    Node currentNode = startNode;

                    int stepCount = 0, distance = 0;

                    while (stepCount < MaxStepsPerWalk)
                    {
                        // 1. Move to next node, increase distance, increase step
                        // Get an alter
                        //List<Connection>? connections = Network.GetAlters(LayerId, currentNode);
                        List<Connection>? connections = Network.GetAltersAllLayers(currentNode);
                        if (connections == null || connections.Count == 0)
                            break;
                        currentNode = Network.Nodeset.GetNodeById(Misc.GetRandom<Connection>(connections).partnerNodeId);
                        distance++;
                        stepCount++;

                        // 2. Get attribute of new node
                        if (Misc.Random.NextDouble() < CheckProb)
                        {

                            if (currentNode.GetAttribute(attributeIndex) is NodeAttribute currentAttribute)
                            {
                                string currentValue = currentAttribute.ToString();
                                categoryCollection.Add(currentValue);

                                // 3. Check if attribute is different
                                if (!startValue.Equals(currentValue))
                                {
                                    // store value
                                    if (!betweenGroupDistances.TryGetValue((startValue, currentValue), out var list))
                                    {
                                        list = new List<int>();
                                        betweenGroupDistances[(startValue, currentValue)] = list;
                                    }
                                    list.Add(distance);
                                    distance = 0;
                                    startValue = currentValue;
                                }
                            }
                        }
                        totalNbrSteps++;
                    }



                    //Console.WriteLine("Starting walk: restart " + i);
                    // Pick random start node:
                    //Node currentNode = Network.Nodeset.GetRandomNode();
                    //Node currentNode = startNode;
                    //Console.WriteLine("First node: " + currentNode.Id);

                    // Initialize queue/buffer
                    //Queue<CategoryStep> buffer = new();

                    //int stepCount = 0;
                    //while (stepCount < MaxSteps)
                    //{
                    //    // Ok - get specific NodeAttribute that we are tracking of the current node
                    //    NodeAttribute? attribute = currentNode.GetAttribute(attributeIndex);

                    //    if (attribute.ToString() is string currentAttrValue)
                    //    {
                    //        categoryCollection.Add(currentAttrValue);
                    //        // Ok - this node has this attribute
                    //        // iterate through buffer
                    //        //foreach (CategoryStep cs in buffer)
                    //        //{
                    //        //    int distance = cs.StepsAgo + 1;
                    //        //    if (currentAttrValue.Equals(cs.AttrValue))
                    //        //    {
                    //        //        // Recording a within-group distance
                    //        //        if (!withinGroupDistances.TryGetValue(currentAttrValue, out var list))
                    //        //        {
                    //        //            list = new List<byte>();
                    //        //            withinGroupDistances[currentAttrValue]= list;
                    //        //        }
                    //        //        list.Add((byte)distance);
                    //        //    }
                    //        //    else
                    //        //    {
                    //        //        // Recording a between-group distance
                    //        //        if (!betweenGroupDistances.TryGetValue((cs.AttrValue, currentAttrValue), out var list))
                    //        //        {
                    //        //            list = new List<byte>();
                    //        //            betweenGroupDistances[(cs.AttrValue, currentAttrValue)] = list;
                    //        //        }
                    //        //        list.Add((byte)distance);
                    //        //    }
                    //        //}

                    //        // Add to queue (and only do this if the current node actually had this attribute
                    //        //CategoryStep newCategoryStep = (buffer.Count == BufferSize) ? buffer.Dequeue() : new CategoryStep();
                    //        //newCategoryStep.StepsAgo = 0;
                    //        //newCategoryStep.AttrValue = currentAttrValue;
                    //        //buffer.Enqueue(newCategoryStep);
                    //    }

                    //    // Increase all stepsAgo in the buffer (also if the node didn't have this attribute)
                    //    //foreach (CategoryStep cs in buffer)
                    //    //    cs.StepsAgo++;

                    //    // Get connections of the current node
                    //    List<Connection>? connections = Network.GetAlters(LayerId, currentNode);
                    //    if (connections == null || connections.Count == 0)
                    //    {
                    //        //Console.WriteLine("Couldn't find an outbound alter for this node.");
                    //        break;
                    //    }
                    //    // Pick a random connection
                    //    Connection connection = Misc.GetRandom<Connection>(connections);

                    //    // Move the walker to this target
                    //    currentNode = Network.Nodeset.GetNodeById(connection.partnerNodeId);
                    //    //Console.WriteLine("Ok, moving to neighbor: " + currentNode.Id);

                    //    stepCount++;
                    //    totalNbrSteps++;
                    //}

                }
            Console.WriteLine($"Total number of steps: {totalNbrSteps} - found {categoryCollection.Count} unique values for attribute {AttributeName}");
            List<string> matrixLabels = categoryCollection.ToList();

            MatrixStructure matrixMeanDistance = new MatrixStructure("mean", matrixLabels, false);
            MatrixStructure matrixMedianDistance = new MatrixStructure("median", matrixLabels, false);
            MatrixStructure matrixStdev = new MatrixStructure("stdev", matrixLabels, false);
            MatrixStructure matrixNbrDataPoints = new MatrixStructure("nbr_points", matrixLabels, false);
            MatrixStructure matrixMaxVals = new MatrixStructure("max", matrixLabels, false);
            MatrixStructure matrixMinVals = new MatrixStructure("min", matrixLabels, false);
            //foreach (var kvp in withinGroupDistances)
            //{
            //    matrixMeanDistance.Set(kvp.Key, kvp.Key, Math.Round(Misc.GetMean(kvp.Value), 4));
            //    matrixStdev.Set(kvp.Key, kvp.Key, Math.Round(Misc.GetStandardDeviation(kvp.Value), 4));
            //    matrixNbrDataPoints.Set(kvp.Key, kvp.Key, kvp.Value.Count);
            //}

            string row = "", col = "";
            foreach (var kvp in betweenGroupDistances)
            {
                row = kvp.Key.Item1;
                col = kvp.Key.Item2;
                List<double> doubles = Misc.ToDoubleList(kvp.Value);
                matrixMeanDistance.Set(row, col, Math.Round(Misc.GetMean(doubles), 4));
                matrixMedianDistance.Set(row, col, Misc.GetMedian(doubles));
                matrixMaxVals.Set(row, col, kvp.Value.Max());
                matrixMinVals.Set(row, col, kvp.Value.Min());
                matrixStdev.Set(row, col, Math.Round(Misc.GetStandardDeviation(doubles), 4));
                matrixNbrDataPoints.Set(row, col, kvp.Value.Count);
            }

            return new StructureResult(matrixMeanDistance, new Dictionary<string, IStructure>
                {
                    { "median",matrixMedianDistance },
                    { "max",matrixMaxVals },
                    { "min",matrixMinVals },
                    { "stdev",matrixStdev },
                    { "nbr_datapoints",matrixNbrDataPoints }
                });
        }

        private class CategoryStep
        {
            public byte StepsAgo {  get; set; }
            public string AttrValue { get; set; }

            public CategoryStep(byte stepsAgo = 0, string attrValue ="")
            {
                StepsAgo = stepsAgo;
                AttrValue = attrValue;
            }
        }

    }
}

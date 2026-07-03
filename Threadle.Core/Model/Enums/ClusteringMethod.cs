namespace Threadle.Core.Model.Enums
{
    public enum ClusteringMethod
    {
        Auto,           // auto-detect from layer properties
        WattsStrogatz,  // binary, treats directed as undirected
        Fagiolo,        // binary directed — counts all directed triangle types (Fagiolo 2007)
        Barrat,         // valued undirected — weights by mean edge strength (Barrat et al. 2004)
        Onnela          // valued undirected — weights by geometric mean of all three edges (Onnela et al. 2005)
    }
}
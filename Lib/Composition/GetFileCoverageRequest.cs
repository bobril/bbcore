using System.Collections.Generic;

namespace Lib.Composition
{
    public class GetFileCoverageRequest
    {
        public string FileName { get; set; }
    }

    public class GetFileCoverageResponse
    {
        // "Unknown", "Calculating", "Done"
        public string Status { get; set; }
        // Type 0 - statement: Start Line, Start Column, End Line, End Column, count
        // Type 1 - condition: Start Line, Start Column, End Line, End Column, count false, count true
        public List<int> Ranges { get; set; }
    }

}

using System;
using System.Collections.Generic;

namespace MBCC2018BeerList
{
    public class MbccData
    {
        public IList<MbccBeer> Beers { get; set; }

        public MbccData()
        {
            Beers = new List<MbccBeer>();
        }
    }
}

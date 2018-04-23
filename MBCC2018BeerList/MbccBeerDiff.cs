using System;
namespace MBCC2018BeerList
{
    public class MbccBeerDiff : MbccBeer
    {
        public AddRemove Action { get; set; }
        public DateTime Changed { get; set; }

        public static MbccBeerDiff FromBeer(MbccBeer beer, AddRemove action, DateTime changed)
        {
            var diff = new MbccBeerDiff
            {
                BreweryName = beer.BreweryName,
                BeerName = beer.BeerName,
                ABV = beer.ABV,
                Style = beer.Style,
                Session = beer.Session,
                Action = action,
                Changed = changed
            };

            return diff;
        }
	}
}

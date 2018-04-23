using System;
namespace MBCC2018BeerList
{
    public class MbccBeer
    {
        public string BreweryName { get; set; }
        public string BeerName { get; set; }
        public string ABV { get; set; }
        public string Style { get; set; }
        public CbcSession Session { get; set; }

		public override string ToString()
		{
            return $"{BreweryName} - {BeerName} - {ABV} - {Style} - {Session}";
		}

		public override int GetHashCode()
		{
            return ToString().GetHashCode();
		}

		public override bool Equals(object obj)
		{
            if (obj is MbccBeer beer)
                return obj?.ToString() == ToString();

            return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.BZip2;
using Newtonsoft.Json;
using Shrulik.NGeoKDBush;
using Shrulik.NKDBush;
using Xunit;

namespace Shrulik.NGeoKDBush.Tests
{
    public class GeoKDBushTests
    {
        private List<City> cities;
        private readonly KDBush<City> index;
        private GeoKDBush<City> geoKdBush;

        public GeoKDBushTests()
        {
            string geoDataDir = Directory.GetCurrentDirectory() + "\\Data";
            string jsonCities = geoDataDir + "\\cities.json";
            string bz2Cities = geoDataDir + "\\cities.bz2";

            if (!File.Exists(jsonCities))
            {
                if (!File.Exists(bz2Cities))
                {
                    throw new InvalidOperationException("No test data found, either as zip or json.");
                }

                BZip2.Decompress(File.OpenRead(bz2Cities), File.Create(jsonCities), true);
            }


            using (var file = File.OpenText(jsonCities))
            {
                var serializer = new JsonSerializer();
                cities = (List<City>) serializer.Deserialize(file, typeof(List<City>));
            }

            index = new KDBush<City>(cities.ToArray(), p => p.Lon, p => p.Lat, nodeSize: 10);
            geoKdBush = new GeoKDBush<City>();
        }

        [Fact]
        public void SearchWithMaxResults()
        {
            var points = geoKdBush.Around(index, -119.7051, 34.4363, 5);


            Assert.Equal(string.Join(", ", points.Select(p => p.Name)),
                "Mission Canyon, Santa Barbara, Montecito, Summerland, Goleta");
        }


        [Fact]
        public void SearchWithMaxDistances()
        {
            var points = geoKdBush.Around(index, 30.5, 50.5, int.MaxValue, 20);


            Assert.Equal(string.Join(", ", points.Select(p => p.Name)),
                "Kiev, Vyshhorod, Kotsyubyns’ke, Sofiyivska Borschagivka, Vyshneve, Kriukivschina, Irpin’, Hostomel’, Khotiv");
        }

        [Fact]
        public void SearchWithFilterFunction()
        {
            var points = geoKdBush.Around(index, 30.5, 50.5, 10, double.MaxValue, p => p.Population > 1000000);


            Assert.Equal(string.Join(", ", points.Select(p => p.Name)),
                "Kiev, Dnipropetrovsk, Kharkiv, Minsk, Odessa, Donets’k, Warsaw, Bucharest, Moscow, Rostov-na-Donu");
        }

        [Fact]
        public void ExhustiveSearchInCorrectOrder()
        {
            var searchPoint = new SimplePoint
            {
                Lon = 30.5,
                Lat = 50.5
            };

            var points = geoKdBush.Around(index, 30.5, 50.5);

            var sorted = cities.Select(c => new Node
            {
                item = c,
                dist = (float) geoKdBush.Distance(searchPoint.Lon, searchPoint.Lat, c.Lon, c.Lat)
            }).OrderBy(x => x, Comparer<Node>.Create((a, b) => a.dist > b.dist ? 1 : b.dist > a.dist ? -1 : 0
            )).ToList();


            for (var i = 0; i < sorted.Count; i++)
            {
                float dist = (float) geoKdBush.Distance(points[i].Lon, points[i].Lat,
                    searchPoint.Lon, searchPoint.Lat);

                Assert.Equal(dist, sorted[i].dist);
            }
        }

        [Fact]
        public void GreatCircleDistanceCalculation()
        {
            var calculatedDistance = Math.Round(1e4 * geoKdBush.Distance(30.5, 50.5, -119.7, 34.4)) / 1e4;

            Assert.Equal(10131.7396, calculatedDistance);
        }
    }

    internal class Node
    {
        public City item { get; set; }
        public float dist { get; set; }
    }


    internal class SimplePoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    internal class City
    {
        public string Name { get; set; }
        public string Country { get; set; }
        public string AltCountry { get; set; }
        public string Muni { get; set; }
        public string MuniSub { get; set; }
        public string FeatureCode { get; set; }
        public string AdminCode { get; set; }
        public int Population { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}

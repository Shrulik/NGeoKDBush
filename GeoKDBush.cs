using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;
using Shrulik.NKDBush;

namespace Shrulik.NGeoKDBush
{
    public class GeoKDBush<T> : IGeoKDBush<T>
    {
        public const int earthRadius = 6371;
        public const int earthCircumference = 40007;
        public const double rad = Math.PI / 180;

        public double Distance(double lng, double lat, double lng2, double lat2)
        {
            return GreatCircleDist(lng, lat, lng2, lat2, Math.Cos(lat * rad), Math.Sin(lat * rad));
        }

        public List<T> Around(IKDBush<T> index, double lng, double lat, int? maxResults = null,
            double? maxDistance = null, Predicate<T> predicate = null)
        {
            var result = new List<T>();

            if (maxResults == null)
                maxResults = int.MaxValue;
            if (maxDistance == null)
                maxDistance = double.MaxValue;
//                maxDistance = double.PositiveInfinity;

            var cosLat = Math.Cos(lat * rad);
            var sinLat = Math.Sin(lat * rad);

            // a distance-sorted priority queue that will contain both points and kd-tree nodes
            var pointsLength = index.ids.Length;
            FastPriorityQueue<Node<T>> q =
                new FastPriorityQueue<Node<T>>(pointsLength);


            // an object that represents the top kd-tree node (the whole Earth)
            var node = new Node<T>
            {
                left = 0, // left index in the kd-tree array
                right = index.ids.Length - 1, // right index
                axis = 0, // 0 for longitude axis and 1 for latitude axis
                dist = 0, // will hold the lower bound of children's distances to the query point
                minLng = -180, // bounding box of the node
                minLat = -90,
                maxLng = 180,
                maxLat = 90
            };

            while (node != null)
            {
                var right = node.right;
                var left = node.left;
                T item;

                if (right - left <= index.nodeSize)
                {
                    // leaf node

                    // add all points of the leaf node to the queue
                    for (var i = left; i <= right; i++)
                    {
                        item = index.points[index.ids[i]];
                        if (predicate == null || predicate(item))
                        {
                            var dist = GreatCircleDist(lng, lat, index.coords[2 * i], index.coords[2 * i + 1], cosLat,
                                sinLat);
                            q.Enqueue(new Node<T>
                            {
                                item = item,
                                dist = dist
                            }, (float) dist);
                        }
                    }
                }
                else
                {
                    // not a leaf node (has child nodes)

                    var m = (left + right) >> 1; // middle index

                    var midLng = index.coords[2 * m];
                    var midLat = index.coords[2 * m + 1];

                    // add middle point to the queue
                    item = index.points[index.ids[m]];
                    if (predicate == null || predicate(item))
                    {
                        var dist = GreatCircleDist(lng, lat, midLng, midLat, cosLat, sinLat);
                        q.Enqueue(new Node<T>
                        {
                            item = item,
                            dist = dist
                        }, (float) dist);
                    }

                    var nextAxis = (node.axis + 1) % 2;

                    // first half of the node
                    var leftNode = new Node<T>
                    {
                        left = left,
                        right = m - 1,
                        axis = nextAxis,
                        minLng = node.minLng,
                        minLat = node.minLat,
                        maxLng = node.axis == 0 ? midLng : node.maxLng,
                        maxLat = node.axis == 1 ? midLat : node.maxLat,
                        dist = 0
                    };
                    // second half of the node
                    var rightNode = new Node<T>
                    {
                        left = m + 1,
                        right = right,
                        axis = nextAxis,
                        minLng = node.axis == 0 ? midLng : node.minLng,
                        minLat = node.axis == 1 ? midLat : node.minLat,
                        maxLng = node.maxLng,
                        maxLat = node.maxLat,
                        dist = 0
                    };

                    leftNode.dist = BoxDist(lng, lat, leftNode, cosLat, sinLat);
                    rightNode.dist = BoxDist(lng, lat, rightNode, cosLat, sinLat);

                    // add child nodes to the queue

                    q.Enqueue(leftNode, (float) leftNode.dist);
                    q.Enqueue(rightNode, (float) rightNode.dist);
                }

                // fetch closest points from the queue; they're guaranteed to be closer
                // than all remaining points (both individual and those in kd-tree nodes),
                // since each node's distance is a lower bound of distances to its children
                while (q.Any() && q.First.item != null)
                {
                    var candidate = q.Dequeue();
                    if (candidate.dist > maxDistance) return result;
                    result.Add(candidate.item);
                    if (result.Count == maxResults) return result;
                }

                // the next closest kd-tree node
                node = q.Count > 0 ? q.Dequeue() : null;
            }

            return result;
        }


        internal double BoxDist(double lng, double lat, Node<T> node, double cosLat, double sinLat)
        {
            var minLng = node.minLng;
            var maxLng = node.maxLng;
            var minLat = node.minLat;
            var maxLat = node.maxLat;

            // query point is between minimum and maximum longitudes
            if (lng >= minLng && lng <= maxLng)
            {
                if (lat <= minLat) return earthCircumference * (minLat - lat) / 360; // south
                if (lat >= maxLat) return earthCircumference * (lat - maxLat) / 360; // north
                return 0; // inside the bbox
            }

            // query point is west or east of the bounding box;
            // calculate the extremum for great circle distance from query point to the closest longitude
            var closestLng = (minLng - lng + 360) % 360 <= (lng - maxLng + 360) % 360 ? minLng : maxLng;
            var cosLngDelta = Math.Cos((closestLng - lng) * rad);
            var extremumLat = Math.Atan(sinLat / (cosLat * cosLngDelta)) / rad;

            // calculate distances to lower and higher bbox corners and extremum (if it's within this range);
            // one of the three distances will be the lower bound of great circle distance to bbox
            var d = Math.Max(
                GreatCircleDistPart(minLat, cosLat, sinLat, cosLngDelta),
                GreatCircleDistPart(maxLat, cosLat, sinLat, cosLngDelta));

            if (extremumLat > minLat && extremumLat < maxLat)
            {
                d = Math.Max(d, GreatCircleDistPart(extremumLat, cosLat, sinLat, cosLngDelta));
            }

            return earthRadius * Math.Acos(d);
        }

        internal double CompareDist(Node<T> a, Node<T> b)
        {
            return a.dist - b.dist;
        }

        // distance using spherical law of cosines; should be precise enough for our needs
        internal double GreatCircleDist(double lng, double lat, double lng2, double lat2, double cosLat, double sinLat)
        {
            var cosLngDelta = Math.Cos((lng2 - lng) * rad);
            return earthRadius * Math.Acos(GreatCircleDistPart(lat2, cosLat, sinLat, cosLngDelta));
        }

        // partial greatCircleDist to reduce trigonometric calculations
        internal double GreatCircleDistPart(double lat, double cosLat, double sinLat, double cosLngDelta)
        {
            var d = sinLat * Math.Sin(lat * rad) +
                    cosLat * Math.Cos(lat * rad) * cosLngDelta;
            return Math.Min(d, 1);
        }
    }    

    internal class Node<T> : FastPriorityQueueNode
    {
        public int left;
        public int right;
        public int axis;
        public double dist;
        public double minLng;
        public double minLat;
        public double maxLng;
        public double maxLat;
        public T item { get; set; }
    }
}
